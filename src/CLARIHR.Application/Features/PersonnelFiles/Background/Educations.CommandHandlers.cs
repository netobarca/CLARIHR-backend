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

internal sealed class AddPersonnelFileEducationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IEducationCatalogRepository educationCatalogRepository,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddPersonnelFileEducationCommand, PersonnelFileEducationResponse>
{
    public async Task<Result<PersonnelFileEducationResponse>> Handle(
        AddPersonnelFileEducationCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileEducationResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileEducationResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Educations,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileEducationResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }


        var (catalogError, resolvedIds) = await ResolveEducationCatalogIdsAsync(
            command.Education, educationCatalogRepository, repository, cancellationToken);
        if (catalogError != Error.None)
        {
            return Result<PersonnelFileEducationResponse>.Failure(catalogError);
        }

        PersonnelFileEducation education;
        try
        {
            education = PersonnelFileEducation.Create(
                resolvedIds!.StatusId,
                command.Education.DegreeTitle,
                resolvedIds.StudyTypeId,
                resolvedIds.CareerId,
                command.Education.Institution,
                command.Education.CountryCode,
                command.Education.Specialty,
                command.Education.IsCurrentlyStudying,
                command.Education.StartDate,
                command.Education.EndDate,
                resolvedIds.ShiftId,
                resolvedIds.ModalityId,
                command.Education.TotalSubjects,
                command.Education.ApprovedSubjects);
        }
        catch (InvalidOperationException)
        {
            return Result<PersonnelFileEducationResponse>.Failure(PersonnelFileErrors.EffectiveDatesInvalid);
        }

        var before = await repository.GetEducationsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.AddEducation(education);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetEducationsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == education.PublicId)
                ?? throw new InvalidOperationException("Personnel file education response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Added education for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Educations,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Educations,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileEducationResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static Error CreateEducationCatalogValidationError(string fieldName, Guid publicId) =>
        ErrorCatalog.Validation(
            new Dictionary<string, string[]>
            {
                [fieldName] = [$"Catalog item '{publicId}' is not active or does not belong to the tenant."]
            });

    internal static async Task<(Error Error, ResolvedEducationCatalogIds? Ids)> ResolveEducationCatalogIdsAsync(
        EducationInput input,
        IEducationCatalogRepository catalogRepository,
        IPersonnelFileRepository fileRepository,
        CancellationToken cancellationToken)
    {
        var statusLookup = await catalogRepository.GetActiveLookupByIdAsync(
            EducationCatalogType.EducationStatus,
            input.StatusPublicId,
            cancellationToken);
        if (statusLookup is null)
        {
            return (CreateEducationCatalogValidationError(nameof(input.StatusPublicId), input.StatusPublicId), null);
        }

        var studyTypeLookup = await catalogRepository.GetActiveLookupByIdAsync(
            EducationCatalogType.StudyType,
            input.StudyTypePublicId,
            cancellationToken);
        if (studyTypeLookup is null)
        {
            return (CreateEducationCatalogValidationError(nameof(input.StudyTypePublicId), input.StudyTypePublicId), null);
        }

        var careerLookup = await catalogRepository.GetActiveLookupByIdAsync(
            EducationCatalogType.Career,
            input.CareerPublicId,
            cancellationToken);
        if (careerLookup is null)
        {
            return (CreateEducationCatalogValidationError(nameof(input.CareerPublicId), input.CareerPublicId), null);
        }

        var countryError = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
            fileRepository,
            Guid.Empty, // country validation is now tenant-independent for education catalogs
            nameof(input.CountryCode),
            PersonnelCurriculumCatalogCategories.Country,
            input.CountryCode,
            cancellationToken);
        if (countryError != Error.None)
        {
            return (countryError, null);
        }

        long? shiftId = null;
        if (input.ShiftPublicId.HasValue)
        {
            var shiftLookup = await catalogRepository.GetActiveLookupByIdAsync(
                EducationCatalogType.Shift,
                input.ShiftPublicId.Value,
                cancellationToken);
            if (shiftLookup is null)
            {
                return (CreateEducationCatalogValidationError(nameof(input.ShiftPublicId), input.ShiftPublicId.Value), null);
            }

            shiftId = shiftLookup.InternalId;
        }

        long? modalityId = null;
        if (input.ModalityPublicId.HasValue)
        {
            var modalityLookup = await catalogRepository.GetActiveLookupByIdAsync(
                EducationCatalogType.Modality,
                input.ModalityPublicId.Value,
                cancellationToken);
            if (modalityLookup is null)
            {
                return (CreateEducationCatalogValidationError(nameof(input.ModalityPublicId), input.ModalityPublicId.Value), null);
            }

            modalityId = modalityLookup.InternalId;
        }

        return (Error.None, new ResolvedEducationCatalogIds(
            statusLookup.InternalId,
            studyTypeLookup.InternalId,
            careerLookup.InternalId,
            shiftId,
            modalityId));
    }

    internal sealed record ResolvedEducationCatalogIds(
        long StatusId,
        long StudyTypeId,
        long CareerId,
        long? ShiftId,
        long? ModalityId);
}

internal sealed class UpdatePersonnelFileEducationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IEducationCatalogRepository educationCatalogRepository,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePersonnelFileEducationCommand, PersonnelFileEducationResponse>
{
    public async Task<Result<PersonnelFileEducationResponse>> Handle(
        UpdatePersonnelFileEducationCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileEducationResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileEducationResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Educations,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileEducationResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        var education = personnelFile.Educations.FirstOrDefault(item => item.PublicId == command.EducationPublicId);
        if (education is null)
        {
            return Result<PersonnelFileEducationResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (education.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileEducationResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var (catalogError, resolvedIds) = await AddPersonnelFileEducationCommandHandler.ResolveEducationCatalogIdsAsync(
            command.Education, educationCatalogRepository, repository, cancellationToken);
        if (catalogError != Error.None)
        {
            return Result<PersonnelFileEducationResponse>.Failure(catalogError);
        }

        var before = await repository.GetEducationsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateEducation(
                command.EducationPublicId,
                resolvedIds!.StatusId,
                command.Education.DegreeTitle,
                resolvedIds.StudyTypeId,
                resolvedIds.CareerId,
                command.Education.Institution,
                command.Education.CountryCode,
                command.Education.Specialty,
                command.Education.IsCurrentlyStudying,
                command.Education.StartDate,
                command.Education.EndDate,
                resolvedIds.ShiftId,
                resolvedIds.ModalityId,
                command.Education.TotalSubjects,
                command.Education.ApprovedSubjects);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetEducationsAsync(personnelFile.PublicId, cancellationToken);
            var response = after.SingleOrDefault(item => item.Id == command.EducationPublicId)
                ?? throw new InvalidOperationException("Personnel file education response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Updated education for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Educations,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Educations,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileEducationResponse>.Success(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Result<PersonnelFileEducationResponse>.Failure(PersonnelFileErrors.NotFound);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeletePersonnelFileEducationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeletePersonnelFileEducationCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFileEducationCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Educations,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        var education = personnelFile.Educations.FirstOrDefault(item => item.PublicId == command.EducationPublicId);
        if (education is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (education.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetEducationsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.RemoveEducation(command.EducationPublicId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetEducationsAsync(personnelFile.PublicId, cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Deleted education for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Educations,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Educations,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileParentConcurrencyResult>.Success(
                new PersonnelFileParentConcurrencyResult(personnelFile.ConcurrencyToken));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class PatchPersonnelFileEducationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IEducationCatalogRepository educationCatalogRepository,
    IPersonnelFileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchPersonnelFileEducationCommand, PersonnelFileEducationResponse>
{
    public async Task<Result<PersonnelFileEducationResponse>> Handle(
        PatchPersonnelFileEducationCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileEducationResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PersonnelFileEducationResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(
            command.PersonnelFileId,
            PersonnelFileTrackedSection.Educations,
            cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileEducationResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        var education = personnelFile.Educations.FirstOrDefault(item => item.PublicId == command.EducationPublicId);
        if (education is null)
        {
            return Result<PersonnelFileEducationResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (education.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileEducationResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetEducationAsync(personnelFile.PublicId, command.EducationPublicId, cancellationToken);
        if (before is null)
        {
            return Result<PersonnelFileEducationResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var state = PersonnelFileEducationPatchState.From(before);
        var applyResult = PersonnelFileEducationPatchApplier.Apply(command.Operations, state);
        if (applyResult.IsFailure)
        {
            return Result<PersonnelFileEducationResponse>.Failure(applyResult.Error);
        }

        var validation = PersonnelFileEducationPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<PersonnelFileEducationResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<PersonnelFileEducationResponse>.Success(before);
        }

        var input = state.ToInput();
        var (catalogError, resolvedIds) = await AddPersonnelFileEducationCommandHandler.ResolveEducationCatalogIdsAsync(
            input, educationCatalogRepository, repository, cancellationToken);
        if (catalogError != Error.None)
        {
            return Result<PersonnelFileEducationResponse>.Failure(catalogError);
        }

        var beforeList = await repository.GetEducationsAsync(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            personnelFile.UpdateEducation(
                command.EducationPublicId,
                resolvedIds!.StatusId,
                input.DegreeTitle,
                resolvedIds.StudyTypeId,
                resolvedIds.CareerId,
                input.Institution,
                input.CountryCode,
                input.Specialty,
                input.IsCurrentlyStudying,
                input.StartDate,
                input.EndDate,
                resolvedIds.ShiftId,
                resolvedIds.ModalityId,
                input.TotalSubjects,
                input.ApprovedSubjects);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var afterList = await repository.GetEducationsAsync(personnelFile.PublicId, cancellationToken);
            var response = afterList.SingleOrDefault(item => item.Id == command.EducationPublicId)
                ?? throw new InvalidOperationException("Personnel file education response could not be resolved after patch.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileUpdated,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Patched education for personnel file {personnelFile.FullName}.",
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Educations,
                        data = beforeList
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.Educations,
                        data = afterList,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileEducationResponse>.Success(response);
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PersonnelFileEducationResponse>.Failure(PersonnelFileErrors.EffectiveDatesInvalid);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

