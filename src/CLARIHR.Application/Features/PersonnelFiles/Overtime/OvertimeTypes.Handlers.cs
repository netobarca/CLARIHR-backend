using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Overtime;
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
using CLARIHR.Application.Features.PersonnelFiles.Overtime.Common;
using CLARIHR.Domain.Overtime;

namespace CLARIHR.Application.Features.PersonnelFiles.Overtime;

internal sealed class SearchOvertimeTypesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IOvertimeTypeRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchOvertimeTypesQuery, PagedResponse<OvertimeTypeListItemResponse>>
{
    public async Task<Result<PagedResponse<OvertimeTypeListItemResponse>>> Handle(
        SearchOvertimeTypesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewOvertimeRecordsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<OvertimeTypeListItemResponse>>.Failure(authorizationResult.Error);
        }

        var response = await repository.SearchAsync(
            query.CompanyId,
            query.IsActive,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (!query.IncludeAllowedActions)
        {
            return Result<PagedResponse<OvertimeTypeListItemResponse>>.Success(response);
        }

        var canManage = (await authorizationService.EnsureCanManageOvertimeRecordsAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = response.Items
            .Select(item => OvertimeTypePolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManage))
            .ToArray();
        response = response with { Items = items };

        return Result<PagedResponse<OvertimeTypeListItemResponse>>.Success(response);
    }
}

internal sealed class GetOvertimeTypeByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IOvertimeTypeRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetOvertimeTypeByIdQuery, OvertimeTypeResponse>
{
    public async Task<Result<OvertimeTypeResponse>> Handle(
        GetOvertimeTypeByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<OvertimeTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanViewOvertimeRecordsAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OvertimeTypeResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetResponseByIdAsync(query.OvertimeTypeId, cancellationToken);
        if (response is not null)
        {
            var canManage = (await authorizationService.EnsureCanManageOvertimeRecordsAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = OvertimeTypePolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManage);

            return Result<OvertimeTypeResponse>.Success(response);
        }

        return Result<OvertimeTypeResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.OvertimeTypeId, cancellationToken)
                ? OvertimeTypeErrors.TenantMismatch(RbacPermissionAction.Read)
                : OvertimeTypeErrors.OvertimeTypeNotFound);
    }
}

internal sealed class CreateOvertimeTypeCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IOvertimeTypeRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateOvertimeTypeCommand, OvertimeTypeResponse>
{
    public async Task<Result<OvertimeTypeResponse>> Handle(
        CreateOvertimeTypeCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageOvertimeRecordsAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OvertimeTypeResponse>.Failure(authorizationResult.Error);
        }

        if (await repository.CodeExistsAsync(
                command.CompanyId,
                command.Code.Trim().ToUpperInvariant(),
                excludingOvertimeTypeId: null,
                cancellationToken))
        {
            return Result<OvertimeTypeResponse>.Failure(OvertimeTypeErrors.CodeTaken);
        }

        var type = OvertimeType.Create(
            command.Code,
            command.Name,
            command.DefaultFactor,
            command.PayrollEffectDescription,
            command.SortOrder);
        type.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(type);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Overtime type response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OvertimeTypeCreated,
                    AuditEntityTypes.OvertimeType,
                    type.PublicId,
                    type.Code,
                    AuditActions.Create,
                    $"Created overtime type {type.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OvertimeTypeResponse>.Success(response);
        }
        catch (UniqueConstraintViolationException ex) when (OvertimeTypeConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<OvertimeTypeResponse>.Failure(OvertimeTypeErrors.CodeTaken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateOvertimeTypeCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IOvertimeTypeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateOvertimeTypeCommand, OvertimeTypeResponse>
{
    public async Task<Result<OvertimeTypeResponse>> Handle(
        UpdateOvertimeTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<OvertimeTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageOvertimeRecordsAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OvertimeTypeResponse>.Failure(authorizationResult.Error);
        }

        var type = await repository.GetByIdAsync(command.OvertimeTypeId, cancellationToken);
        if (type is null)
        {
            return Result<OvertimeTypeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.OvertimeTypeId, cancellationToken)
                    ? OvertimeTypeErrors.TenantMismatch(RbacPermissionAction.Update)
                    : OvertimeTypeErrors.OvertimeTypeNotFound);
        }

        if (type.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OvertimeTypeResponse>.Failure(OvertimeTypeErrors.ConcurrencyConflict);
        }

        if (await repository.CodeExistsAsync(
                type.TenantId,
                command.Code.Trim().ToUpperInvariant(),
                type.PublicId,
                cancellationToken))
        {
            return Result<OvertimeTypeResponse>.Failure(OvertimeTypeErrors.CodeTaken);
        }

        var before = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Overtime type response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            type.Update(
                command.Code,
                command.Name,
                command.DefaultFactor,
                command.PayrollEffectDescription,
                command.SortOrder);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Overtime type response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OvertimeTypeUpdated,
                    AuditEntityTypes.OvertimeType,
                    type.PublicId,
                    type.Code,
                    AuditActions.Update,
                    $"Updated overtime type {type.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OvertimeTypeResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException ex) when (OvertimeTypeConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<OvertimeTypeResponse>.Failure(OvertimeTypeErrors.CodeTaken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateOvertimeTypeCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IOvertimeTypeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateOvertimeTypeCommand, OvertimeTypeResponse>
{
    public async Task<Result<OvertimeTypeResponse>> Handle(
        ActivateOvertimeTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<OvertimeTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageOvertimeRecordsAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OvertimeTypeResponse>.Failure(authorizationResult.Error);
        }

        var type = await repository.GetByIdAsync(command.OvertimeTypeId, cancellationToken);
        if (type is null)
        {
            return Result<OvertimeTypeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.OvertimeTypeId, cancellationToken)
                    ? OvertimeTypeErrors.TenantMismatch(RbacPermissionAction.Update)
                    : OvertimeTypeErrors.OvertimeTypeNotFound);
        }

        if (type.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OvertimeTypeResponse>.Failure(OvertimeTypeErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Overtime type response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            type.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Overtime type response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OvertimeTypeActivated,
                    AuditEntityTypes.OvertimeType,
                    type.PublicId,
                    type.Code,
                    AuditActions.Reactivate,
                    $"Activated overtime type {type.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OvertimeTypeResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException ex) when (OvertimeTypeConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            // The filtered (tenant, normalized_code) WHERE is_active unique index means reactivating a type
            // whose code is already taken by an active type trips the index — map to a clean 409.
            await transaction.RollbackAsync(cancellationToken);
            return Result<OvertimeTypeResponse>.Failure(OvertimeTypeErrors.CodeTaken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateOvertimeTypeCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IOvertimeTypeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateOvertimeTypeCommand, OvertimeTypeResponse>
{
    public async Task<Result<OvertimeTypeResponse>> Handle(
        InactivateOvertimeTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<OvertimeTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageOvertimeRecordsAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OvertimeTypeResponse>.Failure(authorizationResult.Error);
        }

        var type = await repository.GetByIdAsync(command.OvertimeTypeId, cancellationToken);
        if (type is null)
        {
            return Result<OvertimeTypeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.OvertimeTypeId, cancellationToken)
                    ? OvertimeTypeErrors.TenantMismatch(RbacPermissionAction.Update)
                    : OvertimeTypeErrors.OvertimeTypeNotFound);
        }

        if (type.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OvertimeTypeResponse>.Failure(OvertimeTypeErrors.ConcurrencyConflict);
        }

        // Logical-delete usage guard (OVERTIME_TYPE_IN_USE). PR-1 has no overtime record table yet
        // (M2/PR-2), so IsInUseAsync is a stub returning false today; the real reference check is wired in
        // PR-3 once that table exists.
        if (await repository.IsInUseAsync(type.TenantId, type.PublicId, cancellationToken))
        {
            return Result<OvertimeTypeResponse>.Failure(OvertimeTypeErrors.InUse);
        }

        var before = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Overtime type response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            type.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Overtime type response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OvertimeTypeInactivated,
                    AuditEntityTypes.OvertimeType,
                    type.PublicId,
                    type.Code,
                    AuditActions.Deactivate,
                    $"Inactivated overtime type {type.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OvertimeTypeResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class OvertimeTypeConstraintViolations
{
    // The filtered (TenantId, NormalizedCode) WHERE is_active unique index is the real guard against
    // duplicate active codes; the up-front CodeExistsAsync probe only closes the common (sequential) case.
    // On a concurrent create/update/activate of the same code, the second writer trips this index — map it
    // to the same clean 409 as the probe.
    public static bool IsCodeConflict(string? constraintName) =>
        string.Equals(constraintName, OvertimeMasterConstraintNames.OvertimeTypeCodeUnique, StringComparison.Ordinal);
}

internal static class OvertimeTypePolicyAdapter
{
    public static OvertimeTypeListItemResponse ApplyAllowedActions(
        OvertimeTypeListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage) =>
        response with { AllowedActions = Evaluate(response.IsActive, resourceActionPolicyService, canManage) };

    public static OvertimeTypeResponse ApplyAllowedActions(
        OvertimeTypeResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage) =>
        response with { AllowedActions = Evaluate(response.IsActive, resourceActionPolicyService, canManage) };

    private static AllowedActionsResponse Evaluate(
        bool isActive,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage) =>
        resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                OvertimeConfigurationResourceKeys.OvertimeTypes,
                State: null,
                isActive,
                SupportsEdit: true,
                EditAllowed: canManage,
                SupportsDelete: false,
                SupportsArchive: false,
                SupportsActivate: true,
                ActivateAllowed: canManage,
                SupportsInactivate: true,
                InactivateAllowed: canManage));
}
