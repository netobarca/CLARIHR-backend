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

internal sealed class SearchRecognitionTypesQueryHandler(
    IEmployeeRelationsConfigurationAuthorizationService authorizationService,
    IRecognitionTypeRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchRecognitionTypesQuery, PagedResponse<RecognitionTypeListItemResponse>>
{
    public async Task<Result<PagedResponse<RecognitionTypeListItemResponse>>> Handle(
        SearchRecognitionTypesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<RecognitionTypeListItemResponse>>.Failure(authorizationResult.Error);
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
            return Result<PagedResponse<RecognitionTypeListItemResponse>>.Success(response);
        }

        var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = response.Items
            .Select(item => RecognitionTypePolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManage))
            .ToArray();
        response = response with { Items = items };

        return Result<PagedResponse<RecognitionTypeListItemResponse>>.Success(response);
    }
}

internal sealed class GetRecognitionTypeByIdQueryHandler(
    IEmployeeRelationsConfigurationAuthorizationService authorizationService,
    IRecognitionTypeRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetRecognitionTypeByIdQuery, RecognitionTypeResponse>
{
    public async Task<Result<RecognitionTypeResponse>> Handle(
        GetRecognitionTypeByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<RecognitionTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<RecognitionTypeResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetResponseByIdAsync(query.RecognitionTypeId, cancellationToken);
        if (response is not null)
        {
            var canManage = (await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = RecognitionTypePolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManage);

            return Result<RecognitionTypeResponse>.Success(response);
        }

        return Result<RecognitionTypeResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.RecognitionTypeId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : RecognitionTypeErrors.RecognitionTypeNotFound);
    }
}

internal sealed class CreateRecognitionTypeCommandHandler(
    IEmployeeRelationsConfigurationAuthorizationService authorizationService,
    IRecognitionTypeRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateRecognitionTypeCommand, RecognitionTypeResponse>
{
    public async Task<Result<RecognitionTypeResponse>> Handle(
        CreateRecognitionTypeCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<RecognitionTypeResponse>.Failure(authorizationResult.Error);
        }

        if (await repository.CodeExistsAsync(
                command.CompanyId,
                command.Code.Trim().ToUpperInvariant(),
                excludingRecognitionTypeId: null,
                cancellationToken))
        {
            return Result<RecognitionTypeResponse>.Failure(RecognitionTypeErrors.CodeConflict);
        }

        var type = RecognitionType.Create(command.Code, command.Name, command.SortOrder);
        type.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(type);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Recognition type response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.RecognitionTypeCreated,
                    AuditEntityTypes.RecognitionType,
                    type.PublicId,
                    type.Code,
                    AuditActions.Create,
                    $"Created recognition type {type.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<RecognitionTypeResponse>.Success(response);
        }
        catch (UniqueConstraintViolationException ex) when (RecognitionTypeConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<RecognitionTypeResponse>.Failure(RecognitionTypeErrors.CodeConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateRecognitionTypeCommandHandler(
    IEmployeeRelationsConfigurationAuthorizationService authorizationService,
    IRecognitionTypeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateRecognitionTypeCommand, RecognitionTypeResponse>
{
    public async Task<Result<RecognitionTypeResponse>> Handle(
        UpdateRecognitionTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<RecognitionTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<RecognitionTypeResponse>.Failure(authorizationResult.Error);
        }

        var type = await repository.GetByIdAsync(command.RecognitionTypeId, cancellationToken);
        if (type is null)
        {
            return Result<RecognitionTypeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.RecognitionTypeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : RecognitionTypeErrors.RecognitionTypeNotFound);
        }

        if (type.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<RecognitionTypeResponse>.Failure(EmployeeRelationsConfigurationErrors.ConcurrencyConflict);
        }

        if (await repository.CodeExistsAsync(
                type.TenantId,
                command.Code.Trim().ToUpperInvariant(),
                type.PublicId,
                cancellationToken))
        {
            return Result<RecognitionTypeResponse>.Failure(RecognitionTypeErrors.CodeConflict);
        }

        var before = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Recognition type response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            type.Update(command.Code, command.Name, command.SortOrder);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Recognition type response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.RecognitionTypeUpdated,
                    AuditEntityTypes.RecognitionType,
                    type.PublicId,
                    type.Code,
                    AuditActions.Update,
                    $"Updated recognition type {type.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<RecognitionTypeResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException ex) when (RecognitionTypeConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<RecognitionTypeResponse>.Failure(RecognitionTypeErrors.CodeConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateRecognitionTypeCommandHandler(
    IEmployeeRelationsConfigurationAuthorizationService authorizationService,
    IRecognitionTypeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateRecognitionTypeCommand, RecognitionTypeResponse>
{
    public async Task<Result<RecognitionTypeResponse>> Handle(
        ActivateRecognitionTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<RecognitionTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<RecognitionTypeResponse>.Failure(authorizationResult.Error);
        }

        var type = await repository.GetByIdAsync(command.RecognitionTypeId, cancellationToken);
        if (type is null)
        {
            return Result<RecognitionTypeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.RecognitionTypeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : RecognitionTypeErrors.RecognitionTypeNotFound);
        }

        if (type.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<RecognitionTypeResponse>.Failure(EmployeeRelationsConfigurationErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Recognition type response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            type.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Recognition type response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.RecognitionTypeActivated,
                    AuditEntityTypes.RecognitionType,
                    type.PublicId,
                    type.Code,
                    AuditActions.Reactivate,
                    $"Activated recognition type {type.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<RecognitionTypeResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException ex) when (RecognitionTypeConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            // The filtered (tenant, normalized_code) WHERE is_active unique index means reactivating a
            // type whose code is already taken by an active type trips the index — map to a clean 409.
            await transaction.RollbackAsync(cancellationToken);
            return Result<RecognitionTypeResponse>.Failure(RecognitionTypeErrors.CodeConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateRecognitionTypeCommandHandler(
    IEmployeeRelationsConfigurationAuthorizationService authorizationService,
    IRecognitionTypeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateRecognitionTypeCommand, RecognitionTypeResponse>
{
    public async Task<Result<RecognitionTypeResponse>> Handle(
        InactivateRecognitionTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<RecognitionTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<RecognitionTypeResponse>.Failure(authorizationResult.Error);
        }

        var type = await repository.GetByIdAsync(command.RecognitionTypeId, cancellationToken);
        if (type is null)
        {
            return Result<RecognitionTypeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.RecognitionTypeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : RecognitionTypeErrors.RecognitionTypeNotFound);
        }

        if (type.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<RecognitionTypeResponse>.Failure(EmployeeRelationsConfigurationErrors.ConcurrencyConflict);
        }

        // Logical-delete usage guard (RECOGNITION_TYPE_IN_USE). PR-1 has no recognition table yet
        // (M2/PR-2), so IsInUseAsync is a stub returning false today; the real reference check is wired
        // in PR-3 once that table exists.
        if (await repository.IsInUseAsync(type.TenantId, type.PublicId, cancellationToken))
        {
            return Result<RecognitionTypeResponse>.Failure(RecognitionTypeErrors.InUse);
        }

        var before = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Recognition type response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            type.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Recognition type response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.RecognitionTypeInactivated,
                    AuditEntityTypes.RecognitionType,
                    type.PublicId,
                    type.Code,
                    AuditActions.Deactivate,
                    $"Inactivated recognition type {type.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<RecognitionTypeResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class RecognitionTypeConstraintViolations
{
    // The filtered (TenantId, NormalizedCode) WHERE is_active unique index is the real guard against
    // duplicate active codes; the up-front CodeExistsAsync probe only closes the common (sequential)
    // case. On a concurrent create/update/activate of the same code, the second writer trips this
    // index — map it to the same clean 409 as the probe.
    public static bool IsCodeConflict(string? constraintName) =>
        string.Equals(constraintName, EmployeeRelationsMasterConstraintNames.RecognitionTypeCodeUnique, StringComparison.Ordinal);
}

internal static class RecognitionTypePolicyAdapter
{
    public static RecognitionTypeListItemResponse ApplyAllowedActions(
        RecognitionTypeListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage) =>
        response with { AllowedActions = Evaluate(response.IsActive, resourceActionPolicyService, canManage) };

    public static RecognitionTypeResponse ApplyAllowedActions(
        RecognitionTypeResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage) =>
        response with { AllowedActions = Evaluate(response.IsActive, resourceActionPolicyService, canManage) };

    private static AllowedActionsResponse Evaluate(
        bool isActive,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage) =>
        resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                EmployeeRelationsConfigurationPermissionCodes.RecognitionTypesResourceKey,
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
