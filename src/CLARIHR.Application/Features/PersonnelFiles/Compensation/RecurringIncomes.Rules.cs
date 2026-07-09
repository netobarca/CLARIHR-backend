using CLARIHR.Application.Common.Errors;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles.Compensation;

/// <summary>
/// Dedicated handler-level errors for recurring incomes (REQ-005 §3.3 CRUD + resolution). Each code requires an
/// EN + ES resource entry (parity: <c>BackendMessageLocalizationTests</c>). The plan-coherence / settlement-action
/// / state-transition codes are produced by the pure <see cref="RecurringIncomeRules"/> and already localized;
/// these cover the cross-aggregate (catalog / plaza / cost-center) checks and the decision-flow guards that need
/// a database or the request context. Field-level validation (required codes, positive value, plan shape) is the
/// validator's job (400) and is NOT here.
/// </summary>
internal static class RecurringIncomeErrors
{
    public static readonly Error TypeInvalid = new(
        "RECURRING_INCOME_TYPE_INVALID",
        "The recurring-income type is not valid for the active catalog.", ErrorType.UnprocessableEntity);

    public static readonly Error ConceptInvalid = new(
        "RECURRING_INCOME_CONCEPT_INVALID",
        "The compensation concept is not a valid active income concept for the company's country.", ErrorType.UnprocessableEntity);

    public static readonly Error PayrollTypeInvalid = new(
        "RECURRING_INCOME_PAYROLL_TYPE_INVALID",
        "The payroll type is not valid for the active catalog.", ErrorType.UnprocessableEntity);

    public static readonly Error FrequencyInvalid = new(
        "RECURRING_INCOME_FREQUENCY_INVALID",
        "The installment frequency is not valid for the active pay-period catalog.", ErrorType.UnprocessableEntity);

    public static readonly Error CostCenterMissing = new(
        "RECURRING_INCOME_COST_CENTER_MISSING",
        "The assigned position has no cost center configured; a cost center is required for a recurring income.", ErrorType.UnprocessableEntity);

    public static readonly Error AssignedPositionInvalid = new(
        "RECURRING_INCOME_ASSIGNED_POSITION_INVALID",
        "The assigned position is not a valid plaza of this employee.", ErrorType.UnprocessableEntity);

    public static readonly Error RegistrationDateInFuture = new(
        "RECURRING_INCOME_REGISTRATION_DATE_IN_FUTURE",
        "The registration date cannot be in the future.", ErrorType.UnprocessableEntity);

    public static readonly Error StatusInvalid = new(
        "RECURRING_INCOME_STATUS_INVALID",
        "The target status is not a valid resolution target for the active catalog.", ErrorType.UnprocessableEntity);

    public static readonly Error DecisionNoteRequired = new(
        "RECURRING_INCOME_DECISION_NOTE_REQUIRED",
        "A decision note is required to reject a recurring income.", ErrorType.UnprocessableEntity);

    public static readonly Error AnnulmentReasonRequired = new(
        "RECURRING_INCOME_ANNULMENT_REASON_REQUIRED",
        "An annulment reason is required.", ErrorType.UnprocessableEntity);

    public static readonly Error ClosureReasonRequired = new(
        "RECURRING_INCOME_CLOSURE_REASON_REQUIRED",
        "A closure reason is required to close a recurring income manually.", ErrorType.UnprocessableEntity);

    // Shares the code the pure rules already localize (RN-01/RN-02); the handler pre-checks the state before the
    // domain mutator so an invalid transition returns a clean 422 instead of a 500.
    public static readonly Error StateRuleViolation = new(
        RecurringIncomeRules.StateRuleViolationCode,
        "The recurring income is not in a state that allows this operation.", ErrorType.UnprocessableEntity);

    // Separation of duties (double anti-self, aclaración №6): neither the subject employee nor the registrar
    // may decide/revoke the recurring income. 403 (Forbidden).
    public static readonly Error SelfApprovalForbidden = new(
        "RECURRING_INCOME_SELF_APPROVAL_FORBIDDEN",
        "The subject employee or the registrar cannot decide or revoke the recurring income.", ErrorType.Forbidden);
}

/// <summary>
/// The normalized installment plan of a recurring income. For a finite plan BOTH <see cref="InstallmentCount"/>
/// and <see cref="TotalAmount"/> are resolved (the missing one is derived from the value); for an indefinite
/// plan both are <c>null</c>.
/// </summary>
public sealed record RecurringIncomePlan(
    decimal InstallmentValue,
    int? InstallmentCount,
    decimal? TotalAmount,
    bool IsIndefinite);

/// <summary>Result of <see cref="RecurringIncomeRules.NormalizePlan"/>: the resolved plan, or an error code.</summary>
public sealed record RecurringIncomePlanNormalization(bool IsValid, string? ErrorCode, RecurringIncomePlan? Plan)
{
    public static RecurringIncomePlanNormalization Failure(string errorCode) => new(false, errorCode, null);

    public static RecurringIncomePlanNormalization Success(RecurringIncomePlan plan) => new(true, null, plan);
}

/// <summary>A generic pass/fail rule outcome carrying the bilingual <c>extensions.code</c> when it fails.</summary>
public readonly record struct RecurringIncomeRuleResult(bool IsValid, string? ErrorCode)
{
    public static RecurringIncomeRuleResult Ok { get; } = new(true, null);

    public static RecurringIncomeRuleResult Fail(string errorCode) => new(false, errorCode);
}

/// <summary>One projected (theoretical) installment of the plan — a derivation, never persisted (D-07).</summary>
public sealed record RecurringIncomeProjectedInstallment(
    int InstallmentNumber,
    DateOnly TheoreticalDueDate,
    decimal Amount,
    bool IsApplied,
    bool IsOverdue);

/// <summary>
/// The recurring-income plan + lifecycle arithmetic (REQ-005 §3.2, golden suite A.3) — 100% pure and
/// deterministic: no clock, no database, no side-effects. This is the SINGLE source of truth for the plan
/// coherence, the per-installment amount (with the last-installment rounding adjustment), the theoretical
/// projection, the remaining balance / completion and the state-machine transitions, so the schedule endpoint,
/// the installment application, the settlement suggestion and the write validations all cuadran by
/// construction.
/// <para>Single rounding rule (mirrors <c>SettlementCalculationRules.Round2</c>): half-up away-from-zero,
/// 2 decimals — the ONLY rounding point of the module.</para>
/// </summary>
public static class RecurringIncomeRules
{
    // ── Plan-coherence error codes (bilingual; localization parity kept in BackendMessages[.es].resx) ──
    public const string PlanValueInvalidCode = "RECURRING_INCOME_PLAN_VALUE_INVALID";
    public const string PlanIndefiniteWithLimitsCode = "RECURRING_INCOME_PLAN_INDEFINITE_WITH_LIMITS";
    public const string PlanFiniteWithoutLimitsCode = "RECURRING_INCOME_PLAN_FINITE_WITHOUT_LIMITS";
    public const string PlanCountInvalidCode = "RECURRING_INCOME_PLAN_COUNT_INVALID";
    public const string PlanTotalInvalidCode = "RECURRING_INCOME_PLAN_TOTAL_INVALID";
    public const string PlanIncoherentCode = "RECURRING_INCOME_PLAN_INCOHERENT";

    // ── Settlement-action + installment + transition error codes ──────────────────────────────────────
    public const string SettlementActionIndefiniteCode = "RECURRING_INCOME_SETTLEMENT_ACTION_INDEFINITE";
    public const string InstallmentNotApplicableCode = "RECURRING_INCOME_INSTALLMENT_NOT_APPLICABLE";
    public const string InstallmentSequenceInvalidCode = "RECURRING_INCOME_INSTALLMENT_SEQUENCE_INVALID";
    public const string InstallmentExceedsPlanCode = "RECURRING_INCOME_INSTALLMENT_EXCEEDS_PLAN";
    public const string StateRuleViolationCode = "RECURRING_INCOME_STATE_RULE_VIOLATION";

    /// <summary>Single rounding rule of the module: half-up away-from-zero, 2 decimals.</summary>
    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Normalizes and validates the plan of a recurring income (RN-05). The installment value is always
    /// required and positive. An indefinite plan must carry neither a count nor a total; a finite plan must
    /// carry at least one of them and the rules derive the missing one:
    /// <list type="bullet">
    /// <item>value + count → total = <c>Round2(value × count)</c>.</item>
    /// <item>value + total → count = the rounded number of installments that fit the total (the last one
    /// absorbs the rounding remainder).</item>
    /// <item>value + count + total → coherence is verified (the last installment must stay positive).</item>
    /// </list>
    /// </summary>
    public static RecurringIncomePlanNormalization NormalizePlan(
        decimal installmentValue,
        int? installmentCount,
        decimal? totalAmount,
        bool isIndefinite)
    {
        if (installmentValue <= 0m)
        {
            return RecurringIncomePlanNormalization.Failure(PlanValueInvalidCode);
        }

        if (isIndefinite)
        {
            if (installmentCount is not null || totalAmount is not null)
            {
                return RecurringIncomePlanNormalization.Failure(PlanIndefiniteWithLimitsCode);
            }

            return RecurringIncomePlanNormalization.Success(
                new RecurringIncomePlan(Round2(installmentValue), null, null, IsIndefinite: true));
        }

        if (installmentCount is null && totalAmount is null)
        {
            return RecurringIncomePlanNormalization.Failure(PlanFiniteWithoutLimitsCode);
        }

        if (installmentCount is { } providedCount && providedCount < 1)
        {
            return RecurringIncomePlanNormalization.Failure(PlanCountInvalidCode);
        }

        if (totalAmount is { } providedTotal && providedTotal <= 0m)
        {
            return RecurringIncomePlanNormalization.Failure(PlanTotalInvalidCode);
        }

        var value = Round2(installmentValue);
        int resolvedCount;
        decimal resolvedTotal;

        if (installmentCount is { } count && totalAmount is { } total)
        {
            resolvedCount = count;
            resolvedTotal = Round2(total);

            // Coherence: the last installment (total − value×(count−1)) must stay strictly positive.
            var lastInstallment = resolvedTotal - (value * (resolvedCount - 1));
            if (lastInstallment <= 0m)
            {
                return RecurringIncomePlanNormalization.Failure(PlanIncoherentCode);
            }
        }
        else if (installmentCount is { } onlyCount)
        {
            resolvedCount = onlyCount;
            resolvedTotal = Round2(value * onlyCount);
        }
        else
        {
            // Only the total is known: the count is the rounded number of installments that fit it.
            resolvedTotal = Round2(totalAmount!.Value);
            resolvedCount = (int)Math.Round(resolvedTotal / value, MidpointRounding.AwayFromZero);
            if (resolvedCount < 1)
            {
                resolvedCount = 1;
            }
        }

        return RecurringIncomePlanNormalization.Success(
            new RecurringIncomePlan(value, resolvedCount, resolvedTotal, IsIndefinite: false));
    }

    /// <summary>
    /// The amount of installment <paramref name="installmentNumber"/> (1-based). Every installment equals the
    /// plan value except the last of a finite plan, which absorbs the rounding remainder
    /// (<c>total − value×(count−1)</c>).
    /// </summary>
    public static decimal InstallmentAmountFor(int installmentNumber, RecurringIncomePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (installmentNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(installmentNumber), "The installment number must be greater than or equal to one.");
        }

        var value = Round2(plan.InstallmentValue);

        if (plan.IsIndefinite || plan.InstallmentCount is not { } count || plan.TotalAmount is not { } total)
        {
            return value;
        }

        if (installmentNumber > count)
        {
            throw new ArgumentOutOfRangeException(nameof(installmentNumber), "The installment number cannot exceed the finite plan count.");
        }

        return installmentNumber == count
            ? Round2(total - (value * (count - 1)))
            : value;
    }

    /// <summary>
    /// Builds the theoretical projection of the plan (D-07 — derived, never persisted). The due date of
    /// installment <c>n</c> is <c>start + frequency×(n−1)</c> (MENSUAL = +1 month, QUINCENAL = +15 days,
    /// SEMANAL = +7 days, UNICA = a single installment). For a finite plan the projection covers all its
    /// installments; for an indefinite plan it covers <paramref name="indefiniteHorizon"/> installments from
    /// the current one. An unapplied installment whose due date is before <paramref name="today"/> is overdue.
    /// </summary>
    public static IReadOnlyList<RecurringIncomeProjectedInstallment> BuildProjection(
        RecurringIncomePlan plan,
        string frequencyCode,
        DateOnly installmentStartDate,
        IReadOnlySet<int> appliedNumbers,
        DateOnly today,
        int indefiniteHorizon = 12)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(appliedNumbers);

        var normalizedFrequency = (frequencyCode ?? string.Empty).Trim().ToUpperInvariant();

        int projectionCount;
        if (normalizedFrequency == RecurringIncomeFrequencies.Unica)
        {
            projectionCount = 1;
        }
        else if (!plan.IsIndefinite && plan.InstallmentCount is { } count)
        {
            projectionCount = count;
        }
        else
        {
            var appliedCeiling = appliedNumbers.Count == 0 ? 0 : appliedNumbers.Max();
            projectionCount = Math.Max(indefiniteHorizon, appliedCeiling);
        }

        var projection = new List<RecurringIncomeProjectedInstallment>(projectionCount);
        for (var number = 1; number <= projectionCount; number++)
        {
            var dueDate = DueDateFor(normalizedFrequency, installmentStartDate, number);
            var isApplied = appliedNumbers.Contains(number);
            var amount = plan.IsIndefinite
                ? Round2(plan.InstallmentValue)
                : InstallmentAmountFor(number, plan);
            var isOverdue = !isApplied && dueDate < today;

            projection.Add(new RecurringIncomeProjectedInstallment(number, dueDate, amount, isApplied, isOverdue));
        }

        return projection;
    }

    /// <summary>The next expected installment number: the smallest positive integer not already applied.</summary>
    public static int NextInstallmentNumber(IReadOnlySet<int> appliedNumbers)
    {
        ArgumentNullException.ThrowIfNull(appliedNumbers);

        var next = 1;
        while (appliedNumbers.Contains(next))
        {
            next++;
        }

        return next;
    }

    /// <summary>
    /// Validates that installment <paramref name="installmentNumber"/> may be applied now (RN-08): the income
    /// must be VIGENTE, the number must be the next expected one (strict sequence) and — for a finite plan —
    /// must not exceed the installment count.
    /// </summary>
    public static RecurringIncomeRuleResult CanApplyInstallment(
        string statusCode,
        int installmentNumber,
        RecurringIncomePlan plan,
        IReadOnlySet<int> appliedNumbers)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(appliedNumbers);

        if (statusCode != RecurringIncomeStatuses.Vigente)
        {
            return RecurringIncomeRuleResult.Fail(InstallmentNotApplicableCode);
        }

        if (installmentNumber != NextInstallmentNumber(appliedNumbers))
        {
            return RecurringIncomeRuleResult.Fail(InstallmentSequenceInvalidCode);
        }

        if (!plan.IsIndefinite && plan.InstallmentCount is { } count && installmentNumber > count)
        {
            return RecurringIncomeRuleResult.Fail(InstallmentExceedsPlanCode);
        }

        return RecurringIncomeRuleResult.Ok;
    }

    /// <summary>
    /// The outstanding amount of a finite plan (<c>total − Σ applied installment amounts</c>); <c>null</c> for
    /// an indefinite plan (there is no balance to pay, P-06).
    /// </summary>
    public static decimal? RemainingAmount(RecurringIncomePlan plan, IReadOnlySet<int> appliedNumbers)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(appliedNumbers);

        if (plan.IsIndefinite || plan.TotalAmount is not { } total)
        {
            return null;
        }

        var applied = appliedNumbers.Sum(number => InstallmentAmountFor(number, plan));
        return Round2(total - applied);
    }

    /// <summary>True when every installment of a finite plan has been applied; always false for an indefinite plan.</summary>
    public static bool IsPlanComplete(RecurringIncomePlan plan, IReadOnlySet<int> appliedNumbers)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(appliedNumbers);

        if (plan.IsIndefinite || plan.InstallmentCount is not { } count)
        {
            return false;
        }

        for (var number = 1; number <= count; number++)
        {
            if (!appliedNumbers.Contains(number))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The recurring-income state machine (RN-01/RN-02): EN_REVISION → VIGENTE / RECHAZADO / ANULADO;
    /// VIGENTE → SUSPENDIDO / FINALIZADO / ANULADO; SUSPENDIDO → VIGENTE; FINALIZADO → VIGENTE (settlement
    /// reopen only). Terminal RECHAZADO / ANULADO allow no transition.
    /// </summary>
    public static bool CanTransition(string fromStatus, string toStatus)
    {
        if (fromStatus == toStatus)
        {
            return false;
        }

        return fromStatus switch
        {
            RecurringIncomeStatuses.EnRevision =>
                toStatus is RecurringIncomeStatuses.Vigente
                    or RecurringIncomeStatuses.Rechazado
                    or RecurringIncomeStatuses.Anulado,
            RecurringIncomeStatuses.Vigente =>
                toStatus is RecurringIncomeStatuses.Suspendido
                    or RecurringIncomeStatuses.Finalizado
                    or RecurringIncomeStatuses.Anulado,
            RecurringIncomeStatuses.Suspendido =>
                toStatus is RecurringIncomeStatuses.Vigente,
            RecurringIncomeStatuses.Finalizado =>
                toStatus is RecurringIncomeStatuses.Vigente,
            _ => false,
        };
    }

    /// <summary>
    /// Validates the settlement action against the plan (P-06): PAGAR_SALDO is meaningless for an indefinite
    /// plan (there is no balance to pay), so it is rejected.
    /// </summary>
    public static RecurringIncomeRuleResult ValidateSettlementAction(string settlementActionCode, bool isIndefinite)
    {
        if (isIndefinite && settlementActionCode == RecurringIncomeSettlementActions.PagarSaldo)
        {
            return RecurringIncomeRuleResult.Fail(SettlementActionIndefiniteCode);
        }

        return RecurringIncomeRuleResult.Ok;
    }

    private static DateOnly DueDateFor(string frequencyCode, DateOnly startDate, int installmentNumber)
    {
        var steps = installmentNumber - 1;
        return frequencyCode switch
        {
            RecurringIncomeFrequencies.Quincenal => startDate.AddDays(15 * steps),
            RecurringIncomeFrequencies.Semanal => startDate.AddDays(7 * steps),
            RecurringIncomeFrequencies.Unica => startDate,
            // MENSUAL and any other (unknown) frequency default to a monthly cadence.
            _ => startDate.AddMonths(steps),
        };
    }
}
