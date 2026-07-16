using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Payroll;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Payroll.Common;
using CLARIHR.Domain.Payroll;

namespace CLARIHR.Application.Features.Payroll;

internal sealed class SearchWorkSchedulesQueryHandler(
    IPayrollConfigurationAuthorizationService authorizationService,
    IWorkScheduleRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchWorkSchedulesQuery, PagedResponse<WorkScheduleListItemResponse>>
{
    public async Task<Result<PagedResponse<WorkScheduleListItemResponse>>> Handle(
        SearchWorkSchedulesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<WorkScheduleListItemResponse>>.Failure(authorizationResult.Error);
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
            return Result<PagedResponse<WorkScheduleListItemResponse>>.Success(response);
        }

        var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = response.Items
            .Select(item => WorkSchedulePolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManage))
            .ToArray();
        response = response with { Items = items };

        return Result<PagedResponse<WorkScheduleListItemResponse>>.Success(response);
    }
}

internal sealed class GetWorkScheduleByIdQueryHandler(
    IPayrollConfigurationAuthorizationService authorizationService,
    IWorkScheduleRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetWorkScheduleByIdQuery, WorkScheduleResponse>
{
    public async Task<Result<WorkScheduleResponse>> Handle(
        GetWorkScheduleByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<WorkScheduleResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<WorkScheduleResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetResponseByIdAsync(query.WorkScheduleId, cancellationToken);
        if (response is not null)
        {
            var canManage = (await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = WorkSchedulePolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManage);

            return Result<WorkScheduleResponse>.Success(response);
        }

        return Result<WorkScheduleResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.WorkScheduleId, cancellationToken)
                ? WorkScheduleErrors.TenantMismatch(RbacPermissionAction.Read)
                : WorkScheduleErrors.WorkScheduleNotFound);
    }
}

internal sealed class CreateWorkScheduleCommandHandler(
    IPayrollConfigurationAuthorizationService authorizationService,
    IWorkScheduleRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateWorkScheduleCommand, WorkScheduleResponse>
{
    public async Task<Result<WorkScheduleResponse>> Handle(
        CreateWorkScheduleCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<WorkScheduleResponse>.Failure(authorizationResult.Error);
        }

        if (await repository.CodeExistsAsync(
                command.CompanyId,
                command.Code.Trim().ToUpperInvariant(),
                excludingWorkScheduleId: null,
                cancellationToken))
        {
            return Result<WorkScheduleResponse>.Failure(WorkScheduleErrors.CodeTaken);
        }

        WorkSchedule schedule;
        try
        {
            schedule = WorkSchedule.Create(
                command.Code,
                command.Name,
                command.ScheduleLabel,
                command.AttendanceDateAnchor,
                command.ScheduleClass,
                command.TotalWeeklyHours,
                WorkScheduleMapping.ToDayInputs(command.Days));
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            // Day-set violations (duplicated weekday, bad meal break, night shift with meal, zero shift,
            // anchor/class out of range) → clean 422 instead of a 500 (REQ-012 §5).
            return Result<WorkScheduleResponse>.Failure(WorkScheduleErrors.DayInvalid);
        }

        schedule.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(schedule);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetResponseByIdAsync(schedule.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Work schedule response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.WorkScheduleCreated,
                    AuditEntityTypes.WorkSchedule,
                    schedule.PublicId,
                    schedule.Code,
                    AuditActions.Create,
                    $"Created work schedule {schedule.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<WorkScheduleResponse>.Success(response);
        }
        catch (UniqueConstraintViolationException ex) when (WorkScheduleConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<WorkScheduleResponse>.Failure(WorkScheduleErrors.CodeTaken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateWorkScheduleCommandHandler(
    IPayrollConfigurationAuthorizationService authorizationService,
    IWorkScheduleRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateWorkScheduleCommand, WorkScheduleResponse>
{
    public async Task<Result<WorkScheduleResponse>> Handle(
        UpdateWorkScheduleCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<WorkScheduleResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<WorkScheduleResponse>.Failure(authorizationResult.Error);
        }

        var schedule = await repository.GetByIdAsync(command.WorkScheduleId, cancellationToken);
        if (schedule is null)
        {
            return Result<WorkScheduleResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.WorkScheduleId, cancellationToken)
                    ? WorkScheduleErrors.TenantMismatch(RbacPermissionAction.Update)
                    : WorkScheduleErrors.WorkScheduleNotFound);
        }

        if (schedule.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<WorkScheduleResponse>.Failure(WorkScheduleErrors.ConcurrencyConflict);
        }

        if (await repository.CodeExistsAsync(
                schedule.TenantId,
                command.Code.Trim().ToUpperInvariant(),
                schedule.PublicId,
                cancellationToken))
        {
            return Result<WorkScheduleResponse>.Failure(WorkScheduleErrors.CodeTaken);
        }

        var before = await repository.GetResponseByIdAsync(schedule.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Work schedule response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            try
            {
                schedule.Update(
                    command.Code,
                    command.Name,
                    command.ScheduleLabel,
                    command.AttendanceDateAnchor,
                    command.ScheduleClass,
                    command.TotalWeeklyHours,
                    WorkScheduleMapping.ToDayInputs(command.Days));
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<WorkScheduleResponse>.Failure(WorkScheduleErrors.DayInvalid);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(schedule.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Work schedule response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.WorkScheduleUpdated,
                    AuditEntityTypes.WorkSchedule,
                    schedule.PublicId,
                    schedule.Code,
                    AuditActions.Update,
                    $"Updated work schedule {schedule.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<WorkScheduleResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException ex) when (WorkScheduleConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<WorkScheduleResponse>.Failure(WorkScheduleErrors.CodeTaken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateWorkScheduleCommandHandler(
    IPayrollConfigurationAuthorizationService authorizationService,
    IWorkScheduleRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateWorkScheduleCommand, WorkScheduleResponse>
{
    public async Task<Result<WorkScheduleResponse>> Handle(
        ActivateWorkScheduleCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<WorkScheduleResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<WorkScheduleResponse>.Failure(authorizationResult.Error);
        }

        var schedule = await repository.GetByIdAsync(command.WorkScheduleId, cancellationToken);
        if (schedule is null)
        {
            return Result<WorkScheduleResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.WorkScheduleId, cancellationToken)
                    ? WorkScheduleErrors.TenantMismatch(RbacPermissionAction.Update)
                    : WorkScheduleErrors.WorkScheduleNotFound);
        }

        if (schedule.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<WorkScheduleResponse>.Failure(WorkScheduleErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(schedule.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Work schedule response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            schedule.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(schedule.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Work schedule response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.WorkScheduleActivated,
                    AuditEntityTypes.WorkSchedule,
                    schedule.PublicId,
                    schedule.Code,
                    AuditActions.Reactivate,
                    $"Activated work schedule {schedule.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<WorkScheduleResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException ex) when (WorkScheduleConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            // The filtered (tenant, normalized_code) WHERE is_active unique index means reactivating a
            // schedule whose code is already taken by an active one trips the index — map to a clean 409.
            await transaction.RollbackAsync(cancellationToken);
            return Result<WorkScheduleResponse>.Failure(WorkScheduleErrors.CodeTaken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateWorkScheduleCommandHandler(
    IPayrollConfigurationAuthorizationService authorizationService,
    IWorkScheduleRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateWorkScheduleCommand, WorkScheduleResponse>
{
    public async Task<Result<WorkScheduleResponse>> Handle(
        InactivateWorkScheduleCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<WorkScheduleResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<WorkScheduleResponse>.Failure(authorizationResult.Error);
        }

        var schedule = await repository.GetByIdAsync(command.WorkScheduleId, cancellationToken);
        if (schedule is null)
        {
            return Result<WorkScheduleResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.WorkScheduleId, cancellationToken)
                    ? WorkScheduleErrors.TenantMismatch(RbacPermissionAction.Update)
                    : WorkScheduleErrors.WorkScheduleNotFound);
        }

        if (schedule.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<WorkScheduleResponse>.Failure(WorkScheduleErrors.ConcurrencyConflict);
        }

        // Logical-delete usage guard: the plaza's WorkdayCode IS the link (no FK), so the probe is real
        // from this PR — an ACTIVE assignment carrying the code blocks the inactivation (D-06/P-17).
        if (await repository.IsInUseAsync(schedule.TenantId, schedule.NormalizedCode, cancellationToken))
        {
            return Result<WorkScheduleResponse>.Failure(WorkScheduleErrors.InUse);
        }

        var before = await repository.GetResponseByIdAsync(schedule.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Work schedule response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            schedule.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(schedule.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Work schedule response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.WorkScheduleInactivated,
                    AuditEntityTypes.WorkSchedule,
                    schedule.PublicId,
                    schedule.Code,
                    AuditActions.Deactivate,
                    $"Inactivated work schedule {schedule.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<WorkScheduleResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class WorkScheduleMapping
{
    public static IReadOnlyCollection<WorkScheduleDayInput> ToDayInputs(
        IReadOnlyCollection<WorkScheduleDayInputModel> days) =>
        days.Select(day => new WorkScheduleDayInput(
                day.DayOfWeek,
                day.StartTime,
                day.EndTime,
                day.MealStart,
                day.MealEnd))
            .ToArray();
}

internal static class WorkScheduleConstraintViolations
{
    // The filtered (TenantId, NormalizedCode) WHERE is_active unique index is the real guard against
    // duplicate active codes; the up-front CodeExistsAsync probe only closes the common (sequential) case.
    // On a concurrent create/update/activate of the same code, the second writer trips this index — map it
    // to the same clean 409 as the probe.
    public static bool IsCodeConflict(string? constraintName) =>
        string.Equals(constraintName, PayrollMasterConstraintNames.WorkScheduleCodeUnique, StringComparison.Ordinal);
}

internal static class WorkSchedulePolicyAdapter
{
    public static WorkScheduleListItemResponse ApplyAllowedActions(
        WorkScheduleListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage) =>
        response with { AllowedActions = Evaluate(response.IsActive, resourceActionPolicyService, canManage) };

    public static WorkScheduleResponse ApplyAllowedActions(
        WorkScheduleResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage) =>
        response with { AllowedActions = Evaluate(response.IsActive, resourceActionPolicyService, canManage) };

    private static AllowedActionsResponse Evaluate(
        bool isActive,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage) =>
        resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                PayrollConfigurationPermissionCodes.WorkSchedulesResourceKey,
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
