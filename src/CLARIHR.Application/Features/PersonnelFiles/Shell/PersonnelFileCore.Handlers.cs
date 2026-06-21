using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Banks;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.EducationCatalogs;
using CLARIHR.Application.Abstractions.DocumentTypeCatalogs;
using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Features.Files.Common;
using CLARIHR.Domain.Files;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.EducationCatalogs.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;
using FluentValidation.Results;

namespace CLARIHR.Application.Features.PersonnelFiles;

internal sealed class SearchPersonnelFilesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchPersonnelFilesQuery, PagedResponse<PersonnelFileListItemResponse>>
{
    public async Task<Result<PagedResponse<PersonnelFileListItemResponse>>> Handle(
        SearchPersonnelFilesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<PersonnelFileListItemResponse>>.Failure(authorizationResult.Error);
        }

        var response = await repository.SearchAsync(
            query.CompanyId,
            query.IsActive,
            query.RecordType,
            query.OrgUnitId,
            query.MinAge,
            query.MaxAge,
            query.MaritalStatus,
            query.Nationality,
            query.Profession,
            query.CreatedFromUtc,
            query.CreatedToUtc,
            query.Search,
            query.SortBy,
            query.SortDirection,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (!query.IncludeAllowedActions)
        {
            return Result<PagedResponse<PersonnelFileListItemResponse>>.Success(response);
        }

        var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = response.Items
            .Select(item => PersonnelFilePolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManage))
            .ToArray();
        response = response with { Items = items };

        return Result<PagedResponse<PersonnelFileListItemResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetPersonnelFileByIdQuery, PersonnelFileShellResponse>
{
    public async Task<Result<PersonnelFileShellResponse>> Handle(
        GetPersonnelFileByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileShellResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileShellResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetShellByIdAsync(query.PersonnelFileId, cancellationToken);
        if (response is not null)
        {
            var canManage = (await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = PersonnelFilePolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManage);
            return Result<PersonnelFileShellResponse>.Success(response);
        }

        return Result<PersonnelFileShellResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.PersonnelFileId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : PersonnelFileErrors.NotFound);
    }
}

internal sealed class CreatePersonnelFileCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    IPersonnelFileProfilePhotoService profilePhotoService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreatePersonnelFileCommand, PersonnelFileShellResponse>
{
    private sealed record PersonnelFileLifecycleAuditSnapshot(
        Guid PublicId,
        Guid CompanyId,
        PersonnelFileRecordType RecordType,
        PersonnelFileLifecycleStatus LifecycleStatus,
        string FullName,
        Guid? PhotoFilePublicId,
        bool IsActive,
        Guid? OrgUnitId,
        Guid? LinkedUserId,
        Guid ConcurrencyToken,
        DateTime CreatedAtUtc,
        DateTime? ModifiedAtUtc);

    public async Task<Result<PersonnelFileShellResponse>> Handle(
        CreatePersonnelFileCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileShellResponse>.Failure(authorizationResult.Error);
        }



        var personalInfoCatalogValidation = await PersonnelReferenceCatalogValidation.ValidatePersonalInfoCodesAsync(
            repository,
            command.CompanyId,
            command.MaritalStatusCode,
            command.ProfessionCode,
            command.BirthCountryCode,
            command.BirthDepartmentCode,
            command.BirthMunicipalityCode,
            cancellationToken);
        if (personalInfoCatalogValidation != Error.None)
        {
            return Result<PersonnelFileShellResponse>.Failure(personalInfoCatalogValidation);
        }

        var personnelFile = PersonnelFile.Create(
            command.RecordType,
            command.FirstName,
            command.LastName,
            command.BirthDate,
            command.MaritalStatusCode,
            command.ProfessionCode,
            command.Nationality,
            command.PersonalEmail,
            command.InstitutionalEmail,
            command.PersonalPhone,
            command.InstitutionalPhone,
            command.BirthCountryCode,
            command.BirthDepartmentCode,
            command.BirthMunicipalityCode,
            photoFilePublicId: null,
            command.OrgUnitId);
        personnelFile.SetTenantId(command.CompanyId);

        var photoWritePlanResult = await profilePhotoService.PrepareWriteAsync(
            command.CompanyId,
            personnelFile.PublicId,
            command.PhotoFilePublicId,
            currentPersistedPhotoFilePublicId: null,
            cancellationToken);
        if (photoWritePlanResult.IsFailure)
        {
            return Result<PersonnelFileShellResponse>.Failure(photoWritePlanResult.Error);
        }

        var photoWritePlan = photoWritePlanResult.Value;
        personnelFile.UpdatePersonalInfo(
            command.RecordType,
            command.FirstName,
            command.LastName,
            command.BirthDate,
            command.MaritalStatusCode,
            command.ProfessionCode,
            command.Nationality,
            command.PersonalEmail,
            command.InstitutionalEmail,
            command.PersonalPhone,
            command.InstitutionalPhone,
            command.BirthCountryCode,
            command.BirthDepartmentCode,
            command.BirthMunicipalityCode,
            photoWritePlan.PersistedPhotoFilePublicId,
            command.OrgUnitId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var shellResponse = CreateShellResponse(personnelFile);
            var auditSnapshot = CreateAuditSnapshot(personnelFile);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileCreated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Create,
                    $"Created personnel file {personnelFile.FullName}.",
                    After: auditSnapshot),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            await profilePhotoService.CleanupAfterPersistenceSuccessAsync(photoWritePlan, cancellationToken);
            return Result<PersonnelFileShellResponse>.Success(shellResponse);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            await profilePhotoService.CleanupAfterPersistenceFailureAsync(photoWritePlan, cancellationToken);
            throw;
        }
    }

    private static PersonnelFileShellResponse CreateShellResponse(PersonnelFile personnelFile) =>
        new(
            personnelFile.PublicId,
            personnelFile.TenantId,
            personnelFile.RecordType,
            personnelFile.LifecycleStatus,
            personnelFile.FullName,
            personnelFile.PhotoFilePublicId?.ToString(),
            personnelFile.IsActive,
            personnelFile.OrgUnitPublicId,
            personnelFile.LinkedUserPublicId,
            personnelFile.ConcurrencyToken,
            personnelFile.CreatedUtc,
            personnelFile.ModifiedUtc);

    private static PersonnelFileLifecycleAuditSnapshot CreateAuditSnapshot(PersonnelFile personnelFile) =>
        new(
            personnelFile.PublicId,
            personnelFile.TenantId,
            personnelFile.RecordType,
            personnelFile.LifecycleStatus,
            personnelFile.FullName,
            personnelFile.PhotoFilePublicId,
            personnelFile.IsActive,
            personnelFile.OrgUnitPublicId,
            personnelFile.LinkedUserPublicId,
            personnelFile.ConcurrencyToken,
            personnelFile.CreatedUtc,
            personnelFile.ModifiedUtc);
}

internal sealed class UpdatePersonnelFileCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    IPersonnelFileProfilePhotoService profilePhotoService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePersonnelFileCommand, PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse>>
{
    public async Task<Result<PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse>>> Handle(
        UpdatePersonnelFileCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse>>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForAccessCheckAsync(command.PersonnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse>>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse>>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (personnelFile.RecordType != command.RecordType)
        {
            return Result<PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse>>.Failure(PersonnelFileErrors.RecordTypeTransitionNotAllowed);
        }

        // PUT never changes the active state (that is PATCH's job), so pass the current value
        // as the desired one (no toggle) and always log PersonnelFileUpdated.
        var applyResult = await ReplacePersonnelFileSectionCommandHandlerBase.ApplyPersonalInfoAsync(
            personnelFile,
            command,
            desiredIsActive: personnelFile.IsActive,
            static file => (
                AuditEventTypes.PersonnelFileUpdated,
                AuditActions.Update,
                $"Updated personnel file {file.FullName} personal info."),
            repository,
            profilePhotoService,
            auditService,
            unitOfWork,
            cancellationToken);

        return applyResult.IsSuccess
            ? Result<PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse>>.Success(
                ReplacePersonnelFileSectionCommandHandlerBase.CreateSectionResult(personnelFile, applyResult.Value))
            : Result<PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse>>.Failure(applyResult.Error);
    }
}

internal sealed class PatchPersonnelFileCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    IPersonnelFileProfilePhotoService profilePhotoService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchPersonnelFileCommand, PersonnelFilePersonalInfoResponse>
{
    public async Task<Result<PersonnelFilePersonalInfoResponse>> Handle(
        PatchPersonnelFileCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFilePersonalInfoResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFilePersonalInfoResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForAccessCheckAsync(command.PersonnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFilePersonalInfoResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFilePersonalInfoResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Seed the patch state from the current entity so unspecified members keep their
        // values, then apply the RFC 6902 operations (root-path members only).
        var state = PersonnelFilePatchState.From(personnelFile);
        var applyResult = PersonnelFilePatchApplier.Apply(command.Operations, state);
        if (applyResult.IsFailure)
        {
            return Result<PersonnelFilePersonalInfoResponse>.Failure(applyResult.Error);
        }

        if (personnelFile.RecordType != state.RecordType)
        {
            return Result<PersonnelFilePersonalInfoResponse>.Failure(PersonnelFileErrors.RecordTypeTransitionNotAllowed);
        }

        // Reuse the canonical personal-info validation rules (name/phone/code formats)
        // on the patched result so PATCH and PUT validate identically, instead of
        // maintaining a parallel rule set.
        var candidate = new UpdatePersonnelFileCommand(
            command.PersonnelFileId,
            state.RecordType,
            state.FirstName,
            state.LastName,
            state.BirthDate,
            state.MaritalStatusCode,
            state.ProfessionCode,
            state.Nationality,
            state.PersonalEmail,
            state.InstitutionalEmail,
            state.PersonalPhone,
            state.InstitutionalPhone,
            state.BirthCountryCode,
            state.BirthDepartmentCode,
            state.BirthMunicipalityCode,
            state.PhotoFilePublicId,
            state.OrgUnitPublicId,
            command.ConcurrencyToken);

        var validation = new UpdatePersonnelFileCommandValidator().Validate(candidate);
        if (!validation.IsValid)
        {
            return Result<PersonnelFilePersonalInfoResponse>.Failure(
                ErrorCatalog.Validation(ToValidationDictionary(validation.Errors)));
        }

        // The "not rehireable" mark (D-11/D-18) lives on the root file so it survives the 1:1
        // employee-profile overwrite a future rehire performs. It is captured here — typically in
        // the same PATCH that sets isActive=false at retirement — and applied to the tracked
        // entity so it persists inside the shared personal-info transaction below.
        if (state.IsRehireBlocked != personnelFile.IsRehireBlocked ||
            !string.Equals(state.RehireBlockedReason, personnelFile.RehireBlockedReason, StringComparison.Ordinal))
        {
            if (state.IsRehireBlocked)
            {
                personnelFile.BlockRehire(state.RehireBlockedReason);
            }
            else
            {
                personnelFile.ClearRehireBlock();
            }
        }

        // The unified PATCH absorbs the retired /activate and /inactivate endpoints: toggling
        // `isActive` drives the lifecycle transition, which selects the audit event below.
        // Capture the pre-mutation state so the post-mutation auditFactory can classify it.
        var wasActive = personnelFile.IsActive;
        return await ReplacePersonnelFileSectionCommandHandlerBase.ApplyPersonalInfoAsync(
            personnelFile,
            candidate,
            desiredIsActive: state.IsActive,
            file => (wasActive, file.IsActive) switch
            {
                (false, true) => (AuditEventTypes.PersonnelFileActivated, AuditActions.Reactivate, $"Activated personnel file {file.FullName}."),
                (true, false) => (AuditEventTypes.PersonnelFileInactivated, AuditActions.Deactivate, $"Inactivated personnel file {file.FullName}."),
                _ => (AuditEventTypes.PersonnelFileUpdated, AuditActions.Update, $"Patched personnel file {file.FullName}."),
            },
            repository,
            profilePhotoService,
            auditService,
            unitOfWork,
            cancellationToken);
    }

    // Mirror the FluentValidation→ProblemDetails mapping used by the dispatcher
    // (RequestDispatcher.ToDictionary) so manually-run validation yields the same contract.
    private static IReadOnlyDictionary<string, string[]> ToValidationDictionary(IEnumerable<ValidationFailure> failures) =>
        failures
            .GroupBy(
                static failure => JsonNamingPolicy.CamelCase.ConvertName(failure.PropertyName),
                static failure => failure.ErrorMessage)
            .ToDictionary(
                static group => group.Key,
                static group => group.Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
}

