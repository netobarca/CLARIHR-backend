using CLARIHR.Application.Common.Errors;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles.Compensation;

/// <summary>
/// Dedicated handler-level errors for recurring deductions (REQ-008 §3.3 CRUD + resolution). Each code requires an
/// EN + ES resource entry (parity: <c>BackendMessageLocalizationTests</c>). The plan / segment / amortization /
/// settlement-action / state-transition codes are produced by the pure <see cref="RecurringDeductionRules"/> and
/// localized alongside these; the ones here cover the cross-aggregate (catalog / concept / plaza) checks and the
/// decision-flow guards that need a database or the request context. Field-level validation (required codes,
/// positive values, plan shape) is the validator's job (400) and is NOT here.
/// </summary>
internal static class RecurringDeductionErrors
{
    public static readonly Error TypeInvalid = new(
        "RECURRING_DEDUCTION_TYPE_INVALID",
        "The recurring-deduction type is not valid for the active catalog.", ErrorType.UnprocessableEntity);

    public static readonly Error ConceptInvalid = new(
        "RECURRING_DEDUCTION_CONCEPT_INVALID",
        "The compensation concept is not a valid active non-statutory deduction concept for the company's country.", ErrorType.UnprocessableEntity);

    public static readonly Error FinancialInstitutionRequired = new(
        "RECURRING_DEDUCTION_FINANCIAL_INSTITUTION_REQUIRED",
        "The financial institution is required for an external deduction concept.", ErrorType.UnprocessableEntity);

    public static readonly Error PayrollTypeInvalid = new(
        "RECURRING_DEDUCTION_PAYROLL_TYPE_INVALID",
        "The payroll type is not valid for the active catalog.", ErrorType.UnprocessableEntity);

    public static readonly Error FrequencyInvalid = new(
        "RECURRING_DEDUCTION_FREQUENCY_INVALID",
        "The installment or application frequency is not valid for the active pay-period catalog.", ErrorType.UnprocessableEntity);

    public static readonly Error AssignedPositionInvalid = new(
        "RECURRING_DEDUCTION_ASSIGNED_POSITION_INVALID",
        "The assigned position is not a valid plaza of this employee.", ErrorType.UnprocessableEntity);

    public static readonly Error StatusInvalid = new(
        "RECURRING_DEDUCTION_STATUS_INVALID",
        "The target status is not a valid resolution target for the active catalog.", ErrorType.UnprocessableEntity);

    public static readonly Error DecisionNoteRequired = new(
        "RECURRING_DEDUCTION_DECISION_NOTE_REQUIRED",
        "A decision note is required to reject a recurring deduction.", ErrorType.UnprocessableEntity);

    public static readonly Error AnnulmentReasonRequired = new(
        "RECURRING_DEDUCTION_ANNULMENT_REASON_REQUIRED",
        "An annulment reason is required.", ErrorType.UnprocessableEntity);

    public static readonly Error ClosureReasonRequired = new(
        "RECURRING_DEDUCTION_CLOSURE_REASON_REQUIRED",
        "A closure reason is required to close a recurring deduction manually.", ErrorType.UnprocessableEntity);

    public static readonly Error StateRuleViolation = new(
        "RECURRING_DEDUCTION_STATE_RULE_VIOLATION",
        "The recurring deduction is not in a state that allows this operation.", ErrorType.UnprocessableEntity);

    // Separation of duties (double anti-self, D-05): neither the subject employee nor the registrar may decide or
    // revoke the recurring deduction. 403 (Forbidden).
    public static readonly Error SelfApprovalForbidden = new(
        "RECURRING_DEDUCTION_SELF_APPROVAL_FORBIDDEN",
        "The subject employee or the registrar cannot decide or revoke the recurring deduction.", ErrorType.Forbidden);
}

/// <summary>One segment of the plan definition (№12): installments <c>From</c>..<c>To</c> are worth <c>Value</c>
/// each. <c>To</c> is null only for the single open segment of an indefinite plan.</summary>
public sealed record RecurringDeductionSegment(int FromInstallment, int? ToInstallment, decimal InstallmentValue);

/// <summary>
/// The normalized plan of a recurring deduction. It is expressed in exactly ONE of two ways (№12): either a list
/// of <see cref="Segments"/> (no interest) or a compound-interest credit (<see cref="UsesCompoundInterest"/>,
/// with <see cref="PrincipalAmount"/> + <see cref="AnnualRatePercent"/> + <see cref="InstallmentCount"/>), whose
/// installment values are DERIVED by the amortization calculator and never materialized. For a finite plan
/// <see cref="InstallmentCount"/> and <see cref="TotalAmount"/> are resolved; for an indefinite one both are null.
/// </summary>
public sealed record RecurringDeductionPlan(
    IReadOnlyList<RecurringDeductionSegment> Segments,
    int? InstallmentCount,
    decimal? TotalAmount,
    bool IsIndefinite,
    bool UsesCompoundInterest,
    decimal? PrincipalAmount,
    decimal? AnnualRatePercent,
    string InstallmentFrequencyCode);

/// <summary>Result of <see cref="RecurringDeductionRules.NormalizePlan"/>: the resolved plan, or an error code.</summary>
public sealed record RecurringDeductionPlanNormalization(bool IsValid, string? ErrorCode, RecurringDeductionPlan? Plan)
{
    public static RecurringDeductionPlanNormalization Failure(string errorCode) => new(false, errorCode, null);

    public static RecurringDeductionPlanNormalization Success(RecurringDeductionPlan plan) => new(true, null, plan);
}

/// <summary>A generic pass/fail rule outcome carrying the bilingual <c>extensions.code</c> when it fails.</summary>
public readonly record struct RecurringDeductionRuleResult(bool IsValid, string? ErrorCode)
{
    public static RecurringDeductionRuleResult Ok { get; } = new(true, null);

    public static RecurringDeductionRuleResult Fail(string errorCode) => new(false, errorCode);
}

/// <summary>
/// One row of the amortization table (derived, never persisted — the split is snapshotted on the applied
/// installment). <see cref="ClosingBalance"/> is the capital still owed after this installment.
/// </summary>
public sealed record RecurringDeductionAmortizationRow(
    int InstallmentNumber,
    decimal Amount,
    decimal CapitalAmount,
    decimal InterestAmount,
    decimal ClosingBalance);

/// <summary>One projected (theoretical) installment of the plan — a derivation, never persisted.</summary>
public sealed record RecurringDeductionProjectedInstallment(
    int InstallmentNumber,
    DateOnly TheoreticalDueDate,
    decimal Amount,
    decimal? CapitalAmount,
    decimal? InterestAmount,
    bool IsApplied,
    bool IsOverdue);

/// <summary>
/// The recurring-deduction plan + amortization + lifecycle arithmetic (REQ-008 §3.2, golden suite A.3) — 100 %
/// pure and deterministic: no clock, no database, no side-effects. It is the SINGLE source of truth for the
/// segment coherence, the French-system amortization (fixed quota + capital/interest split), the per-installment
/// amount, the theoretical projection (skipping the exception months and splitting by the application frequency),
/// the settlement balance and the state machine — so the schedule endpoint, the installment application, the
/// settlement suggestion and the write validations all cuadran by construction.
/// <para>Single rounding rule (mirrors <c>RecurringIncomeRules.Round2</c>): half-up away-from-zero, 2 decimals —
/// the ONLY rounding point of the module.</para>
/// </summary>
public static class RecurringDeductionRules
{
    // ── Plan / segment error codes (bilingual; localization parity kept in BackendMessages[.es].resx) ──
    public const string SegmentsRequiredCode = "RECURRING_DEDUCTION_SEGMENTS_REQUIRED";
    public const string SegmentsWithInterestCode = "RECURRING_DEDUCTION_SEGMENTS_WITH_INTEREST";
    public const string SegmentValueInvalidCode = "RECURRING_DEDUCTION_SEGMENT_VALUE_INVALID";
    public const string SegmentRangeInvalidCode = "RECURRING_DEDUCTION_SEGMENT_RANGE_INVALID";
    public const string SegmentsNotContiguousCode = "RECURRING_DEDUCTION_SEGMENTS_NOT_CONTIGUOUS";
    public const string SegmentsIndefiniteShapeCode = "RECURRING_DEDUCTION_SEGMENTS_INDEFINITE_SHAPE";
    public const string SegmentsFiniteShapeCode = "RECURRING_DEDUCTION_SEGMENTS_FINITE_SHAPE";

    // ── Compound-interest error codes ─────────────────────────────────────────────────────────────────
    public const string InterestIndefiniteCode = "RECURRING_DEDUCTION_INTEREST_INDEFINITE";
    public const string InterestPrincipalInvalidCode = "RECURRING_DEDUCTION_INTEREST_PRINCIPAL_INVALID";
    public const string InterestRateInvalidCode = "RECURRING_DEDUCTION_INTEREST_RATE_INVALID";
    public const string InterestCountInvalidCode = "RECURRING_DEDUCTION_INTEREST_COUNT_INVALID";
    public const string InterestNotAmortizableCode = "RECURRING_DEDUCTION_INTEREST_NOT_AMORTIZABLE";

    // ── Settlement-action + installment + transition + frequency error codes ──────────────────────────
    public const string SettlementActionIndefiniteCode = "RECURRING_DEDUCTION_SETTLEMENT_ACTION_INDEFINITE";
    public const string InstallmentNotApplicableCode = "RECURRING_DEDUCTION_INSTALLMENT_NOT_APPLICABLE";
    public const string InstallmentSequenceInvalidCode = "RECURRING_DEDUCTION_INSTALLMENT_SEQUENCE_INVALID";
    public const string InstallmentExceedsPlanCode = "RECURRING_DEDUCTION_INSTALLMENT_EXCEEDS_PLAN";
    public const string InstallmentNotDueYetCode = "RECURRING_DEDUCTION_INSTALLMENT_NOT_DUE_YET";
    public const string ExtraordinaryNotApplicableCode = "RECURRING_DEDUCTION_EXTRAORDINARY_NOT_APPLICABLE";
    public const string ExtraordinaryExceedsBalanceCode = "RECURRING_DEDUCTION_EXTRAORDINARY_EXCEEDS_BALANCE";
    public const string ExtraordinaryIndefiniteCode = "RECURRING_DEDUCTION_EXTRAORDINARY_INDEFINITE";
    public const string ApplicationFrequencyInvalidCode = "RECURRING_DEDUCTION_APPLICATION_FREQUENCY_INVALID";
    public const string StateRuleViolationCode = "RECURRING_DEDUCTION_STATE_RULE_VIOLATION";

    /// <summary>Single rounding rule of the module: half-up away-from-zero, 2 decimals.</summary>
    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// How many periods of <paramref name="frequencyCode"/> fit in a year (P-03): MENSUAL 12, QUINCENAL 24,
    /// SEMANAL 52. Any other (unknown) code degrades to the monthly cadence, mirroring the projection.
    /// </summary>
    public static int PeriodsPerYear(string frequencyCode) =>
        Normalize(frequencyCode) switch
        {
            RecurringDeductionFrequencies.Quincenal => 24,
            RecurringDeductionFrequencies.Semanal => 52,
            RecurringDeductionFrequencies.Unica => 1,
            _ => 12,
        };

    /// <summary>
    /// Validates the segment list (№12): values are positive, ranges are well formed, the segments are contiguous
    /// from installment 1 with no gaps or overlaps. An INDEFINITE plan is exactly one open segment (no
    /// <c>ToInstallment</c>); a FINITE plan is one or more closed segments (the last one closes the plan). A
    /// compound-interest credit carries NO segments at all — its plan is derived.
    /// </summary>
    public static RecurringDeductionRuleResult ValidateSegments(
        IReadOnlyList<RecurringDeductionSegment>? segments,
        bool isIndefinite,
        bool usesCompoundInterest)
    {
        var ordered = (segments ?? []).OrderBy(segment => segment.FromInstallment).ToList();

        if (usesCompoundInterest)
        {
            return ordered.Count > 0
                ? RecurringDeductionRuleResult.Fail(SegmentsWithInterestCode)
                : RecurringDeductionRuleResult.Ok;
        }

        if (ordered.Count == 0)
        {
            return RecurringDeductionRuleResult.Fail(SegmentsRequiredCode);
        }

        foreach (var segment in ordered)
        {
            if (segment.InstallmentValue <= 0m)
            {
                return RecurringDeductionRuleResult.Fail(SegmentValueInvalidCode);
            }

            if (segment.FromInstallment < 1)
            {
                return RecurringDeductionRuleResult.Fail(SegmentRangeInvalidCode);
            }

            if (segment.ToInstallment is { } to && to < segment.FromInstallment)
            {
                return RecurringDeductionRuleResult.Fail(SegmentRangeInvalidCode);
            }
        }

        // Contiguity: the first segment starts at 1 and each next one starts right after the previous one closes.
        // Only the LAST segment may be open, and only when the plan is indefinite.
        var expectedFrom = 1;
        for (var index = 0; index < ordered.Count; index++)
        {
            var segment = ordered[index];
            var isLast = index == ordered.Count - 1;

            if (segment.FromInstallment != expectedFrom)
            {
                return RecurringDeductionRuleResult.Fail(SegmentsNotContiguousCode);
            }

            if (segment.ToInstallment is not { } to)
            {
                // An open segment is legal only as the last one of an indefinite plan.
                return isLast && isIndefinite
                    ? RecurringDeductionRuleResult.Ok
                    : RecurringDeductionRuleResult.Fail(SegmentsNotContiguousCode);
            }

            expectedFrom = to + 1;
        }

        // Every segment closed: the plan is finite. An indefinite plan must end in an open segment.
        return isIndefinite
            ? RecurringDeductionRuleResult.Fail(SegmentsIndefiniteShapeCode)
            : RecurringDeductionRuleResult.Ok;
    }

    /// <summary>
    /// Normalizes and validates the whole plan. A compound-interest credit must be finite and carry a positive
    /// principal, a positive nominal annual rate and at least one installment (its count/total are derived from
    /// the amortization); a plain credit derives its count and total from the segments (null for an indefinite
    /// plan, which has no end and therefore no total).
    /// </summary>
    public static RecurringDeductionPlanNormalization NormalizePlan(
        IReadOnlyList<RecurringDeductionSegment>? segments,
        bool isIndefinite,
        bool usesCompoundInterest,
        decimal? principalAmount,
        decimal? annualRatePercent,
        int? plannedInstallments,
        string installmentFrequencyCode)
    {
        var segmentCheck = ValidateSegments(segments, isIndefinite, usesCompoundInterest);
        if (!segmentCheck.IsValid)
        {
            return RecurringDeductionPlanNormalization.Failure(segmentCheck.ErrorCode!);
        }

        if (usesCompoundInterest)
        {
            if (isIndefinite)
            {
                return RecurringDeductionPlanNormalization.Failure(InterestIndefiniteCode);
            }

            if (principalAmount is not { } principal || principal <= 0m)
            {
                return RecurringDeductionPlanNormalization.Failure(InterestPrincipalInvalidCode);
            }

            if (annualRatePercent is not { } rate || rate <= 0m)
            {
                return RecurringDeductionPlanNormalization.Failure(InterestRateInvalidCode);
            }

            if (plannedInstallments is not { } count || count < 1)
            {
                return RecurringDeductionPlanNormalization.Failure(InterestCountInvalidCode);
            }

            var schedule = TryBuildAmortizationSchedule(principal, rate, count, installmentFrequencyCode);
            if (schedule is null)
            {
                return RecurringDeductionPlanNormalization.Failure(InterestNotAmortizableCode);
            }

            return RecurringDeductionPlanNormalization.Success(new RecurringDeductionPlan(
                Segments: [],
                InstallmentCount: count,
                TotalAmount: Round2(schedule.Sum(row => row.Amount)),
                IsIndefinite: false,
                UsesCompoundInterest: true,
                PrincipalAmount: Round2(principal),
                AnnualRatePercent: rate,
                InstallmentFrequencyCode: Normalize(installmentFrequencyCode)));
        }

        if (principalAmount is not null || annualRatePercent is not null || plannedInstallments is not null)
        {
            return RecurringDeductionPlanNormalization.Failure(InterestPrincipalInvalidCode);
        }

        var ordered = (segments ?? []).OrderBy(segment => segment.FromInstallment).ToList();

        if (isIndefinite)
        {
            return RecurringDeductionPlanNormalization.Success(new RecurringDeductionPlan(
                Segments: ordered,
                InstallmentCount: null,
                TotalAmount: null,
                IsIndefinite: true,
                UsesCompoundInterest: false,
                PrincipalAmount: null,
                AnnualRatePercent: null,
                InstallmentFrequencyCode: Normalize(installmentFrequencyCode)));
        }

        var resolvedCount = ordered[^1].ToInstallment!.Value;
        var resolvedTotal = Round2(ordered.Sum(segment =>
            (segment.ToInstallment!.Value - segment.FromInstallment + 1) * segment.InstallmentValue));

        return RecurringDeductionPlanNormalization.Success(new RecurringDeductionPlan(
            Segments: ordered,
            InstallmentCount: resolvedCount,
            TotalAmount: resolvedTotal,
            IsIndefinite: false,
            UsesCompoundInterest: false,
            PrincipalAmount: null,
            AnnualRatePercent: null,
            InstallmentFrequencyCode: Normalize(installmentFrequencyCode)));
    }

    /// <summary>
    /// Builds the FRENCH-system amortization table (№13, golden A.3 case 1): a fixed quota
    /// <c>P·i / (1 − (1+i)^−n)</c> where <c>i</c> is the nominal annual rate divided by the periods of the
    /// installment frequency (P-03). Per installment the interest is <c>Round2(balance · i)</c> and the capital is
    /// the remainder of the quota; the LAST installment absorbs the exact remaining capital, so Σ capital equals
    /// the principal to the cent.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The inputs are not amortizable.</exception>
    public static IReadOnlyList<RecurringDeductionAmortizationRow> BuildAmortizationSchedule(
        decimal principal,
        decimal annualRatePercent,
        int installments,
        string installmentFrequencyCode) =>
        TryBuildAmortizationSchedule(principal, annualRatePercent, installments, installmentFrequencyCode)
        ?? throw new ArgumentOutOfRangeException(
            nameof(principal),
            "The credit is not amortizable with the supplied principal, rate and installment count.");

    /// <summary>
    /// Rebuilds the table from an outstanding balance keeping the SAME quota (P-04: an extraordinary payment
    /// "reduces the term", not the quota). The resulting table has fewer installments and its last one absorbs
    /// the remaining capital. Returns an empty table when the balance is already zero (a payoff).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The quota does not even cover the first period's interest,
    /// so the credit would never amortize.</exception>
    public static IReadOnlyList<RecurringDeductionAmortizationRow> RecomputeFromBalance(
        decimal balance,
        decimal quota,
        decimal annualRatePercent,
        string installmentFrequencyCode)
    {
        if (balance <= 0m)
        {
            return [];
        }

        if (quota <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(quota), "The quota must be greater than zero.");
        }

        var periodRate = PeriodRate(annualRatePercent, installmentFrequencyCode);
        var rows = new List<RecurringDeductionAmortizationRow>();
        var outstanding = Round2(balance);
        var number = 1;

        while (outstanding > 0m)
        {
            var interest = Round2(outstanding * periodRate);
            var capital = quota - interest;

            if (capital <= 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quota),
                    "The quota does not cover the period interest; the credit would never amortize.");
            }

            if (capital >= outstanding)
            {
                // Last installment: it absorbs exactly what is left, so Σ capital == the balance.
                rows.Add(new RecurringDeductionAmortizationRow(number, Round2(outstanding + interest), outstanding, interest, 0m));
                break;
            }

            outstanding = Round2(outstanding - capital);
            rows.Add(new RecurringDeductionAmortizationRow(number, quota, capital, interest, outstanding));
            number++;
        }

        return rows;
    }

    /// <summary>
    /// The amount of installment <paramref name="installmentNumber"/> (1-based): the value of the segment it
    /// falls in, or — for a compound-interest credit — the quota of the amortization table (whose last row
    /// absorbs the rounding remainder).
    /// </summary>
    public static decimal InstallmentAmountFor(int installmentNumber, RecurringDeductionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (installmentNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(installmentNumber), "The installment number must be greater than or equal to one.");
        }

        if (plan.InstallmentCount is { } count && installmentNumber > count)
        {
            throw new ArgumentOutOfRangeException(nameof(installmentNumber), "The installment number cannot exceed the finite plan count.");
        }

        if (plan.UsesCompoundInterest)
        {
            var schedule = BuildAmortizationSchedule(
                plan.PrincipalAmount!.Value,
                plan.AnnualRatePercent!.Value,
                plan.InstallmentCount!.Value,
                plan.InstallmentFrequencyCode);

            // The table can close one row early when rounding lets the second-to-last capital absorb the
            // balance; past that point the credit is already paid, so there is nothing left to charge.
            return installmentNumber > schedule.Count ? 0m : schedule[installmentNumber - 1].Amount;
        }

        var segment = plan.Segments.FirstOrDefault(item =>
            item.FromInstallment <= installmentNumber
            && (item.ToInstallment is null || item.ToInstallment >= installmentNumber));

        return segment is null
            ? throw new ArgumentOutOfRangeException(nameof(installmentNumber), "The installment number falls outside every plan segment.")
            : Round2(segment.InstallmentValue);
    }

    /// <summary>
    /// Splits the value of one DUE installment across the application periods when the application frequency is
    /// faster than the installment frequency (№14 / golden A.3 case 6): a MENSUAL $100 quota applied QUINCENAL
    /// becomes 2 × $50.00; an indivisible amount lands as $33.33 / $33.33 / $33.34 — the LAST part absorbs the
    /// remainder so the parts always add up to the quota exactly.
    /// </summary>
    public static IReadOnlyList<decimal> SplitByApplicationFrequency(decimal installmentValue, int parts)
    {
        if (parts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(parts), "The number of parts must be greater than or equal to one.");
        }

        var total = Round2(installmentValue);
        if (parts == 1)
        {
            return [total];
        }

        var part = Round2(total / parts);
        var split = new List<decimal>(parts);
        for (var index = 0; index < parts - 1; index++)
        {
            split.Add(part);
        }

        split.Add(Round2(total - (part * (parts - 1))));
        return split;
    }

    /// <summary>
    /// How many application periods each installment is split into (№14): the ratio between the application
    /// frequency and the installment frequency. It must be a whole number ≥ 1 — an application cadence SLOWER
    /// than the installment cadence (e.g. a quincenal quota applied monthly) is rejected (P-06).
    /// </summary>
    public static RecurringDeductionRuleResult ValidateFrequencyPair(
        string installmentFrequencyCode,
        string applicationFrequencyCode)
    {
        var installmentPeriods = PeriodsPerYear(installmentFrequencyCode);
        var applicationPeriods = PeriodsPerYear(applicationFrequencyCode);

        if (applicationPeriods < installmentPeriods || applicationPeriods % installmentPeriods != 0)
        {
            return RecurringDeductionRuleResult.Fail(ApplicationFrequencyInvalidCode);
        }

        return RecurringDeductionRuleResult.Ok;
    }

    /// <summary>The number of application parts per installment (1 when both frequencies match).</summary>
    public static int ApplicationPartsPerInstallment(string installmentFrequencyCode, string applicationFrequencyCode)
    {
        var installmentPeriods = PeriodsPerYear(installmentFrequencyCode);
        var applicationPeriods = PeriodsPerYear(applicationFrequencyCode);

        return applicationPeriods < installmentPeriods || applicationPeriods % installmentPeriods != 0
            ? 1
            : applicationPeriods / installmentPeriods;
    }

    /// <summary>
    /// Builds the theoretical projection of the plan (derived, never persisted). Due dates advance by the
    /// INSTALLMENT frequency from the start date, SKIPPING every date that lands in an exception month (P-05 —
    /// the plan is pushed forward, never shortened: the credit is still owed in full). An unapplied installment
    /// whose due date is before <paramref name="today"/> is overdue. For a compound-interest credit each row
    /// carries its capital/interest split.
    /// </summary>
    public static IReadOnlyList<RecurringDeductionProjectedInstallment> BuildProjection(
        RecurringDeductionPlan plan,
        DateOnly installmentStartDate,
        IReadOnlySet<int> exceptionMonths,
        IReadOnlySet<int> appliedNumbers,
        DateOnly today,
        int indefiniteHorizon = 12)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(exceptionMonths);
        ArgumentNullException.ThrowIfNull(appliedNumbers);

        int projectionCount;
        if (Normalize(plan.InstallmentFrequencyCode) == RecurringDeductionFrequencies.Unica)
        {
            projectionCount = 1;
        }
        else if (plan.InstallmentCount is { } count)
        {
            projectionCount = count;
        }
        else
        {
            var appliedCeiling = appliedNumbers.Count == 0 ? 0 : appliedNumbers.Max();
            projectionCount = Math.Max(indefiniteHorizon, appliedCeiling);
        }

        var schedule = plan.UsesCompoundInterest
            ? BuildAmortizationSchedule(
                plan.PrincipalAmount!.Value,
                plan.AnnualRatePercent!.Value,
                plan.InstallmentCount!.Value,
                plan.InstallmentFrequencyCode)
            : null;

        var dueDates = BuildDueDates(plan.InstallmentFrequencyCode, installmentStartDate, exceptionMonths, projectionCount);
        var projection = new List<RecurringDeductionProjectedInstallment>(projectionCount);

        for (var number = 1; number <= projectionCount; number++)
        {
            var dueDate = dueDates[number - 1];
            var isApplied = appliedNumbers.Contains(number);
            var row = schedule is not null && number <= schedule.Count ? schedule[number - 1] : null;
            var amount = row?.Amount ?? InstallmentAmountFor(number, plan);
            var isOverdue = !isApplied && dueDate < today;

            projection.Add(new RecurringDeductionProjectedInstallment(
                number,
                dueDate,
                amount,
                row?.CapitalAmount,
                row?.InterestAmount,
                isApplied,
                isOverdue));
        }

        return projection;
    }

    /// <summary>
    /// The theoretical due date of installment <paramref name="installmentNumber"/> (1-based), skipping the
    /// exception months (P-05). Exposed so the application handler can snapshot the due date of the NEXT
    /// installment without materializing the whole projection.
    /// </summary>
    public static DateOnly TheoreticalDueDateFor(
        string installmentFrequencyCode,
        DateOnly installmentStartDate,
        IReadOnlySet<int> exceptionMonths,
        int installmentNumber)
    {
        ArgumentNullException.ThrowIfNull(exceptionMonths);

        if (installmentNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(installmentNumber), "The installment number must be greater than or equal to one.");
        }

        return BuildDueDates(installmentFrequencyCode, installmentStartDate, exceptionMonths, installmentNumber)[installmentNumber - 1];
    }

    /// <summary>The next expected REGULAR installment number: the smallest positive integer not already applied.</summary>
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
    /// Validates that a REGULAR installment may be applied now: the credit must be VIGENTE, its effective date
    /// must have been reached (D-04 — a future-dated credit cannot be charged yet), the number must be the next
    /// expected one and it must not exceed the planned count.
    /// </summary>
    public static RecurringDeductionRuleResult CanApplyInstallment(
        string statusCode,
        DateOnly effectiveDate,
        DateOnly today,
        int installmentNumber,
        RecurringDeductionPlan plan,
        IReadOnlySet<int> appliedNumbers)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(appliedNumbers);

        if (statusCode != RecurringDeductionStatuses.Vigente)
        {
            return RecurringDeductionRuleResult.Fail(InstallmentNotApplicableCode);
        }

        if (effectiveDate > today)
        {
            return RecurringDeductionRuleResult.Fail(InstallmentNotDueYetCode);
        }

        if (installmentNumber != NextInstallmentNumber(appliedNumbers))
        {
            return RecurringDeductionRuleResult.Fail(InstallmentSequenceInvalidCode);
        }

        if (plan.InstallmentCount is { } count && installmentNumber > count)
        {
            return RecurringDeductionRuleResult.Fail(InstallmentExceedsPlanCode);
        }

        return RecurringDeductionRuleResult.Ok;
    }

    /// <summary>
    /// Validates an EXTRAORDINARY payment (P-04): only a VIGENTE credit takes one — never a SUSPENDIDO one —, the
    /// plan must be finite (an indefinite credit has no balance to pay off) and the payment may not exceed the
    /// outstanding balance. Paying exactly the balance is a payoff (legal — it finalizes the credit).
    /// </summary>
    public static RecurringDeductionRuleResult CanApplyExtraordinary(
        string statusCode,
        DateOnly effectiveDate,
        DateOnly today,
        bool isIndefinite,
        decimal amount,
        decimal outstandingBalance)
    {
        if (statusCode != RecurringDeductionStatuses.Vigente)
        {
            return RecurringDeductionRuleResult.Fail(ExtraordinaryNotApplicableCode);
        }

        if (effectiveDate > today)
        {
            return RecurringDeductionRuleResult.Fail(InstallmentNotDueYetCode);
        }

        if (isIndefinite)
        {
            return RecurringDeductionRuleResult.Fail(ExtraordinaryIndefiniteCode);
        }

        if (amount <= 0m)
        {
            return RecurringDeductionRuleResult.Fail(SegmentValueInvalidCode);
        }

        if (amount > outstandingBalance)
        {
            return RecurringDeductionRuleResult.Fail(ExtraordinaryExceedsBalanceCode);
        }

        return RecurringDeductionRuleResult.Ok;
    }

    /// <summary>
    /// The balance the settlement suggests discounting (§0.9). WITHOUT interest it is
    /// <c>Σ segments − Σ charged</c>; WITH interest it is the outstanding CAPITAL — paying a credit off early
    /// does not owe the future interest. Null for an indefinite plan (there is no balance; only CANCELAR applies).
    /// </summary>
    public static decimal? SettlementBalance(
        RecurringDeductionPlan plan,
        decimal chargedAmount,
        decimal chargedCapital)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.IsIndefinite)
        {
            return null;
        }

        if (plan.UsesCompoundInterest)
        {
            return Round2(Math.Max(0m, plan.PrincipalAmount!.Value - chargedCapital));
        }

        return plan.TotalAmount is not { } total
            ? null
            : Round2(Math.Max(0m, total - chargedAmount));
    }

    /// <summary>True when the finite plan is done: everything charged, or the balance paid off ahead of time.</summary>
    public static bool IsPlanComplete(
        RecurringDeductionPlan plan,
        decimal chargedAmount,
        decimal chargedCapital,
        IReadOnlySet<int> appliedNumbers)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(appliedNumbers);

        if (plan.IsIndefinite)
        {
            return false;
        }

        if (SettlementBalance(plan, chargedAmount, chargedCapital) <= 0m)
        {
            return true;
        }

        if (plan.InstallmentCount is not { } count)
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
    /// The recurring-deduction state machine: EN_REVISION → VIGENTE / RECHAZADO / ANULADO; VIGENTE → SUSPENDIDO /
    /// FINALIZADO / ANULADO; SUSPENDIDO → VIGENTE; FINALIZADO → VIGENTE (settlement reopen or installment
    /// annulment only). Terminal RECHAZADO / ANULADO allow no transition.
    /// </summary>
    public static bool CanTransition(string fromStatus, string toStatus)
    {
        if (fromStatus == toStatus)
        {
            return false;
        }

        return fromStatus switch
        {
            RecurringDeductionStatuses.EnRevision =>
                toStatus is RecurringDeductionStatuses.Vigente
                    or RecurringDeductionStatuses.Rechazado
                    or RecurringDeductionStatuses.Anulado,
            RecurringDeductionStatuses.Vigente =>
                toStatus is RecurringDeductionStatuses.Suspendido
                    or RecurringDeductionStatuses.Finalizado
                    or RecurringDeductionStatuses.Anulado,
            RecurringDeductionStatuses.Suspendido =>
                toStatus is RecurringDeductionStatuses.Vigente,
            RecurringDeductionStatuses.Finalizado =>
                toStatus is RecurringDeductionStatuses.Vigente,
            _ => false,
        };
    }

    /// <summary>
    /// Validates the settlement action against the plan (D-12, mirror of P-06): DESCONTAR_SALDO is meaningless for
    /// an indefinite plan (there is no balance to discount), so it is rejected — an indefinite credit may only be
    /// CANCELAR (written off) at settlement.
    /// </summary>
    public static RecurringDeductionRuleResult ValidateSettlementAction(string settlementActionCode, bool isIndefinite)
    {
        if (isIndefinite && settlementActionCode == RecurringDeductionSettlementActions.DescontarSaldo)
        {
            return RecurringDeductionRuleResult.Fail(SettlementActionIndefiniteCode);
        }

        return RecurringDeductionRuleResult.Ok;
    }

    /// <summary>The per-period rate: the NOMINAL ANNUAL percentage divided by the periods of the frequency (P-03).</summary>
    private static decimal PeriodRate(decimal annualRatePercent, string installmentFrequencyCode) =>
        annualRatePercent / 100m / PeriodsPerYear(installmentFrequencyCode);

    private static IReadOnlyList<RecurringDeductionAmortizationRow>? TryBuildAmortizationSchedule(
        decimal principal,
        decimal annualRatePercent,
        int installments,
        string installmentFrequencyCode)
    {
        if (principal <= 0m || annualRatePercent <= 0m || installments < 1)
        {
            return null;
        }

        var periodRate = PeriodRate(annualRatePercent, installmentFrequencyCode);

        // French system: quota = P·i / (1 − (1+i)^−n). Computed in double for the power, then rounded once.
        var factor = Math.Pow(1d + (double)periodRate, -installments);
        var denominator = 1d - factor;
        if (denominator <= 0d)
        {
            return null;
        }

        var quota = Round2((decimal)((double)(principal * periodRate) / denominator));
        if (quota <= 0m)
        {
            return null;
        }

        var rows = new List<RecurringDeductionAmortizationRow>(installments);
        var outstanding = Round2(principal);

        for (var number = 1; number <= installments; number++)
        {
            var interest = Round2(outstanding * periodRate);
            var capital = quota - interest;

            if (capital <= 0m)
            {
                // The quota does not even cover the interest: the credit would never amortize.
                return null;
            }

            // The last installment absorbs the exact remaining capital, so Σ capital == principal. The
            // capital >= outstanding branch is the same close-out reached early, which rounding can produce on
            // the second-to-last row; without it the balance would go negative.
            if (number == installments || capital >= outstanding)
            {
                rows.Add(new RecurringDeductionAmortizationRow(number, Round2(outstanding + interest), outstanding, interest, 0m));
                break;
            }

            outstanding = Round2(outstanding - capital);
            rows.Add(new RecurringDeductionAmortizationRow(number, quota, capital, interest, outstanding));
        }

        return rows;
    }

    /// <summary>
    /// The due dates of the first <paramref name="count"/> installments, advancing by the frequency and SKIPPING
    /// the exception months (P-05): a date landing in an excepted month is pushed to the next period of the
    /// cadence, so the plan runs longer but the credit is still owed in full.
    /// </summary>
    private static IReadOnlyList<DateOnly> BuildDueDates(
        string frequencyCode,
        DateOnly startDate,
        IReadOnlySet<int> exceptionMonths,
        int count)
    {
        var normalized = Normalize(frequencyCode);
        var dates = new List<DateOnly>(count);

        if (normalized == RecurringDeductionFrequencies.Unica)
        {
            for (var index = 0; index < count; index++)
            {
                dates.Add(startDate);
            }

            return dates;
        }

        // Every month excepted would loop forever; treat that degenerate configuration as "no exceptions".
        var effectiveExceptions = exceptionMonths.Count >= 12 ? new HashSet<int>() : exceptionMonths;

        var cursor = startDate;
        while (effectiveExceptions.Contains(cursor.Month))
        {
            cursor = Advance(normalized, cursor);
        }

        for (var index = 0; index < count; index++)
        {
            dates.Add(cursor);

            cursor = Advance(normalized, cursor);
            while (effectiveExceptions.Contains(cursor.Month))
            {
                cursor = Advance(normalized, cursor);
            }
        }

        return dates;
    }

    private static DateOnly Advance(string normalizedFrequency, DateOnly date) =>
        normalizedFrequency switch
        {
            RecurringDeductionFrequencies.Quincenal => date.AddDays(15),
            RecurringDeductionFrequencies.Semanal => date.AddDays(7),
            // MENSUAL and any other (unknown) frequency default to a monthly cadence.
            _ => date.AddMonths(1),
        };

    private static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();
}
