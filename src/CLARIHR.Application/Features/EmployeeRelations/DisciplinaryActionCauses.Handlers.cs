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

internal sealed class SearchDisciplinaryActionCausesQueryHandler(
    IEmployeeRelationsConfigurationAuthorizationService authorizationService,
    IDisciplinaryActionCauseRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchDisciplinaryActionCausesQuery, PagedResponse<DisciplinaryActionCauseListItemResponse>>
{
    public async Task<Result<PagedResponse<DisciplinaryActionCauseListItemResponse>>> Handle(
        SearchDisciplinaryActionCausesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<DisciplinaryActionCauseListItemResponse>>.Failure(authorizationResult.Error);
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
            return Result<PagedResponse<DisciplinaryActionCauseListItemResponse>>.Success(response);
        }

        var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = response.Items
            .Select(item => DisciplinaryActionCausePolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManage))
            .ToArray();
        response = response with { Items = items };

        return Result<PagedResponse<DisciplinaryActionCauseListItemResponse>>.Success(response);
    }
}

internal sealed class GetDisciplinaryActionCauseByIdQueryHandler(
    IEmployeeRelationsConfigurationAuthorizationService authorizationService,
    IDisciplinaryActionCauseRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetDisciplinaryActionCauseByIdQuery, DisciplinaryActionCauseResponse>
{
    public async Task<Result<DisciplinaryActionCauseResponse>> Handle(
        GetDisciplinaryActionCauseByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<DisciplinaryActionCauseResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<DisciplinaryActionCauseResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetResponseByIdAsync(query.DisciplinaryActionCauseId, cancellationToken);
        if (response is not null)
        {
            var canManage = (await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = DisciplinaryActionCausePolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManage);

            return Result<DisciplinaryActionCauseResponse>.Success(response);
        }

        return Result<DisciplinaryActionCauseResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.DisciplinaryActionCauseId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : DisciplinaryActionCauseErrors.DisciplinaryActionCauseNotFound);
    }
}

internal sealed class CreateDisciplinaryActionCauseCommandHandler(
    IEmployeeRelationsConfigurationAuthorizationService authorizationService,
    IDisciplinaryActionCauseRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateDisciplinaryActionCauseCommand, DisciplinaryActionCauseResponse>
{
    public async Task<Result<DisciplinaryActionCauseResponse>> Handle(
        CreateDisciplinaryActionCauseCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<DisciplinaryActionCauseResponse>.Failure(authorizationResult.Error);
        }

        var normalizedConcept = NormalizeConcept(command.DeductionConceptTypeCode);
        if (normalizedConcept is not null &&
            !await repository.IsDeductionConceptValidAsync(command.CompanyId, normalizedConcept, cancellationToken))
        {
            return Result<DisciplinaryActionCauseResponse>.Failure(EmployeeRelationsConfigurationErrors.DeductionConceptInvalid);
        }

        if (await repository.CodeExistsAsync(
                command.CompanyId,
                command.Code.Trim().ToUpperInvariant(),
                excludingDisciplinaryActionCauseId: null,
                cancellationToken))
        {
            return Result<DisciplinaryActionCauseResponse>.Failure(DisciplinaryActionCauseErrors.CodeConflict);
        }

        var cause = DisciplinaryActionCause.Create(command.Code, command.Name, command.DeductionConceptTypeCode, command.SortOrder);
        cause.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(cause);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetResponseByIdAsync(cause.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Disciplinary-action cause response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.DisciplinaryActionCauseCreated,
                    AuditEntityTypes.DisciplinaryActionCause,
                    cause.PublicId,
                    cause.Code,
                    AuditActions.Create,
                    $"Created disciplinary-action cause {cause.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<DisciplinaryActionCauseResponse>.Success(response);
        }
        catch (UniqueConstraintViolationException ex) when (DisciplinaryActionCauseConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<DisciplinaryActionCauseResponse>.Failure(DisciplinaryActionCauseErrors.CodeConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static string? NormalizeConcept(string? deductionConceptTypeCode) =>
        string.IsNullOrWhiteSpace(deductionConceptTypeCode) ? null : deductionConceptTypeCode.Trim().ToUpperInvariant();
}

internal sealed class UpdateDisciplinaryActionCauseCommandHandler(
    IEmployeeRelationsConfigurationAuthorizationService authorizationService,
    IDisciplinaryActionCauseRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateDisciplinaryActionCauseCommand, DisciplinaryActionCauseResponse>
{
    public async Task<Result<DisciplinaryActionCauseResponse>> Handle(
        UpdateDisciplinaryActionCauseCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<DisciplinaryActionCauseResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<DisciplinaryActionCauseResponse>.Failure(authorizationResult.Error);
        }

        var cause = await repository.GetByIdAsync(command.DisciplinaryActionCauseId, cancellationToken);
        if (cause is null)
        {
            return Result<DisciplinaryActionCauseResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.DisciplinaryActionCauseId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : DisciplinaryActionCauseErrors.DisciplinaryActionCauseNotFound);
        }

        if (cause.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<DisciplinaryActionCauseResponse>.Failure(EmployeeRelationsConfigurationErrors.ConcurrencyConflict);
        }

        var normalizedConcept = string.IsNullOrWhiteSpace(command.DeductionConceptTypeCode)
            ? null
            : command.DeductionConceptTypeCode.Trim().ToUpperInvariant();
        if (normalizedConcept is not null &&
            !await repository.IsDeductionConceptValidAsync(cause.TenantId, normalizedConcept, cancellationToken))
        {
            return Result<DisciplinaryActionCauseResponse>.Failure(EmployeeRelationsConfigurationErrors.DeductionConceptInvalid);
        }

        if (await repository.CodeExistsAsync(
                cause.TenantId,
                command.Code.Trim().ToUpperInvariant(),
                cause.PublicId,
                cancellationToken))
        {
            return Result<DisciplinaryActionCauseResponse>.Failure(DisciplinaryActionCauseErrors.CodeConflict);
        }

        var before = await repository.GetResponseByIdAsync(cause.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Disciplinary-action cause response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            cause.Update(command.Code, command.Name, command.DeductionConceptTypeCode, command.SortOrder);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(cause.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Disciplinary-action cause response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.DisciplinaryActionCauseUpdated,
                    AuditEntityTypes.DisciplinaryActionCause,
                    cause.PublicId,
                    cause.Code,
                    AuditActions.Update,
                    $"Updated disciplinary-action cause {cause.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<DisciplinaryActionCauseResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException ex) when (DisciplinaryActionCauseConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<DisciplinaryActionCauseResponse>.Failure(DisciplinaryActionCauseErrors.CodeConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateDisciplinaryActionCauseCommandHandler(
    IEmployeeRelationsConfigurationAuthorizationService authorizationService,
    IDisciplinaryActionCauseRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateDisciplinaryActionCauseCommand, DisciplinaryActionCauseResponse>
{
    public async Task<Result<DisciplinaryActionCauseResponse>> Handle(
        ActivateDisciplinaryActionCauseCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<DisciplinaryActionCauseResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<DisciplinaryActionCauseResponse>.Failure(authorizationResult.Error);
        }

        var cause = await repository.GetByIdAsync(command.DisciplinaryActionCauseId, cancellationToken);
        if (cause is null)
        {
            return Result<DisciplinaryActionCauseResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.DisciplinaryActionCauseId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : DisciplinaryActionCauseErrors.DisciplinaryActionCauseNotFound);
        }

        if (cause.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<DisciplinaryActionCauseResponse>.Failure(EmployeeRelationsConfigurationErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(cause.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Disciplinary-action cause response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            cause.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(cause.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Disciplinary-action cause response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.DisciplinaryActionCauseActivated,
                    AuditEntityTypes.DisciplinaryActionCause,
                    cause.PublicId,
                    cause.Code,
                    AuditActions.Reactivate,
                    $"Activated disciplinary-action cause {cause.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<DisciplinaryActionCauseResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException ex) when (DisciplinaryActionCauseConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            // The filtered (tenant, normalized_code) WHERE is_active unique index means reactivating a
            // cause whose code is already taken by an active cause trips the index — map to a clean 409.
            await transaction.RollbackAsync(cancellationToken);
            return Result<DisciplinaryActionCauseResponse>.Failure(DisciplinaryActionCauseErrors.CodeConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateDisciplinaryActionCauseCommandHandler(
    IEmployeeRelationsConfigurationAuthorizationService authorizationService,
    IDisciplinaryActionCauseRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateDisciplinaryActionCauseCommand, DisciplinaryActionCauseResponse>
{
    public async Task<Result<DisciplinaryActionCauseResponse>> Handle(
        InactivateDisciplinaryActionCauseCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<DisciplinaryActionCauseResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<DisciplinaryActionCauseResponse>.Failure(authorizationResult.Error);
        }

        var cause = await repository.GetByIdAsync(command.DisciplinaryActionCauseId, cancellationToken);
        if (cause is null)
        {
            return Result<DisciplinaryActionCauseResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.DisciplinaryActionCauseId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : DisciplinaryActionCauseErrors.DisciplinaryActionCauseNotFound);
        }

        if (cause.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<DisciplinaryActionCauseResponse>.Failure(EmployeeRelationsConfigurationErrors.ConcurrencyConflict);
        }

        // Logical-delete usage guard (DISCIPLINARY_ACTION_CAUSE_IN_USE). PR-1 has no disciplinary-action
        // table yet (M2/PR-2), so IsInUseAsync is a stub returning false today; the real reference check
        // is wired in PR-4 once that table exists.
        if (await repository.IsInUseAsync(cause.TenantId, cause.PublicId, cancellationToken))
        {
            return Result<DisciplinaryActionCauseResponse>.Failure(DisciplinaryActionCauseErrors.InUse);
        }

        var before = await repository.GetResponseByIdAsync(cause.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Disciplinary-action cause response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            cause.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(cause.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Disciplinary-action cause response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.DisciplinaryActionCauseInactivated,
                    AuditEntityTypes.DisciplinaryActionCause,
                    cause.PublicId,
                    cause.Code,
                    AuditActions.Deactivate,
                    $"Inactivated disciplinary-action cause {cause.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<DisciplinaryActionCauseResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class DisciplinaryActionCauseConstraintViolations
{
    public static bool IsCodeConflict(string? constraintName) =>
        string.Equals(constraintName, EmployeeRelationsMasterConstraintNames.DisciplinaryActionCauseCodeUnique, StringComparison.Ordinal);
}

internal static class DisciplinaryActionCausePolicyAdapter
{
    public static DisciplinaryActionCauseListItemResponse ApplyAllowedActions(
        DisciplinaryActionCauseListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage) =>
        response with { AllowedActions = Evaluate(response.IsActive, resourceActionPolicyService, canManage) };

    public static DisciplinaryActionCauseResponse ApplyAllowedActions(
        DisciplinaryActionCauseResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage) =>
        response with { AllowedActions = Evaluate(response.IsActive, resourceActionPolicyService, canManage) };

    private static AllowedActionsResponse Evaluate(
        bool isActive,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage) =>
        resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                EmployeeRelationsConfigurationPermissionCodes.DisciplinaryActionCausesResourceKey,
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
