using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.CompensatoryTime;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles.Overtime;

/// <summary>
/// Dedicated handler-level errors for overtime records (REQ-007 §3.4 CRUD + resolution + application). Each code
/// requires an EN + ES resource entry (parity: <c>BackendMessageLocalizationTests</c>). The duration / factor /
/// daily-cap / transition / work-date codes are produced by the pure <see cref="OvertimeRecordRules"/> and
/// already localized (PR-2); these cover the cross-aggregate (catalog / plaza / requester) checks and the
/// decision-flow guards that need a database or the request context (added in PR-3…PR-6). Field-level validation
/// (required codes, positive value) is the validator's job (400) and is NOT here.
/// </summary>
internal static class OvertimeRecordErrors
{
    // Shares the code the pure rules already localize (RN-01/RN-02); the handler pre-checks the state before the
    // domain mutator so an invalid transition returns a clean 422 instead of a 500.
    public static readonly Error StateRuleViolation = new(
        OvertimeRecordRules.StateRuleViolationCode,
        "The overtime record is not in a state that allows this operation.", ErrorType.UnprocessableEntity);

    // Dedicated code for the re-imputation ("enviar a otro periodo", RF-005): only an AUTORIZADA record can be
    // re-targeted — a distinct, actionable 422 (vs the generic state violation).
    public static readonly Error NotRetargetable = new(
        OvertimeRecordRules.NotRetargetableCode,
        "Only an authorized overtime record can be re-targeted to another payroll period.", ErrorType.UnprocessableEntity);
}

/// <summary>A generic pass/fail rule outcome carrying the bilingual <c>extensions.code</c> when it fails.</summary>
public readonly record struct OvertimeRecordRuleResult(bool IsValid, string? ErrorCode)
{
    public static OvertimeRecordRuleResult Ok { get; } = new(true, null);

    public static OvertimeRecordRuleResult Fail(string errorCode) => new(false, errorCode);
}

/// <summary>Result of <see cref="OvertimeRecordRules.DeriveDecimalHours"/>: the derived decimal hours, or an error code.</summary>
public sealed record OvertimeDurationDerivation(bool IsValid, string? ErrorCode, decimal? DecimalHours)
{
    public static OvertimeDurationDerivation Failure(string errorCode) => new(false, errorCode, null);

    public static OvertimeDurationDerivation Success(decimal decimalHours) => new(true, null, decimalHours);
}

/// <summary>
/// Result of <see cref="OvertimeRecordRules.ValidateDailyCap"/>: whether the accumulated minutes fit under the
/// daily cap, plus the cap and the resulting total, so the handler can surface both in the 422 extensions.
/// </summary>
public sealed record OvertimeDailyCapCheck(bool IsWithinCap, int CapMinutes, int TotalMinutes)
{
    public bool IsExceeded => !IsWithinCap;
}

/// <summary>One record's decimal hours + applied factor, fed to <see cref="OvertimeRecordRules.FactoredHours"/>.</summary>
public readonly record struct OvertimeFactoredRecord(decimal DecimalHours, decimal Factor);

/// <summary>
/// The overtime-record duration + lifecycle arithmetic (REQ-007 §3.3, golden suite A.4) — 100% pure and
/// deterministic: no clock, no database, no side-effects. This is the SINGLE source of truth for the duration
/// derivation (h:m → decimal hours), the factor coherence (override note), the daily cap, the state-machine
/// transitions, the application eligibility (elapsed work date), the overdue derivation and the settlement
/// valuation, so the write validations, the settlement suggestion and the read projections all cuadran by
/// construction.
/// <para>Single rounding rule (mirrors <c>SettlementCalculationRules.Round2</c> and reused from
/// <c>CompensatoryTimeRules</c>): half-up away-from-zero, 2 decimals — the ONLY rounding point of the module.</para>
/// </summary>
public static class OvertimeRecordRules
{
    // ── Duration error codes (bilingual; localization parity kept in BackendMessages[.es].resx) ────────
    public const string DurationHoursInvalidCode = "OVERTIME_DURATION_HOURS_INVALID";
    public const string DurationMinutesInvalidCode = "OVERTIME_DURATION_MINUTES_INVALID";
    public const string DurationEmptyCode = "OVERTIME_DURATION_EMPTY";

    // ── Factor error codes (P-06) ─────────────────────────────────────────────────────────────────────
    public const string FactorInvalidCode = "OVERTIME_FACTOR_INVALID";
    public const string FactorNoteRequiredCode = "OVERTIME_FACTOR_NOTE_REQUIRED";

    // ── Daily cap (P-05) ────────────────────────────────────────────────────────────────────────────
    public const string DailyCapExceededCode = "OVERTIME_DAILY_CAP_EXCEEDED";

    // ── Application + transition + work-date error codes ──────────────────────────────────────────────
    public const string NotApplicableCode = "OVERTIME_NOT_APPLICABLE";
    public const string AlreadyAppliedCode = "OVERTIME_ALREADY_APPLIED";
    public const string WorkDateNotElapsedCode = "OVERTIME_WORK_DATE_NOT_ELAPSED";
    public const string WorkDateTooFarCode = "OVERTIME_WORK_DATE_TOO_FAR";
    public const string NotRetargetableCode = "OVERTIME_NOT_RETARGETABLE";
    public const string ApplicationNotRevertibleCode = "OVERTIME_APPLICATION_NOT_REVERTIBLE";
    public const string StateRuleViolationCode = "OVERTIME_STATE_RULE_VIOLATION";

    /// <summary>Single rounding rule of the module: half-up away-from-zero, 2 decimals.</summary>
    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Derives the decimal hours from the h:m duration (2 h 30 m = 2.50, 0 h 45 m = 0.75), validating
    /// non-negative hours, minutes in 0–59 and a strictly positive total. The result is rounded to 2 decimals
    /// (half-up away-from-zero) — mirrors the domain factory's private derivation so the persisted value and this
    /// projection cuadran by construction.
    /// </summary>
    public static OvertimeDurationDerivation DeriveDecimalHours(int hours, int minutes)
    {
        if (hours < 0)
        {
            return OvertimeDurationDerivation.Failure(DurationHoursInvalidCode);
        }

        if (minutes is < 0 or > 59)
        {
            return OvertimeDurationDerivation.Failure(DurationMinutesInvalidCode);
        }

        if ((hours * 60) + minutes <= 0)
        {
            return OvertimeDurationDerivation.Failure(DurationEmptyCode);
        }

        return OvertimeDurationDerivation.Success(Round2(hours + (minutes / 60m)));
    }

    /// <summary>
    /// Validates the applied factor (P-06): both the applied factor and the type factor must be strictly
    /// positive, and an override note is mandatory when the applied factor differs from the type's reference
    /// factor.
    /// </summary>
    public static OvertimeRecordRuleResult ValidateFactor(decimal factorApplied, decimal typeFactorSnapshot, string? note)
    {
        if (factorApplied <= 0m || typeFactorSnapshot <= 0m)
        {
            return OvertimeRecordRuleResult.Fail(FactorInvalidCode);
        }

        if (factorApplied != typeFactorSnapshot && string.IsNullOrWhiteSpace(note))
        {
            return OvertimeRecordRuleResult.Fail(FactorNoteRequiredCode);
        }

        return OvertimeRecordRuleResult.Ok;
    }

    /// <summary>
    /// Validates a new overtime record against the employee's daily cap (P-05): the sum of the day's active
    /// minutes plus the new minutes must not exceed <paramref name="capMinutes"/>. A null cap means no limit.
    /// </summary>
    public static OvertimeDailyCapCheck ValidateDailyCap(int existingActiveMinutes, int newMinutes, int? capMinutes)
    {
        var total = existingActiveMinutes + newMinutes;

        if (capMinutes is not { } cap)
        {
            return new OvertimeDailyCapCheck(true, 0, total);
        }

        return new OvertimeDailyCapCheck(total <= cap, cap, total);
    }

    /// <summary>
    /// The overtime-record state machine (RN-01/RN-02): EN_REVISION → AUTORIZADA / RECHAZADA / ANULADA;
    /// AUTORIZADA → APLICADA / ANULADA; APLICADA → AUTORIZADA (reversal). Terminal RECHAZADA / ANULADA allow no
    /// transition (the settlement-driven reopen of a future-annulled record is a separate mutator, not a normal
    /// transition).
    /// </summary>
    public static bool CanTransition(string fromStatus, string toStatus)
    {
        if (fromStatus == toStatus)
        {
            return false;
        }

        return fromStatus switch
        {
            OvertimeRecordStatuses.EnRevision =>
                toStatus is OvertimeRecordStatuses.Autorizada
                    or OvertimeRecordStatuses.Rechazada
                    or OvertimeRecordStatuses.Anulada,
            OvertimeRecordStatuses.Autorizada =>
                toStatus is OvertimeRecordStatuses.Aplicada
                    or OvertimeRecordStatuses.Anulada,
            OvertimeRecordStatuses.Aplicada =>
                toStatus is OvertimeRecordStatuses.Autorizada,
            _ => false,
        };
    }

    /// <summary>
    /// Validates that the single application may be registered now (RN-06/№13): the record must be AUTORIZADA,
    /// its work date must have elapsed (a future organized shift is not payable) and it must not already carry an
    /// active application.
    /// </summary>
    public static OvertimeRecordRuleResult CanApply(string statusCode, bool hasActiveApplication, DateOnly workDate, DateOnly today)
    {
        if (statusCode != OvertimeRecordStatuses.Autorizada)
        {
            return OvertimeRecordRuleResult.Fail(NotApplicableCode);
        }

        if (workDate > today)
        {
            return OvertimeRecordRuleResult.Fail(WorkDateNotElapsedCode);
        }

        return hasActiveApplication
            ? OvertimeRecordRuleResult.Fail(AlreadyAppliedCode)
            : OvertimeRecordRuleResult.Ok;
    }

    /// <summary>Validates that the payroll destination may be re-targeted now: only while AUTORIZADA.</summary>
    public static OvertimeRecordRuleResult CanRetarget(string statusCode) =>
        statusCode == OvertimeRecordStatuses.Autorizada
            ? OvertimeRecordRuleResult.Ok
            : OvertimeRecordRuleResult.Fail(NotRetargetableCode);

    /// <summary>Validates that the active application may be reverted now: only while APLICADA.</summary>
    public static OvertimeRecordRuleResult CanRevertApplication(string statusCode) =>
        statusCode == OvertimeRecordStatuses.Aplicada
            ? OvertimeRecordRuleResult.Ok
            : OvertimeRecordRuleResult.Fail(ApplicationNotRevertibleCode);

    /// <summary>
    /// True when the record is overdue: the target payroll period already ended before <paramref name="today"/>.
    /// A null end date is not derivable (the destination is a free-text label), so it is never overdue.
    /// </summary>
    public static bool IsOverdue(DateOnly? payrollPeriodEndDate, DateOnly today) =>
        payrollPeriodEndDate is { } endDate && endDate < today;

    /// <summary>
    /// Validates the work date (№13): a past OR future date is permitted (the shift may be organized ahead), but
    /// when a sanity cap is supplied a date more than <paramref name="sanityCapDays"/> in the future is rejected
    /// (guards against year typos). A null cap disables the check.
    /// </summary>
    public static OvertimeRecordRuleResult ValidateWorkDate(DateOnly workDate, DateOnly today, int? sanityCapDays)
    {
        if (sanityCapDays is { } cap && workDate > today.AddDays(cap))
        {
            return OvertimeRecordRuleResult.Fail(WorkDateTooFarCode);
        }

        return OvertimeRecordRuleResult.Ok;
    }

    // ── Settlement helpers (№15) ────────────────────────────────────────────────────────────────────

    /// <summary>Factored hours of the pending records = <c>Round2(Σ decimalHours × factor)</c> (RF-014).</summary>
    public static decimal FactoredHours(IEnumerable<OvertimeFactoredRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var total = 0m;
        foreach (var record in records)
        {
            total += record.DecimalHours * record.Factor;
        }

        return Round2(total);
    }

    /// <summary>
    /// Hourly rate = <c>Round2(dailySalary / standardDailyHours)</c> (RF-014). Reuses
    /// <see cref="CompensatoryTimeRules.HourlyRate"/> so the whole system keeps a single hourly-rate rounding.
    /// </summary>
    public static decimal HourlyRate(decimal dailySalary, decimal standardDailyHours) =>
        CompensatoryTimeRules.HourlyRate(dailySalary, standardDailyHours);

    /// <summary>Settlement amount of the pending overtime = <c>Round2(factoredHours × hourlyRate)</c> (RF-014).</summary>
    public static decimal SettlementAmount(decimal factoredHours, decimal hourlyRate) =>
        Round2(factoredHours * hourlyRate);
}
