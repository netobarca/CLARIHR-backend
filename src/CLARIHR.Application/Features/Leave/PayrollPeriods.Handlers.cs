using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Leave;
using CLARIHR.Application.Abstractions.Payroll;
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

internal sealed class SearchPayrollPeriodsQueryHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IPayrollPeriodRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchPayrollPeriodsQuery, PagedResponse<PayrollPeriodListItemResponse>>
{
    public async Task<Result<PagedResponse<PayrollPeriodListItemResponse>>> Handle(
        SearchPayrollPeriodsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<PayrollPeriodListItemResponse>>.Failure(authorizationResult.Error);
        }

        var response = await repository.SearchAsync(
            query.CompanyId,
            query.PayPeriodTypeCode,
            query.Year,
            query.IsActive,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (!query.IncludeAllowedActions)
        {
            return Result<PagedResponse<PayrollPeriodListItemResponse>>.Success(response);
        }

        var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = response.Items
            .Select(item => PayrollPeriodPolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManage))
            .ToArray();
        response = response with { Items = items };

        return Result<PagedResponse<PayrollPeriodListItemResponse>>.Success(response);
    }
}

internal sealed class GetPayrollPeriodByIdQueryHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IPayrollPeriodRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetPayrollPeriodByIdQuery, PayrollPeriodResponse>
{
    public async Task<Result<PayrollPeriodResponse>> Handle(
        GetPayrollPeriodByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PayrollPeriodResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollPeriodResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetResponseByIdAsync(query.PayrollPeriodId, cancellationToken);
        if (response is not null)
        {
            var canManage = (await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = PayrollPeriodPolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManage);

            return Result<PayrollPeriodResponse>.Success(response);
        }

        return Result<PayrollPeriodResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.PayrollPeriodId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : PayrollPeriodErrors.PayrollPeriodNotFound);
    }
}

internal sealed class CreatePayrollPeriodCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IPayrollPeriodRepository repository,
    IPayrollDefinitionRepository payrollDefinitionRepository,
    IPersonnelFileRepository personnelFileRepository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreatePayrollPeriodCommand, PayrollPeriodResponse>
{
    public async Task<Result<PayrollPeriodResponse>> Handle(
        CreatePayrollPeriodCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollPeriodResponse>.Failure(authorizationResult.Error);
        }

        var definitionResolution = await PayrollPeriodRules.ResolveDefinitionAsync(
            payrollDefinitionRepository,
            command.CompanyId,
            command.PayrollDefinitionPublicId,
            command.PayPeriodTypeCode,
            cancellationToken);
        if (definitionResolution.IsFailure)
        {
            return Result<PayrollPeriodResponse>.Failure(definitionResolution.Error);
        }

        var businessResult = await PayrollPeriodRules.ValidateAsync(
            repository,
            personnelFileRepository,
            command.CompanyId,
            command.PayPeriodTypeCode,
            command.Year,
            command.Number,
            command.StartDate,
            command.EndDate,
            definitionResolution.Value,
            excludingPayrollPeriodId: null,
            cancellationToken);
        if (businessResult.IsFailure)
        {
            return Result<PayrollPeriodResponse>.Failure(businessResult.Error);
        }

        PayrollPeriodDefinition payrollPeriod;
        try
        {
            payrollPeriod = PayrollPeriodDefinition.Create(
                command.PayPeriodTypeCode,
                command.Year,
                command.Number,
                command.Label,
                command.StartDate,
                command.EndDate);
        }
        catch (ArgumentException)
        {
            // Validators mirror the domain guards, but a value that still slips through must
            // surface as a clean 422 instead of a 500.
            return Result<PayrollPeriodResponse>.Failure(PayrollPeriodErrors.RuleViolation);
        }

        try
        {
            payrollPeriod.AssignDefinition(definitionResolution.Value);
            payrollPeriod.SetCode(command.Code);
            payrollPeriod.SetSchedule(command.CutoffDate, command.PaymentDate, command.Month);
            payrollPeriod.SetWindows(
                command.AllowsOvertimeEntry,
                command.OvertimeEntryStart,
                command.OvertimeEntryEnd,
                command.AllowsAttendance,
                command.AttendanceEntryStart,
                command.AttendanceEntryEnd);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            // Cutoff out of range / month out of bounds / incoherent windows → clean 422 (REQ-012 §5).
            return Result<PayrollPeriodResponse>.Failure(PayrollPeriodErrors.ScheduleInvalid);
        }

        payrollPeriod.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(payrollPeriod);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetResponseByIdAsync(payrollPeriod.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Payroll period response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PayrollPeriodDefinitionCreated,
                    AuditEntityTypes.PayrollPeriodDefinition,
                    payrollPeriod.PublicId,
                    payrollPeriod.Label,
                    AuditActions.Create,
                    $"Created payroll period {payrollPeriod.Label} ({payrollPeriod.PayPeriodTypeCode} {payrollPeriod.Year}-{payrollPeriod.Number}).",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PayrollPeriodResponse>.Success(response);
        }
        catch (UniqueConstraintViolationException ex) when (PayrollPeriodConstraintViolations.IsPeriodConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PayrollPeriodResponse>.Failure(PayrollPeriodErrors.PeriodConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePayrollPeriodCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IPayrollPeriodRepository repository,
    IPayrollDefinitionRepository payrollDefinitionRepository,
    IPersonnelFileRepository personnelFileRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePayrollPeriodCommand, PayrollPeriodResponse>
{
    public async Task<Result<PayrollPeriodResponse>> Handle(
        UpdatePayrollPeriodCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PayrollPeriodResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollPeriodResponse>.Failure(authorizationResult.Error);
        }

        var payrollPeriod = await repository.GetByIdAsync(command.PayrollPeriodId, cancellationToken);
        if (payrollPeriod is null)
        {
            return Result<PayrollPeriodResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PayrollPeriodId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PayrollPeriodErrors.PayrollPeriodNotFound);
        }

        if (payrollPeriod.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PayrollPeriodResponse>.Failure(LeaveConfigurationErrors.ConcurrencyConflict);
        }

        // A CERRADO/ANULADO period is immutable (REQ-012 §1.2); pre-check to avoid a domain exception → 500.
        if (!payrollPeriod.IsEditable)
        {
            return Result<PayrollPeriodResponse>.Failure(PayrollPeriodErrors.StateRuleViolation);
        }

        var definitionResolution = await PayrollPeriodRules.ResolveDefinitionAsync(
            payrollDefinitionRepository,
            payrollPeriod.TenantId,
            command.PayrollDefinitionPublicId,
            command.PayPeriodTypeCode,
            cancellationToken);
        if (definitionResolution.IsFailure)
        {
            return Result<PayrollPeriodResponse>.Failure(definitionResolution.Error);
        }

        var businessResult = await PayrollPeriodRules.ValidateAsync(
            repository,
            personnelFileRepository,
            payrollPeriod.TenantId,
            command.PayPeriodTypeCode,
            command.Year,
            command.Number,
            command.StartDate,
            command.EndDate,
            definitionResolution.Value,
            payrollPeriod.PublicId,
            cancellationToken);
        if (businessResult.IsFailure)
        {
            return Result<PayrollPeriodResponse>.Failure(businessResult.Error);
        }

        var before = await repository.GetResponseByIdAsync(payrollPeriod.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Payroll period response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            payrollPeriod.Update(
                command.PayPeriodTypeCode,
                command.Year,
                command.Number,
                command.Label,
                command.StartDate,
                command.EndDate);
            try
            {
                payrollPeriod.AssignDefinition(definitionResolution.Value);
                payrollPeriod.SetCode(command.Code);
                payrollPeriod.SetSchedule(command.CutoffDate, command.PaymentDate, command.Month);
                payrollPeriod.SetWindows(
                    command.AllowsOvertimeEntry,
                    command.OvertimeEntryStart,
                    command.OvertimeEntryEnd,
                    command.AllowsAttendance,
                    command.AttendanceEntryStart,
                    command.AttendanceEntryEnd);
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
            {
                // Cutoff out of the (possibly re-dated) range / bad month / incoherent windows → 422.
                await transaction.RollbackAsync(cancellationToken);
                return Result<PayrollPeriodResponse>.Failure(PayrollPeriodErrors.ScheduleInvalid);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(payrollPeriod.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Payroll period response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PayrollPeriodDefinitionUpdated,
                    AuditEntityTypes.PayrollPeriodDefinition,
                    payrollPeriod.PublicId,
                    payrollPeriod.Label,
                    AuditActions.Update,
                    $"Updated payroll period {payrollPeriod.Label} ({payrollPeriod.PayPeriodTypeCode} {payrollPeriod.Year}-{payrollPeriod.Number}).",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PayrollPeriodResponse>.Success(after);
        }
        catch (ArgumentException)
        {
            // The domain mutator re-runs the entity guards; a value that slips past the validator
            // surfaces as a clean 422 instead of a 500.
            await transaction.RollbackAsync(cancellationToken);
            return Result<PayrollPeriodResponse>.Failure(PayrollPeriodErrors.RuleViolation);
        }
        catch (UniqueConstraintViolationException ex) when (PayrollPeriodConstraintViolations.IsPeriodConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PayrollPeriodResponse>.Failure(PayrollPeriodErrors.PeriodConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivatePayrollPeriodCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IPayrollPeriodRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivatePayrollPeriodCommand, PayrollPeriodResponse>
{
    public async Task<Result<PayrollPeriodResponse>> Handle(
        ActivatePayrollPeriodCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PayrollPeriodResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollPeriodResponse>.Failure(authorizationResult.Error);
        }

        var payrollPeriod = await repository.GetByIdAsync(command.PayrollPeriodId, cancellationToken);
        if (payrollPeriod is null)
        {
            return Result<PayrollPeriodResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PayrollPeriodId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PayrollPeriodErrors.PayrollPeriodNotFound);
        }

        if (payrollPeriod.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PayrollPeriodResponse>.Failure(LeaveConfigurationErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(payrollPeriod.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Payroll period response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            payrollPeriod.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(payrollPeriod.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Payroll period response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PayrollPeriodDefinitionActivated,
                    AuditEntityTypes.PayrollPeriodDefinition,
                    payrollPeriod.PublicId,
                    payrollPeriod.Label,
                    AuditActions.Reactivate,
                    $"Activated payroll period {payrollPeriod.Label} ({payrollPeriod.PayPeriodTypeCode} {payrollPeriod.Year}-{payrollPeriod.Number}).",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PayrollPeriodResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivatePayrollPeriodCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IPayrollPeriodRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivatePayrollPeriodCommand, PayrollPeriodResponse>
{
    public async Task<Result<PayrollPeriodResponse>> Handle(
        InactivatePayrollPeriodCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PayrollPeriodResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollPeriodResponse>.Failure(authorizationResult.Error);
        }

        var payrollPeriod = await repository.GetByIdAsync(command.PayrollPeriodId, cancellationToken);
        if (payrollPeriod is null)
        {
            return Result<PayrollPeriodResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PayrollPeriodId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PayrollPeriodErrors.PayrollPeriodNotFound);
        }

        if (payrollPeriod.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PayrollPeriodResponse>.Failure(LeaveConfigurationErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(payrollPeriod.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Payroll period response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            payrollPeriod.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(payrollPeriod.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Payroll period response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PayrollPeriodDefinitionInactivated,
                    AuditEntityTypes.PayrollPeriodDefinition,
                    payrollPeriod.PublicId,
                    payrollPeriod.Label,
                    AuditActions.Deactivate,
                    $"Inactivated payroll period {payrollPeriod.Label} ({payrollPeriod.PayPeriodTypeCode} {payrollPeriod.Year}-{payrollPeriod.Number}).",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PayrollPeriodResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class PayrollPeriodRules
{
    /// <summary>
    /// Resolves the optional Nómina reference of a create/update (REQ-012 D-03). No reference →
    /// legacy-style period (null). A reference that does not resolve to an ACTIVE definition of the
    /// tenant → 422 <c>PAYROLL_PERIOD_DEFINITION_REQUIRED</c>; a frequency mismatch between the period's
    /// pay-period type and the Nómina's frequency → 422 <c>PAYROLL_PERIOD_SCHEDULE_INVALID</c> (a
    /// QUINCENAL period cannot hang from a MENSUAL payroll).
    /// </summary>
    public static async Task<Result<long?>> ResolveDefinitionAsync(
        IPayrollDefinitionRepository payrollDefinitionRepository,
        Guid companyId,
        Guid? payrollDefinitionPublicId,
        string payPeriodTypeCode,
        CancellationToken cancellationToken)
    {
        if (!payrollDefinitionPublicId.HasValue)
        {
            return Result<long?>.Success(null);
        }

        var definition = await payrollDefinitionRepository.GetByIdAsync(payrollDefinitionPublicId.Value, cancellationToken);
        if (definition is null || definition.TenantId != companyId || !definition.IsActive)
        {
            return Result<long?>.Failure(PayrollPeriodErrors.DefinitionRequired);
        }

        if (!string.Equals(definition.PayPeriodCode, payPeriodTypeCode.Trim().ToUpperInvariant(), StringComparison.Ordinal))
        {
            return Result<long?>.Failure(PayrollPeriodErrors.ScheduleInvalid);
        }

        return Result<long?>.Success(definition.Id);
    }

    /// <summary>
    /// Shared Create/Update business validations, in order:
    /// (1) the pay-period type must be an active code of the country-scoped pay-periods general
    /// catalog (<c>PAY_PERIOD_CATALOG</c>, validated via CatalogCodeIsActiveAsync like the
    /// compensation-concept pay period) → 422;
    /// (2) (type, year, number) must be unique in its BUCKET → 409 (per-Nómina when the period hangs
    /// from one — REQ-012 §1.2 —, the legacy bucket otherwise; the probe closes the sequential case,
    /// the partial unique indexes close the concurrent one);
    /// (3) the date range must not overlap another ACTIVE period of the same type and year in the same
    /// bucket → 422 (two Nóminas of the same frequency deliberately do not collide).
    /// </summary>
    public static async Task<Result> ValidateAsync(
        IPayrollPeriodRepository repository,
        IPersonnelFileRepository personnelFileRepository,
        Guid companyId,
        string payPeriodTypeCode,
        int year,
        int number,
        DateOnly startDate,
        DateOnly endDate,
        long? payrollDefinitionId,
        Guid? excludingPayrollPeriodId,
        CancellationToken cancellationToken)
    {
        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                companyId,
                PersonnelCurriculumCatalogCategories.PayPeriod,
                payPeriodTypeCode,
                cancellationToken))
        {
            return Result.Failure(PayrollPeriodErrors.TypeInvalid);
        }

        var normalizedTypeCode = payPeriodTypeCode.Trim().ToUpperInvariant();

        var duplicate = payrollDefinitionId.HasValue
            ? await repository.PeriodExistsForDefinitionAsync(
                companyId,
                payrollDefinitionId.Value,
                year,
                number,
                excludingPayrollPeriodId,
                cancellationToken)
            : await repository.PeriodExistsAsync(
                companyId,
                normalizedTypeCode,
                year,
                number,
                excludingPayrollPeriodId,
                cancellationToken);
        if (duplicate)
        {
            return Result.Failure(PayrollPeriodErrors.PeriodConflict);
        }

        if (await repository.HasOverlapAsync(
                companyId,
                normalizedTypeCode,
                year,
                startDate,
                endDate,
                payrollDefinitionId,
                excludingPayrollPeriodId,
                cancellationToken))
        {
            return Result.Failure(PayrollPeriodErrors.PeriodOverlap);
        }

        return Result.Success();
    }
}

internal static class PayrollPeriodConstraintViolations
{
    // The (TenantId, PayPeriodTypeCode, Year, Number) unique index is the real guard against
    // duplicate periods; the up-front PeriodExistsAsync probe only closes the common (sequential)
    // case. On a concurrent create/update of the same period, the second writer trips this index —
    // map it to the same clean 409 as the probe instead of letting the 23505 escape as an HTTP 500
    // (mirrors MedicalClinicConstraintViolations).
    public static bool IsPeriodConflict(string? constraintName) =>
        string.Equals(constraintName, LeaveMasterConstraintNames.PayrollPeriodUnique, StringComparison.Ordinal);
}

internal static class PayrollPeriodPolicyAdapter
{
    public static PayrollPeriodListItemResponse ApplyAllowedActions(
        PayrollPeriodListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                LeaveConfigurationPermissionCodes.PayrollPeriodsResourceKey,
                response.PayPeriodTypeCode,
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

    public static PayrollPeriodResponse ApplyAllowedActions(
        PayrollPeriodResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                LeaveConfigurationPermissionCodes.PayrollPeriodsResourceKey,
                response.PayPeriodTypeCode,
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
