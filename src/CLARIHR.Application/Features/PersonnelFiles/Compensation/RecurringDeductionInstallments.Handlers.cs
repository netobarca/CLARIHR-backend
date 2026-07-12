using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PersonnelFiles.Compensation;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>Maps a charge entity to its API response (user ids null-safe; payroll-period public id supplied).</summary>
public static class RecurringDeductionInstallmentMapping
{
    public static RecurringDeductionInstallmentResponse ToResponse(
        PersonnelFileRecurringDeductionInstallment entity, Guid? payrollPeriodPublicId) =>
        new(
            entity.PublicId,
            entity.Kind,
            entity.InstallmentNumber,
            entity.ExtraordinaryNumber,
            entity.AppliedDate,
            entity.TheoreticalDueDate,
            entity.Amount,
            entity.CapitalAmount,
            entity.InterestAmount,
            entity.CurrencyCode,
            entity.PayrollTypeCode,
            payrollPeriodPublicId,
            entity.PayrollPeriodLabel,
            entity.OriginCode,
            entity.StatusCode,
            NullIfEmpty(entity.AppliedByUserId),
            entity.AnnulmentReason,
            entity.AnnulledByUserId,
            entity.AnnulledUtc,
            entity.Notes,
            entity.ConcurrencyToken);

    private static Guid? NullIfEmpty(Guid value) => value == Guid.Empty ? null : value;
}

/// <summary>
/// Shared pure arithmetic for turning a recurring deduction into the plan / applied-charge set the rules consume,
/// so the unitary application, the extraordinary payment and the apply-period batch all cuadran with
/// <see cref="RecurringDeductionRules"/> by construction.
/// </summary>
internal static class RecurringDeductionInstallmentSupport
{
    /// <summary>The numbers of the active REGULAR charges (an extraordinary payment has no plan number).</summary>
    public static HashSet<int> ActiveAppliedNumbers(PersonnelFileRecurringDeduction deduction) =>
        deduction.Installments
            .Where(item => item.StatusCode == RecurringDeductionInstallmentStatuses.Aplicada
                && item.Kind == RecurringDeductionInstallmentKinds.Regular)
            .Select(item => item.InstallmentNumber!.Value)
            .ToHashSet();

    /// <summary>Everything charged so far (regular + extraordinary) — the basis of "total cobrado".</summary>
    public static decimal ChargedAmount(PersonnelFileRecurringDeduction deduction) =>
        deduction.Installments
            .Where(item => item.StatusCode == RecurringDeductionInstallmentStatuses.Aplicada)
            .Sum(item => item.Amount);

    /// <summary>The capital charged so far (an extraordinary payment is 100 % capital) — the basis of the payoff.</summary>
    public static decimal ChargedCapital(PersonnelFileRecurringDeduction deduction) =>
        deduction.Installments
            .Where(item => item.StatusCode == RecurringDeductionInstallmentStatuses.Aplicada)
            .Sum(item => item.CapitalAmount ?? item.Amount);

    public static RecurringDeductionPlan ToPlan(PersonnelFileRecurringDeduction deduction) =>
        new(
            deduction.PlanSegments
                .Where(segment => segment.IsActive)
                .OrderBy(segment => segment.FromInstallment)
                .Select(segment => new RecurringDeductionSegment(segment.FromInstallment, segment.ToInstallment, segment.InstallmentValue))
                .ToList(),
            deduction.PlannedInstallmentCount,
            deduction.TotalPlanAmount(),
            deduction.IsIndefinite,
            deduction.UsesCompoundInterest,
            deduction.PrincipalAmount,
            deduction.InterestRatePercent,
            deduction.InstallmentFrequencyCode);

    public static RecurringDeductionPlan ToPlan(RecurringDeductionScheduleData data) =>
        new(
            data.Segments.OrderBy(segment => segment.FromInstallment).ToList(),
            InstallmentCountOf(data.Segments, data.IsIndefinite, data.UsesCompoundInterest, data.PlannedInstallments),
            TotalOf(data.Segments, data.IsIndefinite, data.UsesCompoundInterest),
            data.IsIndefinite,
            data.UsesCompoundInterest,
            data.PrincipalAmount,
            data.InterestRatePercent,
            data.InstallmentFrequencyCode);

    public static RecurringDeductionPlan ToPlan(RecurringDeductionBatchScanItem scan) =>
        new(
            scan.Segments.OrderBy(segment => segment.FromInstallment).ToList(),
            InstallmentCountOf(scan.Segments, scan.IsIndefinite, scan.UsesCompoundInterest, scan.PlannedInstallments),
            TotalOf(scan.Segments, scan.IsIndefinite, scan.UsesCompoundInterest),
            scan.IsIndefinite,
            scan.UsesCompoundInterest,
            scan.PrincipalAmount,
            scan.InterestRatePercent,
            scan.InstallmentFrequencyCode);

    /// <summary>
    /// The CHARGE numbers of <paramref name="scan"/> whose theoretical due date is on/before <paramref name="cutoff"/>
    /// and are not yet applied (ascending). Includes overdue ones — the batch catches up on postponed charges. A
    /// finite plan is bounded by its charge count; an indefinite plan iterates until the due date passes the cutoff
    /// (bounded defensively).
    /// </summary>
    public static IReadOnlyList<int> PendingChargesUpTo(RecurringDeductionBatchScanItem scan, DateOnly cutoff)
    {
        var plan = ToPlan(scan);
        var applied = scan.AppliedInstallmentNumbers.ToHashSet();
        var exceptionMonths = scan.ExceptionMonths.ToHashSet();
        var pending = new List<int>();

        var chargeCount = RecurringDeductionRules.ChargeCount(plan, scan.ApplicationFrequencyCode);
        const int indefiniteCap = 1200;
        var ceiling = chargeCount ?? indefiniteCap;

        for (var number = 1; number <= ceiling; number++)
        {
            var due = RecurringDeductionRules.ChargeDueDateFor(
                scan.ApplicationFrequencyCode, scan.InstallmentStartDate, exceptionMonths, number);

            if (due > cutoff)
            {
                // The dates are monotonically increasing: everything past this point is beyond the cutoff.
                break;
            }

            if (!applied.Contains(number))
            {
                pending.Add(number);
            }
        }

        return pending;
    }

    private static int? InstallmentCountOf(
        IReadOnlyCollection<RecurringDeductionSegment> segments,
        bool isIndefinite,
        bool usesCompoundInterest,
        int? plannedInstallments)
    {
        if (isIndefinite)
        {
            return null;
        }

        return usesCompoundInterest
            ? plannedInstallments
            : segments.Select(segment => segment.ToInstallment).Max();
    }

    private static decimal? TotalOf(
        IReadOnlyCollection<RecurringDeductionSegment> segments,
        bool isIndefinite,
        bool usesCompoundInterest)
    {
        if (isIndefinite || usesCompoundInterest)
        {
            return null;
        }

        var total = 0m;
        foreach (var segment in segments)
        {
            if (segment.ToInstallment is not { } to)
            {
                return null;
            }

            total += (to - segment.FromInstallment + 1) * segment.InstallmentValue;
        }

        return total;
    }
}

// ── Reads (View — schedule + history) ─────────────────────────────────────────────────────────────────

internal sealed class GetRecurringDeductionScheduleQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<GetRecurringDeductionScheduleQuery, RecurringDeductionScheduleResponse>
{
    public async Task<Result<RecurringDeductionScheduleResponse>> Handle(
        GetRecurringDeductionScheduleQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForRecurringDeductionReadAsync<RecurringDeductionScheduleResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var data = await employeeRepository.GetRecurringDeductionScheduleDataAsync(
            personnelFile!.PublicId, query.RecurringDeductionPublicId, cancellationToken);
        if (data is null)
        {
            return Result<RecurringDeductionScheduleResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var plan = RecurringDeductionInstallmentSupport.ToPlan(data);
        var appliedNumbers = data.AppliedInstallmentNumbers.ToHashSet();
        var exceptionMonths = data.ExceptionMonths.ToHashSet();
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow);

        var projection = RecurringDeductionRules.BuildChargeProjection(
            plan, data.ApplicationFrequencyCode, data.InstallmentStartDate, exceptionMonths, appliedNumbers, today);

        var items = projection
            .Select(item => new RecurringDeductionScheduleItemResponse(
                item.InstallmentNumber,
                item.TheoreticalDueDate,
                item.Amount,
                item.CapitalAmount,
                item.InterestAmount,
                item.IsApplied,
                item.IsOverdue))
            .ToArray();

        // "Total cobrado" is what has actually been charged; "total no cobrado" is what the plan still owes. For a
        // compound-interest credit the outstanding BALANCE (the payoff) is the capital, which is less than the sum
        // of the remaining quotas — the two figures are deliberately different.
        var totalAmount = data.UsesCompoundInterest
            ? TotalWithInterest(plan)
            : plan.TotalAmount;
        var totalOutstanding = totalAmount is { } total
            ? RecurringDeductionRules.Round2(Math.Max(0m, total - data.ChargedAmount))
            : (decimal?)null;

        var response = new RecurringDeductionScheduleResponse(
            data.PublicId,
            data.StatusCode,
            data.IsIndefinite,
            data.UsesCompoundInterest,
            data.InstallmentFrequencyCode,
            data.ApplicationFrequencyCode,
            data.EffectiveDate,
            data.InstallmentStartDate,
            data.ExceptionMonths,
            plan.InstallmentCount,
            RecurringDeductionRules.ChargeCount(plan, data.ApplicationFrequencyCode),
            totalAmount,
            RecurringDeductionRules.Round2(data.ChargedAmount),
            totalOutstanding,
            RecurringDeductionRules.SettlementBalance(plan, data.ChargedAmount, data.ChargedCapital),
            RecurringDeductionRules.IsChargePlanComplete(
                plan, data.ApplicationFrequencyCode, data.ChargedAmount, data.ChargedCapital, appliedNumbers),
            RecurringDeductionRules.NextInstallmentNumber(appliedNumbers),
            items);

        return Result<RecurringDeductionScheduleResponse>.Success(response);
    }

    private static decimal TotalWithInterest(RecurringDeductionPlan plan) =>
        RecurringDeductionRules.Round2(
            RecurringDeductionRules.BuildAmortizationSchedule(
                plan.PrincipalAmount!.Value,
                plan.AnnualRatePercent!.Value,
                plan.InstallmentCount!.Value,
                plan.InstallmentFrequencyCode)
            .Sum(row => row.Amount));
}

internal sealed class GetRecurringDeductionInstallmentsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<GetRecurringDeductionInstallmentsQuery, RecurringDeductionInstallmentHistoryResponse>
{
    public async Task<Result<RecurringDeductionInstallmentHistoryResponse>> Handle(
        GetRecurringDeductionInstallmentsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForRecurringDeductionReadAsync<RecurringDeductionInstallmentHistoryResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var history = await employeeRepository.GetRecurringDeductionInstallmentHistoryAsync(
            personnelFile!.PublicId, query.RecurringDeductionPublicId, query.PageNumber, query.PageSize, cancellationToken);

        return history is null
            ? Result<RecurringDeductionInstallmentHistoryResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<RecurringDeductionInstallmentHistoryResponse>.Success(history);
    }
}

// ── Unitary charge application (Manage — lock) ─────────────────────────────────────────────────────────

internal sealed class ApplyRecurringDeductionInstallmentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ApplyRecurringDeductionInstallmentCommand, RecurringDeductionInstallmentApplicationResult>
{
    public async Task<Result<RecurringDeductionInstallmentApplicationResult>> Handle(
        ApplyRecurringDeductionInstallmentCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageRecurringDeductionsAsync<RecurringDeductionInstallmentApplicationResult>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<RecurringDeductionInstallmentApplicationResult>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        // Pre-check existence + If-Match (AsNoTracking) so we never take the lock for a bad request.
        var snapshot = await employeeRepository.GetRecurringDeductionAsync(
            personnelFile.PublicId, command.RecurringDeductionPublicId, cancellationToken);
        if (snapshot is null)
        {
            return Result<RecurringDeductionInstallmentApplicationResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (snapshot.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<RecurringDeductionInstallmentApplicationResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var period = await ResolvePayrollPeriodAsync(
            employeeRepository, personnelFile.TenantId, command.PayrollPeriodPublicId, cancellationToken);
        if (period.Failed)
        {
            return Result<RecurringDeductionInstallmentApplicationResult>.Failure(RecurringDeductionInstallmentErrors.PayrollPeriodInvalid);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var appliedByUserId);
        var now = dateTimeProvider.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var appliedDate = command.AppliedDate ?? today;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Serialize the strict charge sequence: take the lock, then load fresh (the first tracking load of this
            // credit on the context, so it reflects any committed concurrent application).
            await employeeRepository.AcquireRecurringDeductionMutationLockAsync(command.RecurringDeductionPublicId, cancellationToken);
            var deduction = await employeeRepository.GetTrackedRecurringDeductionWithInstallmentsAsync(
                command.RecurringDeductionPublicId, personnelFile.TenantId, cancellationToken);
            if (deduction is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<RecurringDeductionInstallmentApplicationResult>.Failure(PersonnelFileErrors.ItemNotFound);
            }

            if (deduction.ConcurrencyToken != command.ConcurrencyToken)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<RecurringDeductionInstallmentApplicationResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
            }

            var plan = RecurringDeductionInstallmentSupport.ToPlan(deduction);
            var appliedNumbers = RecurringDeductionInstallmentSupport.ActiveAppliedNumbers(deduction);
            var nextNumber = RecurringDeductionRules.NextInstallmentNumber(appliedNumbers);

            if (RecurringDeductionRules.ChargeCount(plan, deduction.ApplicationFrequencyCode) is { } count && nextNumber > count)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<RecurringDeductionInstallmentApplicationResult>.Failure(RecurringDeductionInstallmentErrors.PlanComplete);
            }

            var canApply = RecurringDeductionRules.CanApplyCharge(
                deduction.StatusCode, deduction.EffectiveDate, today, nextNumber, plan, deduction.ApplicationFrequencyCode, appliedNumbers);
            if (!canApply.IsValid)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<RecurringDeductionInstallmentApplicationResult>.Failure(
                    new Error(canApply.ErrorCode!, "The installment cannot be applied.", ErrorType.UnprocessableEntity));
            }

            var (amount, capital, interest) = RecurringDeductionRules.ChargeSplitFor(nextNumber, plan, deduction.ApplicationFrequencyCode);
            var dueDate = RecurringDeductionRules.ChargeDueDateFor(
                deduction.ApplicationFrequencyCode, deduction.InstallmentStartDate, deduction.ExceptionMonths.ToHashSet(), nextNumber);

            var installment = deduction.ApplyInstallment(
                nextNumber,
                appliedDate,
                dueDate,
                amount,
                capital,
                interest,
                deduction.CurrencyCode,
                deduction.PayrollTypeCode,
                period.InternalId,
                period.Label,
                RecurringDeductionInstallmentOrigins.Manual,
                appliedByUserId,
                command.Notes);

            if (deduction.IsPlanComplete())
            {
                deduction.FinalizeByPlanCompletion(now);
            }

            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var result = new RecurringDeductionInstallmentApplicationResult(
                RecurringDeductionInstallmentMapping.ToResponse(installment, command.PayrollPeriodPublicId),
                deduction.StatusCode,
                deduction.ConcurrencyToken);

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile,
                $"Applied recurring-deduction installment #{nextNumber} for {personnelFile.FullName}.", result, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<RecurringDeductionInstallmentApplicationResult>.Success(result);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    internal static async Task<(bool Failed, long? InternalId, string? Label, DateOnly? EndDate)> ResolvePayrollPeriodAsync(
        IPersonnelFileEmployeeRepository employeeRepository,
        Guid tenantId,
        Guid? payrollPeriodPublicId,
        CancellationToken cancellationToken)
    {
        if (payrollPeriodPublicId is not { } publicId || publicId == Guid.Empty)
        {
            return (false, null, null, null);
        }

        var period = await employeeRepository.ResolveRecurringDeductionPayrollPeriodAsync(tenantId, publicId, cancellationToken);
        return period is null || !period.IsActive
            ? (true, null, null, null)
            : (false, period.InternalId, period.Label, period.EndDate);
    }
}

// ── Extraordinary payment (Manage — lock; payoff finalizes) ────────────────────────────────────────────

internal sealed class ApplyRecurringDeductionExtraordinaryCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ApplyRecurringDeductionExtraordinaryCommand, RecurringDeductionInstallmentApplicationResult>
{
    public async Task<Result<RecurringDeductionInstallmentApplicationResult>> Handle(
        ApplyRecurringDeductionExtraordinaryCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageRecurringDeductionsAsync<RecurringDeductionInstallmentApplicationResult>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<RecurringDeductionInstallmentApplicationResult>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var snapshot = await employeeRepository.GetRecurringDeductionAsync(
            personnelFile.PublicId, command.RecurringDeductionPublicId, cancellationToken);
        if (snapshot is null)
        {
            return Result<RecurringDeductionInstallmentApplicationResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (snapshot.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<RecurringDeductionInstallmentApplicationResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var period = await ApplyRecurringDeductionInstallmentCommandHandler.ResolvePayrollPeriodAsync(
            employeeRepository, personnelFile.TenantId, command.PayrollPeriodPublicId, cancellationToken);
        if (period.Failed)
        {
            return Result<RecurringDeductionInstallmentApplicationResult>.Failure(RecurringDeductionInstallmentErrors.PayrollPeriodInvalid);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var appliedByUserId);
        var now = dateTimeProvider.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var appliedDate = command.AppliedDate ?? today;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await employeeRepository.AcquireRecurringDeductionMutationLockAsync(command.RecurringDeductionPublicId, cancellationToken);
            var deduction = await employeeRepository.GetTrackedRecurringDeductionWithInstallmentsAsync(
                command.RecurringDeductionPublicId, personnelFile.TenantId, cancellationToken);
            if (deduction is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<RecurringDeductionInstallmentApplicationResult>.Failure(PersonnelFileErrors.ItemNotFound);
            }

            if (deduction.ConcurrencyToken != command.ConcurrencyToken)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<RecurringDeductionInstallmentApplicationResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
            }

            var canApply = RecurringDeductionRules.CanApplyExtraordinary(
                deduction.StatusCode,
                deduction.EffectiveDate,
                today,
                deduction.IsIndefinite,
                command.Amount,
                deduction.OutstandingBalance());
            if (!canApply.IsValid)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<RecurringDeductionInstallmentApplicationResult>.Failure(
                    new Error(canApply.ErrorCode!, "The extraordinary payment cannot be applied.", ErrorType.UnprocessableEntity));
            }

            var installment = deduction.ApplyExtraordinaryInstallment(
                appliedDate,
                command.Amount,
                deduction.CurrencyCode,
                deduction.PayrollTypeCode,
                period.InternalId,
                period.Label,
                RecurringDeductionInstallmentOrigins.Manual,
                appliedByUserId,
                command.Notes);

            // A payoff (the payment equals the outstanding balance) closes the credit in the SAME transaction; a
            // partial abono leaves it VIGENTE with a shorter remaining term (the schedule is derived, so the term
            // shortens by construction — nothing to rewrite, P-04).
            if (deduction.IsPlanComplete())
            {
                deduction.FinalizeByPlanCompletion(now);
            }

            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var result = new RecurringDeductionInstallmentApplicationResult(
                RecurringDeductionInstallmentMapping.ToResponse(installment, command.PayrollPeriodPublicId),
                deduction.StatusCode,
                deduction.ConcurrencyToken);

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile,
                $"Applied extraordinary recurring-deduction payment for {personnelFile.FullName}.", result, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<RecurringDeductionInstallmentApplicationResult>.Success(result);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

// ── Charge annulment (Manage — lock; reopens a FINALIZADO credit) ──────────────────────────────────────

internal sealed class AnnulRecurringDeductionInstallmentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AnnulRecurringDeductionInstallmentCommand, RecurringDeductionInstallmentApplicationResult>
{
    public async Task<Result<RecurringDeductionInstallmentApplicationResult>> Handle(
        AnnulRecurringDeductionInstallmentCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageRecurringDeductionsAsync<RecurringDeductionInstallmentApplicationResult>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<RecurringDeductionInstallmentApplicationResult>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<RecurringDeductionInstallmentApplicationResult>.Failure(RecurringDeductionInstallmentErrors.AnnulmentReasonRequired);
        }

        var snapshot = await employeeRepository.GetRecurringDeductionAsync(
            personnelFile.PublicId, command.RecurringDeductionPublicId, cancellationToken);
        if (snapshot is null)
        {
            return Result<RecurringDeductionInstallmentApplicationResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (snapshot.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<RecurringDeductionInstallmentApplicationResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var now = dateTimeProvider.UtcNow;
        _ = Guid.TryParse(currentUserService.UserId, out var byUserId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await employeeRepository.AcquireRecurringDeductionMutationLockAsync(command.RecurringDeductionPublicId, cancellationToken);
            var deduction = await employeeRepository.GetTrackedRecurringDeductionWithInstallmentsAsync(
                command.RecurringDeductionPublicId, personnelFile.TenantId, cancellationToken);
            if (deduction is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<RecurringDeductionInstallmentApplicationResult>.Failure(PersonnelFileErrors.ItemNotFound);
            }

            if (deduction.ConcurrencyToken != command.ConcurrencyToken)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<RecurringDeductionInstallmentApplicationResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
            }

            var target = deduction.Installments.SingleOrDefault(item =>
                item.PublicId == command.InstallmentPublicId
                && item.StatusCode == RecurringDeductionInstallmentStatuses.Aplicada);
            if (target is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<RecurringDeductionInstallmentApplicationResult>.Failure(RecurringDeductionInstallmentErrors.InstallmentNotFound);
            }

            // Annulling a charge that was completing the plan reopens FINALIZADO → VIGENTE (domain guard).
            deduction.AnnulInstallment(command.InstallmentPublicId, command.Reason, byUserId, now);

            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var periodPublicId = target.PayrollPeriodId is { } internalId
                ? await employeeRepository.ResolvePayrollPeriodPublicIdAsync(internalId, cancellationToken)
                : null;

            var result = new RecurringDeductionInstallmentApplicationResult(
                RecurringDeductionInstallmentMapping.ToResponse(target, periodPublicId),
                deduction.StatusCode,
                deduction.ConcurrencyToken);

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile,
                $"Annulled a recurring-deduction installment for {personnelFile.FullName}.", result, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<RecurringDeductionInstallmentApplicationResult>.Success(result);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

// ── Apply-period batch (Manage — atomic, ordered locks) ────────────────────────────────────────────────

internal sealed class ApplyRecurringDeductionPeriodCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ApplyRecurringDeductionPeriodCommand, RecurringDeductionApplyPeriodResult>
{
    private sealed record BatchTarget(long InternalId, Guid PublicId, IReadOnlyList<int> ChargeNumbers);

    public async Task<Result<RecurringDeductionApplyPeriodResult>> Handle(
        ApplyRecurringDeductionPeriodCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageRecurringDeductionsAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<RecurringDeductionApplyPeriodResult>.Failure(authorizationResult.Error);
        }

        var tenantId = command.CompanyId;
        var payrollTypeCode = command.PayrollTypeCode.Trim().ToUpperInvariant();

        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                tenantId, PersonnelCurriculumCatalogCategories.PayrollType, payrollTypeCode, cancellationToken))
        {
            return Result<RecurringDeductionApplyPeriodResult>.Failure(RecurringDeductionErrors.PayrollTypeInvalid);
        }

        long? payrollPeriodInternalId = null;
        string? payrollPeriodLabel = null;
        DateOnly cutoff;
        if (command.PayrollPeriodPublicId is { } payrollPeriodPublicId && payrollPeriodPublicId != Guid.Empty)
        {
            var period = await employeeRepository.ResolveRecurringDeductionPayrollPeriodAsync(tenantId, payrollPeriodPublicId, cancellationToken);
            if (period is null || !period.IsActive)
            {
                return Result<RecurringDeductionApplyPeriodResult>.Failure(RecurringDeductionInstallmentErrors.PayrollPeriodInvalid);
            }

            payrollPeriodInternalId = period.InternalId;
            payrollPeriodLabel = period.Label;
            cutoff = period.EndDate;
        }
        else
        {
            cutoff = command.CutoffDate!.Value;
        }

        var excluded = command.ExcludedDeductionPublicIds?.ToHashSet() ?? [];
        var scan = await employeeRepository.GetRecurringDeductionBatchScanAsync(tenantId, payrollTypeCode, cancellationToken);

        var now = dateTimeProvider.UtcNow;
        var today = DateOnly.FromDateTime(now);

        var targets = new List<BatchTarget>();
        var pospuestas = 0;
        foreach (var item in scan)
        {
            // A future-dated credit cannot be charged yet (D-04) — it is simply not a candidate.
            if (item.EffectiveDate > today)
            {
                continue;
            }

            var pending = RecurringDeductionInstallmentSupport.PendingChargesUpTo(item, cutoff);
            if (pending.Count == 0)
            {
                continue;
            }

            if (excluded.Contains(item.PublicId))
            {
                pospuestas++;
                continue;
            }

            targets.Add(new BatchTarget(item.InternalId, item.PublicId, pending));
        }

        if (targets.Count == 0)
        {
            return Result<RecurringDeductionApplyPeriodResult>.Success(new RecurringDeductionApplyPeriodResult(0, 0, pospuestas));
        }

        _ = Guid.TryParse(currentUserService.UserId, out var appliedByUserId);
        var appliedDate = today;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var aplicadas = 0;
            var finalizados = 0;

            // Ordered by internal id (the scan is id-ordered) → anti-deadlock. The batch is ATOMIC: any conflict
            // rolls the whole transaction back with the offending charge in the detail.
            foreach (var target in targets)
            {
                await employeeRepository.AcquireRecurringDeductionMutationLockAsync(target.PublicId, cancellationToken);
                var deduction = await employeeRepository.GetTrackedRecurringDeductionWithInstallmentsAsync(target.PublicId, tenantId, cancellationToken);
                if (deduction is null || deduction.StatusCode != RecurringDeductionStatuses.Vigente)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<RecurringDeductionApplyPeriodResult>.Failure(
                        RecurringDeductionInstallmentErrors.ApplyPeriodConflict($"deduction {target.PublicId} is no longer VIGENTE"));
                }

                var plan = RecurringDeductionInstallmentSupport.ToPlan(deduction);
                var exceptionMonths = deduction.ExceptionMonths.ToHashSet();

                foreach (var number in target.ChargeNumbers)
                {
                    var appliedNumbers = RecurringDeductionInstallmentSupport.ActiveAppliedNumbers(deduction);
                    var canApply = RecurringDeductionRules.CanApplyCharge(
                        deduction.StatusCode, deduction.EffectiveDate, today, number, plan, deduction.ApplicationFrequencyCode, appliedNumbers);
                    if (!canApply.IsValid)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<RecurringDeductionApplyPeriodResult>.Failure(
                            RecurringDeductionInstallmentErrors.ApplyPeriodConflict($"deduction {target.PublicId} installment {number}"));
                    }

                    var (amount, capital, interest) = RecurringDeductionRules.ChargeSplitFor(number, plan, deduction.ApplicationFrequencyCode);
                    var dueDate = RecurringDeductionRules.ChargeDueDateFor(
                        deduction.ApplicationFrequencyCode, deduction.InstallmentStartDate, exceptionMonths, number);

                    _ = deduction.ApplyInstallment(
                        number,
                        appliedDate,
                        dueDate,
                        amount,
                        capital,
                        interest,
                        deduction.CurrencyCode,
                        deduction.PayrollTypeCode,
                        payrollPeriodInternalId,
                        payrollPeriodLabel,
                        RecurringDeductionInstallmentOrigins.Manual,
                        appliedByUserId,
                        notes: null);
                    aplicadas++;
                }

                if (deduction.IsPlanComplete())
                {
                    deduction.FinalizeByPlanCompletion(now);
                    finalizados++;
                }
            }

            var summary = new RecurringDeductionApplyPeriodResult(aplicadas, finalizados, pospuestas);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.RecurringDeductionInstallmentsApplied,
                    AuditEntityTypes.PersonnelFile,
                    command.CompanyId,
                    $"RECURRING_DEDUCTION_APPLY_PERIOD_{payrollTypeCode}",
                    AuditActions.Update,
                    $"Applied {aplicadas} recurring-deduction installment(s) for payroll type {payrollTypeCode} ({finalizados} finalized, {pospuestas} postponed).",
                    After: summary),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<RecurringDeductionApplyPeriodResult>.Success(summary);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
