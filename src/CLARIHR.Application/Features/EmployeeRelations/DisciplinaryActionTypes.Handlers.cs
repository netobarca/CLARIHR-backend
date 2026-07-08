using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.EmployeeRelations;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.EmployeeRelations.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.EmployeeRelations;

namespace CLARIHR.Application.Features.EmployeeRelations;

internal sealed class SearchDisciplinaryActionTypesQueryHandler(
    IEmployeeRelationsConfigurationAuthorizationService authorizationService,
    IDisciplinaryActionTypeRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchDisciplinaryActionTypesQuery, PagedResponse<DisciplinaryActionTypeListItemResponse>>
{
    public async Task<Result<PagedResponse<DisciplinaryActionTypeListItemResponse>>> Handle(
        SearchDisciplinaryActionTypesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<DisciplinaryActionTypeListItemResponse>>.Failure(authorizationResult.Error);
        }

        var response = await repository.SearchAsync(
            query.CompanyId,
            query.IsActive,
            query.AppliesSuspension,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (!query.IncludeAllowedActions)
        {
            return Result<PagedResponse<DisciplinaryActionTypeListItemResponse>>.Success(response);
        }

        var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = response.Items
            .Select(item => DisciplinaryActionTypePolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManage))
            .ToArray();
        response = response with { Items = items };

        return Result<PagedResponse<DisciplinaryActionTypeListItemResponse>>.Success(response);
    }
}

internal sealed class GetDisciplinaryActionTypeByIdQueryHandler(
    IEmployeeRelationsConfigurationAuthorizationService authorizationService,
    IDisciplinaryActionTypeRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetDisciplinaryActionTypeByIdQuery, DisciplinaryActionTypeResponse>
{
    public async Task<Result<DisciplinaryActionTypeResponse>> Handle(
        GetDisciplinaryActionTypeByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<DisciplinaryActionTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<DisciplinaryActionTypeResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetResponseByIdAsync(query.DisciplinaryActionTypeId, cancellationToken);
        if (response is not null)
        {
            var canManage = (await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = DisciplinaryActionTypePolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManage);

            return Result<DisciplinaryActionTypeResponse>.Success(response);
        }

        return Result<DisciplinaryActionTypeResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.DisciplinaryActionTypeId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : DisciplinaryActionTypeErrors.DisciplinaryActionTypeNotFound);
    }
}

internal sealed class CreateDisciplinaryActionTypeCommandHandler(
    IEmployeeRelationsConfigurationAuthorizationService authorizationService,
    IDisciplinaryActionTypeRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateDisciplinaryActionTypeCommand, DisciplinaryActionTypeResponse>
{
    public async Task<Result<DisciplinaryActionTypeResponse>> Handle(
        CreateDisciplinaryActionTypeCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<DisciplinaryActionTypeResponse>.Failure(authorizationResult.Error);
        }

        if (await repository.CodeExistsAsync(
                command.CompanyId,
                command.Code.Trim().ToUpperInvariant(),
                excludingDisciplinaryActionTypeId: null,
                cancellationToken))
        {
            return Result<DisciplinaryActionTypeResponse>.Failure(DisciplinaryActionTypeErrors.CodeConflict);
        }

        var type = DisciplinaryActionType.Create(command.Code, command.Name, command.AppliesSuspension, command.SortOrder);
        type.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(type);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Disciplinary-action type response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.DisciplinaryActionTypeCreated,
                    AuditEntityTypes.DisciplinaryActionType,
                    type.PublicId,
                    type.Code,
                    AuditActions.Create,
                    $"Created disciplinary-action type {type.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<DisciplinaryActionTypeResponse>.Success(response);
        }
        catch (UniqueConstraintViolationException ex) when (DisciplinaryActionTypeConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<DisciplinaryActionTypeResponse>.Failure(DisciplinaryActionTypeErrors.CodeConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateDisciplinaryActionTypeCommandHandler(
    IEmployeeRelationsConfigurationAuthorizationService authorizationService,
    IDisciplinaryActionTypeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateDisciplinaryActionTypeCommand, DisciplinaryActionTypeResponse>
{
    public async Task<Result<DisciplinaryActionTypeResponse>> Handle(
        UpdateDisciplinaryActionTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<DisciplinaryActionTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<DisciplinaryActionTypeResponse>.Failure(authorizationResult.Error);
        }

        var type = await repository.GetByIdAsync(command.DisciplinaryActionTypeId, cancellationToken);
        if (type is null)
        {
            return Result<DisciplinaryActionTypeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.DisciplinaryActionTypeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : DisciplinaryActionTypeErrors.DisciplinaryActionTypeNotFound);
        }

        if (type.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<DisciplinaryActionTypeResponse>.Failure(EmployeeRelationsConfigurationErrors.ConcurrencyConflict);
        }

        if (await repository.CodeExistsAsync(
                type.TenantId,
                command.Code.Trim().ToUpperInvariant(),
                type.PublicId,
                cancellationToken))
        {
            return Result<DisciplinaryActionTypeResponse>.Failure(DisciplinaryActionTypeErrors.CodeConflict);
        }

        var before = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Disciplinary-action type response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            type.Update(command.Code, command.Name, command.AppliesSuspension, command.SortOrder);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Disciplinary-action type response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.DisciplinaryActionTypeUpdated,
                    AuditEntityTypes.DisciplinaryActionType,
                    type.PublicId,
                    type.Code,
                    AuditActions.Update,
                    $"Updated disciplinary-action type {type.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<DisciplinaryActionTypeResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException ex) when (DisciplinaryActionTypeConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<DisciplinaryActionTypeResponse>.Failure(DisciplinaryActionTypeErrors.CodeConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateDisciplinaryActionTypeCommandHandler(
    IEmployeeRelationsConfigurationAuthorizationService authorizationService,
    IDisciplinaryActionTypeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateDisciplinaryActionTypeCommand, DisciplinaryActionTypeResponse>
{
    public async Task<Result<DisciplinaryActionTypeResponse>> Handle(
        ActivateDisciplinaryActionTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<DisciplinaryActionTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<DisciplinaryActionTypeResponse>.Failure(authorizationResult.Error);
        }

        var type = await repository.GetByIdAsync(command.DisciplinaryActionTypeId, cancellationToken);
        if (type is null)
        {
            return Result<DisciplinaryActionTypeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.DisciplinaryActionTypeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : DisciplinaryActionTypeErrors.DisciplinaryActionTypeNotFound);
        }

        if (type.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<DisciplinaryActionTypeResponse>.Failure(EmployeeRelationsConfigurationErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Disciplinary-action type response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            type.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Disciplinary-action type response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.DisciplinaryActionTypeActivated,
                    AuditEntityTypes.DisciplinaryActionType,
                    type.PublicId,
                    type.Code,
                    AuditActions.Reactivate,
                    $"Activated disciplinary-action type {type.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<DisciplinaryActionTypeResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException ex) when (DisciplinaryActionTypeConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            // The filtered (tenant, normalized_code) WHERE is_active unique index means reactivating a
            // type whose code is already taken by an active type trips the index — map to a clean 409.
            await transaction.RollbackAsync(cancellationToken);
            return Result<DisciplinaryActionTypeResponse>.Failure(DisciplinaryActionTypeErrors.CodeConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateDisciplinaryActionTypeCommandHandler(
    IEmployeeRelationsConfigurationAuthorizationService authorizationService,
    IDisciplinaryActionTypeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateDisciplinaryActionTypeCommand, DisciplinaryActionTypeResponse>
{
    public async Task<Result<DisciplinaryActionTypeResponse>> Handle(
        InactivateDisciplinaryActionTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<DisciplinaryActionTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<DisciplinaryActionTypeResponse>.Failure(authorizationResult.Error);
        }

        var type = await repository.GetByIdAsync(command.DisciplinaryActionTypeId, cancellationToken);
        if (type is null)
        {
            return Result<DisciplinaryActionTypeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.DisciplinaryActionTypeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : DisciplinaryActionTypeErrors.DisciplinaryActionTypeNotFound);
        }

        if (type.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<DisciplinaryActionTypeResponse>.Failure(EmployeeRelationsConfigurationErrors.ConcurrencyConflict);
        }

        // Logical-delete usage guard (DISCIPLINARY_ACTION_TYPE_IN_USE). PR-1 has no disciplinary-action
        // table yet (M2/PR-2), so IsInUseAsync is a stub returning false today; the real reference check
        // is wired in PR-4 once that table exists.
        if (await repository.IsInUseAsync(type.TenantId, type.PublicId, cancellationToken))
        {
            return Result<DisciplinaryActionTypeResponse>.Failure(DisciplinaryActionTypeErrors.InUse);
        }

        var before = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Disciplinary-action type response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            type.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Disciplinary-action type response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.DisciplinaryActionTypeInactivated,
                    AuditEntityTypes.DisciplinaryActionType,
                    type.PublicId,
                    type.Code,
                    AuditActions.Deactivate,
                    $"Inactivated disciplinary-action type {type.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<DisciplinaryActionTypeResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class DisciplinaryActionTypeConstraintViolations
{
    public static bool IsCodeConflict(string? constraintName) =>
        string.Equals(constraintName, EmployeeRelationsMasterConstraintNames.DisciplinaryActionTypeCodeUnique, StringComparison.Ordinal);
}

internal static class DisciplinaryActionTypePolicyAdapter
{
    public static DisciplinaryActionTypeListItemResponse ApplyAllowedActions(
        DisciplinaryActionTypeListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage) =>
        response with { AllowedActions = Evaluate(response.IsActive, resourceActionPolicyService, canManage) };

    public static DisciplinaryActionTypeResponse ApplyAllowedActions(
        DisciplinaryActionTypeResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage) =>
        response with { AllowedActions = Evaluate(response.IsActive, resourceActionPolicyService, canManage) };

    private static AllowedActionsResponse Evaluate(
        bool isActive,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage) =>
        resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                EmployeeRelationsConfigurationPermissionCodes.DisciplinaryActionTypesResourceKey,
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
