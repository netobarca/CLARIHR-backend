using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Leave;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Leave.Common;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.Leave;

namespace CLARIHR.Application.Features.Leave;

internal sealed class SearchMedicalClinicsQueryHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IMedicalClinicRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchMedicalClinicsQuery, PagedResponse<MedicalClinicListItemResponse>>
{
    public async Task<Result<PagedResponse<MedicalClinicListItemResponse>>> Handle(
        SearchMedicalClinicsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<MedicalClinicListItemResponse>>.Failure(authorizationResult.Error);
        }

        var response = await repository.SearchAsync(
            query.CompanyId,
            query.IsActive,
            query.SectorCode,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (!query.IncludeAllowedActions)
        {
            return Result<PagedResponse<MedicalClinicListItemResponse>>.Success(response);
        }

        var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = response.Items
            .Select(item => MedicalClinicPolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManage))
            .ToArray();
        response = response with { Items = items };

        return Result<PagedResponse<MedicalClinicListItemResponse>>.Success(response);
    }
}

internal sealed class GetMedicalClinicByIdQueryHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IMedicalClinicRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetMedicalClinicByIdQuery, MedicalClinicResponse>
{
    public async Task<Result<MedicalClinicResponse>> Handle(
        GetMedicalClinicByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<MedicalClinicResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<MedicalClinicResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetResponseByIdAsync(query.MedicalClinicId, cancellationToken);
        if (response is not null)
        {
            var canManage = (await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = MedicalClinicPolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManage);

            return Result<MedicalClinicResponse>.Success(response);
        }

        return Result<MedicalClinicResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.MedicalClinicId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : MedicalClinicErrors.MedicalClinicNotFound);
    }
}

internal sealed class CreateMedicalClinicCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IMedicalClinicRepository repository,
    IPersonnelFileRepository personnelFileRepository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateMedicalClinicCommand, MedicalClinicResponse>
{
    public async Task<Result<MedicalClinicResponse>> Handle(
        CreateMedicalClinicCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<MedicalClinicResponse>.Failure(authorizationResult.Error);
        }

        if (await repository.DescriptionExistsAsync(
                command.CompanyId,
                command.Description.Trim().ToUpperInvariant(),
                excludingMedicalClinicId: null,
                cancellationToken))
        {
            return Result<MedicalClinicResponse>.Failure(MedicalClinicErrors.DescriptionConflict);
        }

        var sectorResult = await MedicalClinicRules.ValidateSectorCodeAsync(
            personnelFileRepository,
            command.CompanyId,
            command.SectorCode,
            cancellationToken);
        if (sectorResult.IsFailure)
        {
            return Result<MedicalClinicResponse>.Failure(sectorResult.Error);
        }

        var medicalClinic = MedicalClinic.Create(
            command.Description,
            command.Specialty,
            command.SectorCode);
        medicalClinic.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(medicalClinic);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetResponseByIdAsync(medicalClinic.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Medical clinic response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.MedicalClinicCreated,
                    AuditEntityTypes.MedicalClinic,
                    medicalClinic.PublicId,
                    medicalClinic.Description,
                    AuditActions.Create,
                    $"Created medical clinic {medicalClinic.Description}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<MedicalClinicResponse>.Success(response);
        }
        catch (UniqueConstraintViolationException ex) when (MedicalClinicConstraintViolations.IsDescriptionConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<MedicalClinicResponse>.Failure(MedicalClinicErrors.DescriptionConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateMedicalClinicCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IMedicalClinicRepository repository,
    IPersonnelFileRepository personnelFileRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateMedicalClinicCommand, MedicalClinicResponse>
{
    public async Task<Result<MedicalClinicResponse>> Handle(
        UpdateMedicalClinicCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<MedicalClinicResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<MedicalClinicResponse>.Failure(authorizationResult.Error);
        }

        var medicalClinic = await repository.GetByIdAsync(command.MedicalClinicId, cancellationToken);
        if (medicalClinic is null)
        {
            return Result<MedicalClinicResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.MedicalClinicId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : MedicalClinicErrors.MedicalClinicNotFound);
        }

        if (medicalClinic.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<MedicalClinicResponse>.Failure(LeaveConfigurationErrors.ConcurrencyConflict);
        }

        if (await repository.DescriptionExistsAsync(
                medicalClinic.TenantId,
                command.Description.Trim().ToUpperInvariant(),
                medicalClinic.PublicId,
                cancellationToken))
        {
            return Result<MedicalClinicResponse>.Failure(MedicalClinicErrors.DescriptionConflict);
        }

        var sectorResult = await MedicalClinicRules.ValidateSectorCodeAsync(
            personnelFileRepository,
            medicalClinic.TenantId,
            command.SectorCode,
            cancellationToken);
        if (sectorResult.IsFailure)
        {
            return Result<MedicalClinicResponse>.Failure(sectorResult.Error);
        }

        var before = await repository.GetResponseByIdAsync(medicalClinic.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Medical clinic response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            medicalClinic.Update(
                command.Description,
                command.Specialty,
                command.SectorCode);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(medicalClinic.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Medical clinic response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.MedicalClinicUpdated,
                    AuditEntityTypes.MedicalClinic,
                    medicalClinic.PublicId,
                    medicalClinic.Description,
                    AuditActions.Update,
                    $"Updated medical clinic {medicalClinic.Description}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<MedicalClinicResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException ex) when (MedicalClinicConstraintViolations.IsDescriptionConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<MedicalClinicResponse>.Failure(MedicalClinicErrors.DescriptionConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateMedicalClinicCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IMedicalClinicRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateMedicalClinicCommand, MedicalClinicResponse>
{
    public async Task<Result<MedicalClinicResponse>> Handle(
        ActivateMedicalClinicCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<MedicalClinicResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<MedicalClinicResponse>.Failure(authorizationResult.Error);
        }

        var medicalClinic = await repository.GetByIdAsync(command.MedicalClinicId, cancellationToken);
        if (medicalClinic is null)
        {
            return Result<MedicalClinicResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.MedicalClinicId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : MedicalClinicErrors.MedicalClinicNotFound);
        }

        if (medicalClinic.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<MedicalClinicResponse>.Failure(LeaveConfigurationErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(medicalClinic.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Medical clinic response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            medicalClinic.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(medicalClinic.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Medical clinic response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.MedicalClinicActivated,
                    AuditEntityTypes.MedicalClinic,
                    medicalClinic.PublicId,
                    medicalClinic.Description,
                    AuditActions.Reactivate,
                    $"Activated medical clinic {medicalClinic.Description}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<MedicalClinicResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateMedicalClinicCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IMedicalClinicRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateMedicalClinicCommand, MedicalClinicResponse>
{
    public async Task<Result<MedicalClinicResponse>> Handle(
        InactivateMedicalClinicCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<MedicalClinicResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<MedicalClinicResponse>.Failure(authorizationResult.Error);
        }

        var medicalClinic = await repository.GetByIdAsync(command.MedicalClinicId, cancellationToken);
        if (medicalClinic is null)
        {
            return Result<MedicalClinicResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.MedicalClinicId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : MedicalClinicErrors.MedicalClinicNotFound);
        }

        if (medicalClinic.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<MedicalClinicResponse>.Failure(LeaveConfigurationErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(medicalClinic.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Medical clinic response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            medicalClinic.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(medicalClinic.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Medical clinic response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.MedicalClinicInactivated,
                    AuditEntityTypes.MedicalClinic,
                    medicalClinic.PublicId,
                    medicalClinic.Description,
                    AuditActions.Deactivate,
                    $"Inactivated medical clinic {medicalClinic.Description}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<MedicalClinicResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class MedicalClinicRules
{
    /// <summary>
    /// Validates the optional sector code against the country-scoped <c>clinic-sectors</c> general
    /// catalog (ISSS / pública / privada) when it travels — 422 when unknown or inactive (mirrors
    /// the other country-catalog code validations via CatalogCodeIsActiveAsync).
    /// </summary>
    public static async Task<Result> ValidateSectorCodeAsync(
        IPersonnelFileRepository personnelFileRepository,
        Guid companyId,
        string? sectorCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sectorCode))
        {
            return Result.Success();
        }

        return await personnelFileRepository.CatalogCodeIsActiveAsync(
                companyId,
                PersonnelCurriculumCatalogCategories.ClinicSector,
                sectorCode,
                cancellationToken)
            ? Result.Success()
            : Result.Failure(MedicalClinicErrors.SectorInvalid);
    }
}

internal static class MedicalClinicConstraintViolations
{
    // The (TenantId, NormalizedDescription) unique index is the real guard against duplicate
    // descriptions; the up-front DescriptionExistsAsync probe only closes the common (sequential)
    // case. On a concurrent create/update of the same description, the second writer trips this
    // index — map it to the same clean 409 as the probe instead of letting the 23505 escape as an
    // HTTP 500 (mirrors CostCenterConstraintViolations).
    public static bool IsDescriptionConflict(string? constraintName) =>
        string.Equals(constraintName, LeaveMasterConstraintNames.MedicalClinicDescriptionUnique, StringComparison.Ordinal);
}

internal static class MedicalClinicPolicyAdapter
{
    public static MedicalClinicListItemResponse ApplyAllowedActions(
        MedicalClinicListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                LeaveConfigurationPermissionCodes.MedicalClinicsResourceKey,
                response.SectorCode,
                response.IsActive,
                SupportsEdit: true,
                EditAllowed: canManage,
                SupportsDelete: false,
                SupportsArchive: false,
                SupportsActivate: true,
                ActivateAllowed: canManage,
                SupportsInactivate: true,
                InactivateAllowed: canManage));

        return response with { AllowedActions = allowedActions };
    }

    public static MedicalClinicResponse ApplyAllowedActions(
        MedicalClinicResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                LeaveConfigurationPermissionCodes.MedicalClinicsResourceKey,
                response.SectorCode,
                response.IsActive,
                SupportsEdit: true,
                EditAllowed: canManage,
                SupportsDelete: false,
                SupportsArchive: false,
                SupportsActivate: true,
                ActivateAllowed: canManage,
                SupportsInactivate: true,
                InactivateAllowed: canManage));

        return response with { AllowedActions = allowedActions };
    }
}
