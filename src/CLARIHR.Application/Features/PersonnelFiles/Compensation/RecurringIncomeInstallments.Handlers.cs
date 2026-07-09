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

/// <summary>Maps an installment entity to its API response (user ids null-safe; payroll-period public id supplied).</summary>
internal static class RecurringIncomeInstallmentMapping
{
    public static RecurringIncomeInstallmentResponse ToResponse(
        PersonnelFileRecurringIncomeInstallment entity, Guid? payrollPeriodPublicId) =>
        new(
            entity.PublicId,
            entity.InstallmentNumber,
            entity.AppliedDate,
            entity.TheoreticalDueDate,
            entity.Amount,
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
/// Shared pure arithmetic for turning a recurring income's plan into the applied numbers set / next candidate
/// installments, so the installment handlers and the apply-period batch all cuadran with
/// <see cref="RecurringIncomeRules"/> by construction.
/// </summary>
internal static class RecurringIncomeInstallmentSupport
{
    public static HashSet<int> ActiveAppliedNumbers(PersonnelFileRecurringIncome income) =>
        income.Installments
            .Where(installment => installment.StatusCode == RecurringIncomeInstallmentStatuses.Aplicada)
            .Select(installment => installment.InstallmentNumber)
            .ToHashSet();

    public static RecurringIncomePlan ToPlan(PersonnelFileRecurringIncome income) =>
        new(income.InstallmentValue, income.InstallmentCount, income.TotalAmount, income.IsIndefinite);

    /// <summary>
    /// The installment numbers of <paramref name="scan"/> whose theoretical due date is on/before
    /// <paramref name="cutoff"/> and are not yet applied (ascending). Includes overdue ones (due &lt; today)
    /// — the batch catches up on postponed installments. A finite plan is bounded by its count; an indefinite
    /// plan iterates until the due date passes the cutoff (bounded defensively).
    /// </summary>
    public static IReadOnlyList<int> PendingInstallmentsUpTo(RecurringIncomeBatchScanItem scan, DateOnly cutoff)
    {
        var applied = scan.AppliedInstallmentNumbers.ToHashSet();
        var pending = new List<int>();

        if (!scan.IsIndefinite && scan.InstallmentCount is { } count)
        {
            for (var number = 1; number <= count; number++)
            {
                if (applied.Contains(number))
                {
                    continue;
                }

                if (RecurringIncomeRules.TheoreticalDueDateFor(scan.InstallmentFrequencyCode, scan.InstallmentStartDate, number) <= cutoff)
                {
                    pending.Add(number);
                }
            }

            return pending;
        }

        // Indefinite: iterate installments while their theoretical due date is within the cutoff (capped).
        const int indefiniteCap = 1200;
        for (var number = 1; number <= indefiniteCap; number++)
        {
            var due = RecurringIncomeRules.TheoreticalDueDateFor(scan.InstallmentFrequencyCode, scan.InstallmentStartDate, number);
            if (due > cutoff)
            {
                break;
            }

            if (!applied.Contains(number))
            {
                pending.Add(number);
            }

            // UNICA collapses every number onto the start date; there is only one installment to consider.
            if (scan.InstallmentFrequencyCode.Trim().ToUpperInvariant() == RecurringIncomeFrequencies.Unica)
            {
                break;
            }
        }

        return pending;
    }
}

// ── Reads (View — schedule + history) ─────────────────────────────────────────────────────────────────

internal sealed class GetRecurringIncomeScheduleQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetRecurringIncomeScheduleQuery, RecurringIncomeScheduleResponse>
{
    public async Task<Result<RecurringIncomeScheduleResponse>> Handle(
        GetRecurringIncomeScheduleQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForRecurringIncomeReadAsync<RecurringIncomeScheduleResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var data = await employeeRepository.GetRecurringIncomeScheduleDataAsync(
            personnelFile!.PublicId, query.RecurringIncomePublicId, cancellationToken);
        if (data is null)
        {
            return Result<RecurringIncomeScheduleResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var plan = new RecurringIncomePlan(data.InstallmentValue, data.InstallmentCount, data.TotalAmount, data.IsIndefinite);
        var appliedNumbers = data.AppliedInstallmentNumbers.ToHashSet();
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow);

        var projection = RecurringIncomeRules.BuildProjection(
            plan, data.InstallmentFrequencyCode, data.InstallmentStartDate, appliedNumbers, today);

        var items = projection
            .Select(item => new RecurringIncomeScheduleItemResponse(
                item.InstallmentNumber, item.TheoreticalDueDate, item.Amount, item.IsApplied, item.IsOverdue))
            .ToArray();

        var response = new RecurringIncomeScheduleResponse(
            data.PublicId,
            data.StatusCode,
            data.IsIndefinite,
            data.InstallmentFrequencyCode,
            data.InstallmentStartDate,
            data.InstallmentValue,
            data.InstallmentCount,
            data.TotalAmount,
            RecurringIncomeRules.RemainingAmount(plan, appliedNumbers),
            RecurringIncomeRules.IsPlanComplete(plan, appliedNumbers),
            RecurringIncomeRules.NextInstallmentNumber(appliedNumbers),
            items);

        return Result<RecurringIncomeScheduleResponse>.Success(response);
    }
}

internal sealed class GetRecurringIncomeInstallmentsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetRecurringIncomeInstallmentsQuery, RecurringIncomeInstallmentHistoryResponse>
{
    public async Task<Result<RecurringIncomeInstallmentHistoryResponse>> Handle(
        GetRecurringIncomeInstallmentsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForRecurringIncomeReadAsync<RecurringIncomeInstallmentHistoryResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetRecurringIncomeInstallmentHistoryAsync(
            personnelFile!.PublicId, query.RecurringIncomePublicId, query.PageNumber, query.PageSize, cancellationToken);
        return response is null
            ? Result<RecurringIncomeInstallmentHistoryResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<RecurringIncomeInstallmentHistoryResponse>.Success(response);
    }
}

// ── Unitary installment application (Manage — lock) ────────────────────────────────────────────────────

internal sealed class ApplyRecurringIncomeInstallmentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ApplyRecurringIncomeInstallmentCommand, RecurringIncomeInstallmentApplicationResult>
{
    public async Task<Result<RecurringIncomeInstallmentApplicationResult>> Handle(
        ApplyRecurringIncomeInstallmentCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageRecurringIncomesAsync<RecurringIncomeInstallmentApplicationResult>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<RecurringIncomeInstallmentApplicationResult>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        if (await employeeRepository.IsRecurringIncomeProfileRetiredAsync(personnelFile.Id, cancellationToken))
        {
            return Result<RecurringIncomeInstallmentApplicationResult>.Failure(PersonnelFileErrors.ProfileRetiredLocked);
        }

        // Pre-check existence + If-Match (AsNoTracking) so we never take the lock for a bad request.
        var snapshot = await employeeRepository.GetRecurringIncomeAsync(personnelFile.PublicId, command.RecurringIncomePublicId, cancellationToken);
        if (snapshot is null)
        {
            return Result<RecurringIncomeInstallmentApplicationResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (snapshot.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<RecurringIncomeInstallmentApplicationResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        long? payrollPeriodInternalId = null;
        string? payrollPeriodLabel = null;
        if (command.PayrollPeriodPublicId is { } payrollPeriodPublicId && payrollPeriodPublicId != Guid.Empty)
        {
            var period = await employeeRepository.ResolveRecurringIncomePayrollPeriodAsync(
                personnelFile.TenantId, payrollPeriodPublicId, cancellationToken);
            if (period is null || !period.IsActive)
            {
                return Result<RecurringIncomeInstallmentApplicationResult>.Failure(RecurringIncomeInstallmentErrors.PayrollPeriodInvalid);
            }

            payrollPeriodInternalId = period.InternalId;
            payrollPeriodLabel = period.Label;
        }

        _ = Guid.TryParse(currentUserService.UserId, out var appliedByUserId);
        var now = dateTimeProvider.UtcNow;
        var appliedDate = command.AppliedDate ?? DateOnly.FromDateTime(now);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Serialize the strict installment sequence: acquire the lock, then load fresh (the first tracking
            // load of this income on the context, so it reflects any committed concurrent application).
            await employeeRepository.AcquireRecurringIncomeMutationLockAsync(command.RecurringIncomePublicId, cancellationToken);
            var income = await employeeRepository.GetTrackedRecurringIncomeWithInstallmentsAsync(
                command.RecurringIncomePublicId, personnelFile.TenantId, cancellationToken);
            if (income is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<RecurringIncomeInstallmentApplicationResult>.Failure(PersonnelFileErrors.ItemNotFound);
            }

            if (income.ConcurrencyToken != command.ConcurrencyToken)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<RecurringIncomeInstallmentApplicationResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
            }

            if (income.StatusCode != RecurringIncomeStatuses.Vigente)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<RecurringIncomeInstallmentApplicationResult>.Failure(RecurringIncomeInstallmentErrors.NotApplicable);
            }

            var plan = RecurringIncomeInstallmentSupport.ToPlan(income);
            var appliedNumbers = RecurringIncomeInstallmentSupport.ActiveAppliedNumbers(income);
            var nextNumber = RecurringIncomeRules.NextInstallmentNumber(appliedNumbers);

            if (!income.IsIndefinite && income.InstallmentCount is { } count && nextNumber > count)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<RecurringIncomeInstallmentApplicationResult>.Failure(RecurringIncomeInstallmentErrors.PlanComplete);
            }

            var canApply = RecurringIncomeRules.CanApplyInstallment(income.StatusCode, nextNumber, plan, appliedNumbers);
            if (!canApply.IsValid)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<RecurringIncomeInstallmentApplicationResult>.Failure(
                    new Error(canApply.ErrorCode!, "The installment cannot be applied.", ErrorType.UnprocessableEntity));
            }

            var amount = RecurringIncomeRules.InstallmentAmountFor(nextNumber, plan);
            var dueDate = RecurringIncomeRules.TheoreticalDueDateFor(income.InstallmentFrequencyCode, income.InstallmentStartDate, nextNumber);

            var installment = income.ApplyInstallment(
                nextNumber,
                appliedDate,
                dueDate,
                amount,
                income.CurrencyCode,
                income.PayrollTypeCode,
                payrollPeriodInternalId,
                payrollPeriodLabel,
                RecurringIncomeInstallmentOrigins.Manual,
                appliedByUserId,
                command.Notes);

            appliedNumbers.Add(nextNumber);
            if (!income.IsIndefinite && RecurringIncomeRules.IsPlanComplete(plan, appliedNumbers))
            {
                income.FinalizeByPlanCompletion(now);
            }

            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var result = new RecurringIncomeInstallmentApplicationResult(
                RecurringIncomeInstallmentMapping.ToResponse(installment, command.PayrollPeriodPublicId),
                income.StatusCode,
                income.ConcurrencyToken);

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile,
                $"Applied recurring-income installment #{nextNumber} for {personnelFile.FullName}.", result, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<RecurringIncomeInstallmentApplicationResult>.Success(result);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

// ── Unitary installment annulment (Manage — lock) ──────────────────────────────────────────────────────

internal sealed class AnnulRecurringIncomeInstallmentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AnnulRecurringIncomeInstallmentCommand, RecurringIncomeInstallmentApplicationResult>
{
    public async Task<Result<RecurringIncomeInstallmentApplicationResult>> Handle(
        AnnulRecurringIncomeInstallmentCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageRecurringIncomesAsync<RecurringIncomeInstallmentApplicationResult>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<RecurringIncomeInstallmentApplicationResult>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<RecurringIncomeInstallmentApplicationResult>.Failure(RecurringIncomeInstallmentErrors.AnnulmentReasonRequired);
        }

        var snapshot = await employeeRepository.GetRecurringIncomeAsync(personnelFile.PublicId, command.RecurringIncomePublicId, cancellationToken);
        if (snapshot is null)
        {
            return Result<RecurringIncomeInstallmentApplicationResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (snapshot.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<RecurringIncomeInstallmentApplicationResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var byUserId);
        var now = dateTimeProvider.UtcNow;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await employeeRepository.AcquireRecurringIncomeMutationLockAsync(command.RecurringIncomePublicId, cancellationToken);
            var income = await employeeRepository.GetTrackedRecurringIncomeWithInstallmentsAsync(
                command.RecurringIncomePublicId, personnelFile.TenantId, cancellationToken);
            if (income is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<RecurringIncomeInstallmentApplicationResult>.Failure(PersonnelFileErrors.ItemNotFound);
            }

            if (income.ConcurrencyToken != command.ConcurrencyToken)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<RecurringIncomeInstallmentApplicationResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
            }

            var installment = income.Installments.FirstOrDefault(item =>
                item.PublicId == command.InstallmentPublicId
                && item.StatusCode == RecurringIncomeInstallmentStatuses.Aplicada);
            if (installment is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<RecurringIncomeInstallmentApplicationResult>.Failure(RecurringIncomeInstallmentErrors.InstallmentNotFound);
            }

            income.AnnulInstallment(command.InstallmentPublicId, command.Reason, byUserId, now);

            var payrollPeriodPublicId = installment.PayrollPeriodId is { } internalId
                ? await employeeRepository.ResolvePayrollPeriodPublicIdAsync(internalId, cancellationToken)
                : null;

            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var result = new RecurringIncomeInstallmentApplicationResult(
                RecurringIncomeInstallmentMapping.ToResponse(installment, payrollPeriodPublicId),
                income.StatusCode,
                income.ConcurrencyToken);

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile,
                $"Annulled recurring-income installment #{installment.InstallmentNumber} for {personnelFile.FullName}.", result, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<RecurringIncomeInstallmentApplicationResult>.Success(result);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

// ── Apply-period batch (company-scoped, Manage per-handler, ordered locks, atomic) ─────────────────────

internal sealed class ApplyRecurringIncomePeriodCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ApplyRecurringIncomePeriodCommand, RecurringIncomeApplyPeriodResult>
{
    private sealed record BatchTarget(long InternalId, Guid PublicId, IReadOnlyList<int> InstallmentNumbers);

    public async Task<Result<RecurringIncomeApplyPeriodResult>> Handle(
        ApplyRecurringIncomePeriodCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageRecurringIncomesAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<RecurringIncomeApplyPeriodResult>.Failure(authorizationResult.Error);
        }

        var tenantId = command.CompanyId;
        var payrollTypeCode = command.PayrollTypeCode.Trim().ToUpperInvariant();

        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                tenantId, PersonnelCurriculumCatalogCategories.PayrollType, payrollTypeCode, cancellationToken))
        {
            return Result<RecurringIncomeApplyPeriodResult>.Failure(RecurringIncomeErrors.PayrollTypeInvalid);
        }

        long? payrollPeriodInternalId = null;
        string? payrollPeriodLabel = null;
        DateOnly cutoff;
        if (command.PayrollPeriodPublicId is { } payrollPeriodPublicId && payrollPeriodPublicId != Guid.Empty)
        {
            var period = await employeeRepository.ResolveRecurringIncomePayrollPeriodAsync(tenantId, payrollPeriodPublicId, cancellationToken);
            if (period is null || !period.IsActive)
            {
                return Result<RecurringIncomeApplyPeriodResult>.Failure(RecurringIncomeInstallmentErrors.PayrollPeriodInvalid);
            }

            payrollPeriodInternalId = period.InternalId;
            payrollPeriodLabel = period.Label;
            cutoff = period.EndDate;
        }
        else
        {
            cutoff = command.CutoffDate!.Value;
        }

        var excluded = command.ExcludedIncomePublicIds?.ToHashSet() ?? [];
        var scan = await employeeRepository.GetRecurringIncomeBatchScanAsync(tenantId, payrollTypeCode, cancellationToken);

        var targets = new List<BatchTarget>();
        var pospuestas = 0;
        foreach (var item in scan)
        {
            var pending = RecurringIncomeInstallmentSupport.PendingInstallmentsUpTo(item, cutoff);
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
            return Result<RecurringIncomeApplyPeriodResult>.Success(new RecurringIncomeApplyPeriodResult(0, 0, pospuestas));
        }

        _ = Guid.TryParse(currentUserService.UserId, out var appliedByUserId);
        var now = dateTimeProvider.UtcNow;
        var appliedDate = DateOnly.FromDateTime(now);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var aplicadas = 0;
            var finalizados = 0;

            // Ordered by internal id (targets already come from an id-ordered scan) → anti-deadlock. The batch is
            // atomic: any conflict rolls the whole transaction back with the offending installment in the detail.
            foreach (var target in targets)
            {
                await employeeRepository.AcquireRecurringIncomeMutationLockAsync(target.PublicId, cancellationToken);
                var income = await employeeRepository.GetTrackedRecurringIncomeWithInstallmentsAsync(target.PublicId, tenantId, cancellationToken);
                if (income is null || income.StatusCode != RecurringIncomeStatuses.Vigente)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<RecurringIncomeApplyPeriodResult>.Failure(
                        RecurringIncomeInstallmentErrors.ApplyPeriodConflict($"income {target.PublicId} is no longer VIGENTE"));
                }

                var plan = RecurringIncomeInstallmentSupport.ToPlan(income);
                foreach (var number in target.InstallmentNumbers)
                {
                    var appliedNumbers = RecurringIncomeInstallmentSupport.ActiveAppliedNumbers(income);
                    var canApply = RecurringIncomeRules.CanApplyInstallment(income.StatusCode, number, plan, appliedNumbers);
                    if (!canApply.IsValid)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<RecurringIncomeApplyPeriodResult>.Failure(
                            RecurringIncomeInstallmentErrors.ApplyPeriodConflict($"income {target.PublicId} installment {number}"));
                    }

                    var amount = RecurringIncomeRules.InstallmentAmountFor(number, plan);
                    var dueDate = RecurringIncomeRules.TheoreticalDueDateFor(income.InstallmentFrequencyCode, income.InstallmentStartDate, number);

                    _ = income.ApplyInstallment(
                        number,
                        appliedDate,
                        dueDate,
                        amount,
                        income.CurrencyCode,
                        income.PayrollTypeCode,
                        payrollPeriodInternalId,
                        payrollPeriodLabel,
                        RecurringIncomeInstallmentOrigins.Manual,
                        appliedByUserId,
                        notes: null);
                    aplicadas++;
                }

                if (!income.IsIndefinite && RecurringIncomeRules.IsPlanComplete(plan, RecurringIncomeInstallmentSupport.ActiveAppliedNumbers(income)))
                {
                    income.FinalizeByPlanCompletion(now);
                    finalizados++;
                }
            }

            var summary = new RecurringIncomeApplyPeriodResult(aplicadas, finalizados, pospuestas);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.RecurringIncomeInstallmentsApplied,
                    AuditEntityTypes.PersonnelFile,
                    command.CompanyId,
                    $"RECURRING_INCOME_APPLY_PERIOD_{payrollTypeCode}",
                    AuditActions.Update,
                    $"Applied {aplicadas} recurring-income installment(s) for payroll type {payrollTypeCode} ({finalizados} finalized, {pospuestas} postponed).",
                    After: summary),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<RecurringIncomeApplyPeriodResult>.Success(summary);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
