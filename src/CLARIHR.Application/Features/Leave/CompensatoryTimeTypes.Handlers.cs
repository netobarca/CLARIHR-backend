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

internal sealed class SearchCompensatoryTimeTypesQueryHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    ICompensatoryTimeTypeRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchCompensatoryTimeTypesQuery, PagedResponse<CompensatoryTimeTypeListItemResponse>>
{
    public async Task<Result<PagedResponse<CompensatoryTimeTypeListItemResponse>>> Handle(
        SearchCompensatoryTimeTypesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<CompensatoryTimeTypeListItemResponse>>.Failure(authorizationResult.Error);
        }

        var response = await repository.SearchAsync(
            query.CompanyId,
            query.IsActive,
            query.OperationCode,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (!query.IncludeAllowedActions)
        {
            return Result<PagedResponse<CompensatoryTimeTypeListItemResponse>>.Success(response);
        }

        var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = response.Items
            .Select(item => CompensatoryTimeTypePolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManage))
            .ToArray();
        response = response with { Items = items };

        return Result<PagedResponse<CompensatoryTimeTypeListItemResponse>>.Success(response);
    }
}

internal sealed class GetCompensatoryTimeTypeByIdQueryHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    ICompensatoryTimeTypeRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetCompensatoryTimeTypeByIdQuery, CompensatoryTimeTypeResponse>
{
    public async Task<Result<CompensatoryTimeTypeResponse>> Handle(
        GetCompensatoryTimeTypeByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CompensatoryTimeTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompensatoryTimeTypeResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetResponseByIdAsync(query.CompensatoryTimeTypeId, cancellationToken);
        if (response is not null)
        {
            var canManage = (await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = CompensatoryTimeTypePolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManage);

            return Result<CompensatoryTimeTypeResponse>.Success(response);
        }

        return Result<CompensatoryTimeTypeResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.CompensatoryTimeTypeId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : CompensatoryTimeTypeErrors.CompensatoryTimeTypeNotFound);
    }
}

internal sealed class CreateCompensatoryTimeTypeCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    ICompensatoryTimeTypeRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateCompensatoryTimeTypeCommand, CompensatoryTimeTypeResponse>
{
    public async Task<Result<CompensatoryTimeTypeResponse>> Handle(
        CreateCompensatoryTimeTypeCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompensatoryTimeTypeResponse>.Failure(authorizationResult.Error);
        }

        if (await repository.CodeExistsAsync(
                command.CompanyId,
                command.Code.Trim().ToUpperInvariant(),
                excludingCompensatoryTimeTypeId: null,
                cancellationToken))
        {
            return Result<CompensatoryTimeTypeResponse>.Failure(CompensatoryTimeTypeErrors.CodeConflict);
        }

        var type = CompensatoryTimeType.Create(
            command.Code,
            command.Name,
            command.OperationCode,
            command.CreditFactor,
            command.SortOrder);
        type.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(type);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Compensatory-time type response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompensatoryTimeTypeCreated,
                    AuditEntityTypes.CompensatoryTimeType,
                    type.PublicId,
                    type.Code,
                    AuditActions.Create,
                    $"Created compensatory-time type {type.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CompensatoryTimeTypeResponse>.Success(response);
        }
        catch (UniqueConstraintViolationException ex) when (CompensatoryTimeTypeConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CompensatoryTimeTypeResponse>.Failure(CompensatoryTimeTypeErrors.CodeConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateCompensatoryTimeTypeCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    ICompensatoryTimeTypeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateCompensatoryTimeTypeCommand, CompensatoryTimeTypeResponse>
{
    public async Task<Result<CompensatoryTimeTypeResponse>> Handle(
        UpdateCompensatoryTimeTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CompensatoryTimeTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompensatoryTimeTypeResponse>.Failure(authorizationResult.Error);
        }

        var type = await repository.GetByIdAsync(command.CompensatoryTimeTypeId, cancellationToken);
        if (type is null)
        {
            return Result<CompensatoryTimeTypeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.CompensatoryTimeTypeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CompensatoryTimeTypeErrors.CompensatoryTimeTypeNotFound);
        }

        if (type.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CompensatoryTimeTypeResponse>.Failure(LeaveConfigurationErrors.ConcurrencyConflict);
        }

        if (await repository.CodeExistsAsync(
                type.TenantId,
                command.Code.Trim().ToUpperInvariant(),
                type.PublicId,
                cancellationToken))
        {
            return Result<CompensatoryTimeTypeResponse>.Failure(CompensatoryTimeTypeErrors.CodeConflict);
        }

        var before = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Compensatory-time type response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            type.Update(
                command.Code,
                command.Name,
                command.OperationCode,
                command.CreditFactor,
                command.SortOrder);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Compensatory-time type response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompensatoryTimeTypeUpdated,
                    AuditEntityTypes.CompensatoryTimeType,
                    type.PublicId,
                    type.Code,
                    AuditActions.Update,
                    $"Updated compensatory-time type {type.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CompensatoryTimeTypeResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException ex) when (CompensatoryTimeTypeConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CompensatoryTimeTypeResponse>.Failure(CompensatoryTimeTypeErrors.CodeConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateCompensatoryTimeTypeCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    ICompensatoryTimeTypeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateCompensatoryTimeTypeCommand, CompensatoryTimeTypeResponse>
{
    public async Task<Result<CompensatoryTimeTypeResponse>> Handle(
        ActivateCompensatoryTimeTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CompensatoryTimeTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompensatoryTimeTypeResponse>.Failure(authorizationResult.Error);
        }

        var type = await repository.GetByIdAsync(command.CompensatoryTimeTypeId, cancellationToken);
        if (type is null)
        {
            return Result<CompensatoryTimeTypeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.CompensatoryTimeTypeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CompensatoryTimeTypeErrors.CompensatoryTimeTypeNotFound);
        }

        if (type.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CompensatoryTimeTypeResponse>.Failure(LeaveConfigurationErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Compensatory-time type response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            type.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Compensatory-time type response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompensatoryTimeTypeActivated,
                    AuditEntityTypes.CompensatoryTimeType,
                    type.PublicId,
                    type.Code,
                    AuditActions.Reactivate,
                    $"Activated compensatory-time type {type.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CompensatoryTimeTypeResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException ex) when (CompensatoryTimeTypeConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            // The filtered (tenant, normalized_code) WHERE is_active unique index means reactivating a
            // type whose code is already taken by an active type trips the index — map to a clean 409.
            await transaction.RollbackAsync(cancellationToken);
            return Result<CompensatoryTimeTypeResponse>.Failure(CompensatoryTimeTypeErrors.CodeConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateCompensatoryTimeTypeCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    ICompensatoryTimeTypeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateCompensatoryTimeTypeCommand, CompensatoryTimeTypeResponse>
{
    public async Task<Result<CompensatoryTimeTypeResponse>> Handle(
        InactivateCompensatoryTimeTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CompensatoryTimeTypeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompensatoryTimeTypeResponse>.Failure(authorizationResult.Error);
        }

        var type = await repository.GetByIdAsync(command.CompensatoryTimeTypeId, cancellationToken);
        if (type is null)
        {
            return Result<CompensatoryTimeTypeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.CompensatoryTimeTypeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CompensatoryTimeTypeErrors.CompensatoryTimeTypeNotFound);
        }

        if (type.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CompensatoryTimeTypeResponse>.Failure(LeaveConfigurationErrors.ConcurrencyConflict);
        }

        // Logical-delete usage guard (COMPENSATORY_TIME_TYPE_IN_USE). PR-1 has no credit/absence tables
        // yet (M2/PR-2), so IsInUseAsync is a stub returning false today; the real reference check is
        // wired in PR-3/PR-4 once those tables exist.
        if (await repository.IsInUseAsync(type.TenantId, type.PublicId, cancellationToken))
        {
            return Result<CompensatoryTimeTypeResponse>.Failure(CompensatoryTimeTypeErrors.InUse);
        }

        var before = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Compensatory-time type response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            type.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(type.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Compensatory-time type response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompensatoryTimeTypeInactivated,
                    AuditEntityTypes.CompensatoryTimeType,
                    type.PublicId,
                    type.Code,
                    AuditActions.Deactivate,
                    $"Inactivated compensatory-time type {type.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CompensatoryTimeTypeResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class CompensatoryTimeTypeConstraintViolations
{
    // The filtered (TenantId, NormalizedCode) WHERE is_active unique index is the real guard against
    // duplicate active codes; the up-front CodeExistsAsync probe only closes the common (sequential)
    // case. On a concurrent create/update/activate of the same code, the second writer trips this
    // index — map it to the same clean 409 as the probe (mirrors MedicalClinicConstraintViolations).
    public static bool IsCodeConflict(string? constraintName) =>
        string.Equals(constraintName, LeaveMasterConstraintNames.CompensatoryTimeTypeCodeUnique, StringComparison.Ordinal);
}

internal static class CompensatoryTimeTypePolicyAdapter
{
    public static CompensatoryTimeTypeListItemResponse ApplyAllowedActions(
        CompensatoryTimeTypeListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                LeaveConfigurationPermissionCodes.CompensatoryTimeTypesResourceKey,
                response.OperationCode,
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

    public static CompensatoryTimeTypeResponse ApplyAllowedActions(
        CompensatoryTimeTypeResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                LeaveConfigurationPermissionCodes.CompensatoryTimeTypesResourceKey,
                response.OperationCode,
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
