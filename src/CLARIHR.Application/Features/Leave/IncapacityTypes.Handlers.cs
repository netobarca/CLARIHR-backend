using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Leave;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Leave.Common;
using CLARIHR.Domain.Leave;

namespace CLARIHR.Application.Features.Leave;

internal sealed class SearchIncapacityTypesQueryHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IIncapacityTypeRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchIncapacityTypesQuery, PagedResponse<IncapacityTypeListItemResponse>>
{
    public async Task<Result<PagedResponse<IncapacityTypeListItemResponse>>> Handle(
        SearchIncapacityTypesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<IncapacityTypeListItemResponse>>.Failure(authorizationResult.Error);
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
            return Result<PagedResponse<IncapacityTypeListItemResponse>>.Success(response);
        }

        var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = response.Items
            .Select(item => IncapacityTypePolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManage))
            .ToArray();
        response = response with { Items = items };

        return Result<PagedResponse<IncapacityTypeListItemResponse>>.Success(response);
    }
}

internal sealed class GetIncapacityTypeByIdQueryHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IIncapacityTypeRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetIncapacityTypeByIdQuery, IncapacityTypeResponse>
{
    public async Task<Result<IncapacityTypeResponse>> Handle(
        GetIncapacityTypeByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<IncapacityTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IncapacityTypeResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetResponseByIdAsync(query.IncapacityTypeId, cancellationToken);
        if (response is not null)
        {
            var canManage = (await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = IncapacityTypePolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManage);

            return Result<IncapacityTypeResponse>.Success(response);
        }

        return Result<IncapacityTypeResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.IncapacityTypeId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : IncapacityTypeErrors.IncapacityTypeNotFound);
    }
}

internal sealed class CreateIncapacityTypeCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IIncapacityTypeRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateIncapacityTypeCommand, IncapacityTypeResponse>
{
    public async Task<Result<IncapacityTypeResponse>> Handle(
        CreateIncapacityTypeCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IncapacityTypeResponse>.Failure(authorizationResult.Error);
        }

        if (await repository.CodeExistsAsync(
                command.CompanyId,
                command.Code.Trim().ToUpperInvariant(),
                excludingIncapacityTypeId: null,
                cancellationToken))
        {
            return Result<IncapacityTypeResponse>.Failure(IncapacityTypeErrors.CodeConflict);
        }

        var incapacityType = IncapacityType.Create(
            command.Code,
            command.Name,
            command.DeductionTypeText,
            command.IncomeTypeText,
            command.AppliesToWorkAccident);
        incapacityType.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(incapacityType);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetResponseByIdAsync(incapacityType.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Incapacity type response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.IncapacityTypeCreated,
                    AuditEntityTypes.IncapacityType,
                    incapacityType.PublicId,
                    incapacityType.Code,
                    AuditActions.Create,
                    $"Created incapacity type {incapacityType.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<IncapacityTypeResponse>.Success(response);
        }
        catch (UniqueConstraintViolationException ex) when (IncapacityTypeConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<IncapacityTypeResponse>.Failure(IncapacityTypeErrors.CodeConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateIncapacityTypeCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IIncapacityTypeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateIncapacityTypeCommand, IncapacityTypeResponse>
{
    public async Task<Result<IncapacityTypeResponse>> Handle(
        UpdateIncapacityTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<IncapacityTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IncapacityTypeResponse>.Failure(authorizationResult.Error);
        }

        var incapacityType = await repository.GetByIdAsync(command.IncapacityTypeId, cancellationToken);
        if (incapacityType is null)
        {
            return Result<IncapacityTypeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.IncapacityTypeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : IncapacityTypeErrors.IncapacityTypeNotFound);
        }

        if (incapacityType.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<IncapacityTypeResponse>.Failure(LeaveConfigurationErrors.ConcurrencyConflict);
        }

        if (await repository.CodeExistsAsync(
                incapacityType.TenantId,
                command.Code.Trim().ToUpperInvariant(),
                incapacityType.PublicId,
                cancellationToken))
        {
            return Result<IncapacityTypeResponse>.Failure(IncapacityTypeErrors.CodeConflict);
        }

        var before = await repository.GetResponseByIdAsync(incapacityType.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Incapacity type response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            incapacityType.Update(
                command.Code,
                command.Name,
                command.DeductionTypeText,
                command.IncomeTypeText,
                command.AppliesToWorkAccident);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(incapacityType.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Incapacity type response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.IncapacityTypeUpdated,
                    AuditEntityTypes.IncapacityType,
                    incapacityType.PublicId,
                    incapacityType.Code,
                    AuditActions.Update,
                    $"Updated incapacity type {incapacityType.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<IncapacityTypeResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException ex) when (IncapacityTypeConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<IncapacityTypeResponse>.Failure(IncapacityTypeErrors.CodeConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateIncapacityTypeCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IIncapacityTypeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateIncapacityTypeCommand, IncapacityTypeResponse>
{
    public async Task<Result<IncapacityTypeResponse>> Handle(
        ActivateIncapacityTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<IncapacityTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IncapacityTypeResponse>.Failure(authorizationResult.Error);
        }

        var incapacityType = await repository.GetByIdAsync(command.IncapacityTypeId, cancellationToken);
        if (incapacityType is null)
        {
            return Result<IncapacityTypeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.IncapacityTypeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : IncapacityTypeErrors.IncapacityTypeNotFound);
        }

        if (incapacityType.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<IncapacityTypeResponse>.Failure(LeaveConfigurationErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(incapacityType.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Incapacity type response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            incapacityType.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(incapacityType.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Incapacity type response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.IncapacityTypeActivated,
                    AuditEntityTypes.IncapacityType,
                    incapacityType.PublicId,
                    incapacityType.Code,
                    AuditActions.Reactivate,
                    $"Activated incapacity type {incapacityType.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<IncapacityTypeResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateIncapacityTypeCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IIncapacityTypeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateIncapacityTypeCommand, IncapacityTypeResponse>
{
    public async Task<Result<IncapacityTypeResponse>> Handle(
        InactivateIncapacityTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<IncapacityTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IncapacityTypeResponse>.Failure(authorizationResult.Error);
        }

        var incapacityType = await repository.GetByIdAsync(command.IncapacityTypeId, cancellationToken);
        if (incapacityType is null)
        {
            return Result<IncapacityTypeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.IncapacityTypeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : IncapacityTypeErrors.IncapacityTypeNotFound);
        }

        if (incapacityType.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<IncapacityTypeResponse>.Failure(LeaveConfigurationErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(incapacityType.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Incapacity type response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            incapacityType.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(incapacityType.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Incapacity type response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.IncapacityTypeInactivated,
                    AuditEntityTypes.IncapacityType,
                    incapacityType.PublicId,
                    incapacityType.Code,
                    AuditActions.Deactivate,
                    $"Inactivated incapacity type {incapacityType.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<IncapacityTypeResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class IncapacityTypeConstraintViolations
{
    // The (TenantId, NormalizedCode) unique index is the real guard against duplicate codes; the
    // up-front CodeExistsAsync probe only closes the common (sequential) case. On a concurrent
    // create/update of the same code, the second writer trips this index — map it to the same
    // clean 409 as the probe instead of letting the 23505 escape as an HTTP 500 (mirrors
    // CostCenterConstraintViolations).
    public static bool IsCodeConflict(string? constraintName) =>
        string.Equals(constraintName, LeaveMasterConstraintNames.IncapacityTypeCodeUnique, StringComparison.Ordinal);
}

internal static class IncapacityTypePolicyAdapter
{
    public static IncapacityTypeListItemResponse ApplyAllowedActions(
        IncapacityTypeListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                LeaveConfigurationPermissionCodes.IncapacityTypesResourceKey,
                response.Code,
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

    public static IncapacityTypeResponse ApplyAllowedActions(
        IncapacityTypeResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                LeaveConfigurationPermissionCodes.IncapacityTypesResourceKey,
                response.Code,
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
