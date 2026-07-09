using CLARIHR.Application.Common.Errors;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles.Compensation;

/// <summary>
/// Dedicated handler-level errors for one-time incomes (REQ-006 §3.3 CRUD + resolution + re-imputation). Each code
/// requires an EN + ES resource entry (parity: <c>BackendMessageLocalizationTests</c>). The value-coherence /
/// concept / transition codes are produced by the pure <see cref="OneTimeIncomeRules"/> and already localized
/// (PR-2); these cover the cross-aggregate (catalog / plaza / cost-center / requester) checks and the
/// decision-flow guards that need a database or the request context. Field-level validation (required codes,
/// positive value) is the validator's job (400) and is NOT here.
/// </summary>
internal static class OneTimeIncomeErrors
{
    public static readonly Error ConceptInvalid = new(
        "ONE_TIME_INCOME_CONCEPT_INVALID",
        "The compensation concept is not a valid active concept for the company's country.", ErrorType.UnprocessableEntity);

    public static readonly Error PayrollTypeInvalid = new(
        "ONE_TIME_INCOME_PAYROLL_TYPE_INVALID",
        "The payroll type is not valid for the active catalog.", ErrorType.UnprocessableEntity);

    public static readonly Error CostCenterMissing = new(
        "ONE_TIME_INCOME_COST_CENTER_MISSING",
        "The assigned position has no cost center configured; a cost center is required for a one-time income.", ErrorType.UnprocessableEntity);

    public static readonly Error AssignedPositionInvalid = new(
        "ONE_TIME_INCOME_ASSIGNED_POSITION_INVALID",
        "The assigned position is not a valid plaza of this employee.", ErrorType.UnprocessableEntity);

    public static readonly Error RequesterInvalid = new(
        "ONE_TIME_INCOME_REQUESTER_INVALID",
        "The requester is not a valid personnel file of the company.", ErrorType.UnprocessableEntity);

    public static readonly Error IncomeDateInFuture = new(
        "ONE_TIME_INCOME_INCOME_DATE_IN_FUTURE",
        "The income date cannot be in the future.", ErrorType.UnprocessableEntity);

    public static readonly Error StatusInvalid = new(
        "ONE_TIME_INCOME_STATUS_INVALID",
        "The target status is not a valid resolution target for the active catalog.", ErrorType.UnprocessableEntity);

    public static readonly Error DecisionNoteRequired = new(
        "ONE_TIME_INCOME_DECISION_NOTE_REQUIRED",
        "A decision note is required to reject a one-time income.", ErrorType.UnprocessableEntity);

    public static readonly Error AnnulmentReasonRequired = new(
        "ONE_TIME_INCOME_ANNULMENT_REASON_REQUIRED",
        "An annulment reason is required.", ErrorType.UnprocessableEntity);

    // Separation of duties (TRIPLE anti-self, aclaración №6): neither the subject employee, the registrar nor the
    // requester (its linked login) may decide or revoke the one-time income. 403 (Forbidden).
    public static readonly Error SelfApprovalForbidden = new(
        "ONE_TIME_INCOME_SELF_APPROVAL_FORBIDDEN",
        "The subject employee, the registrar or the requester cannot decide or revoke the one-time income.", ErrorType.Forbidden);

    // Shares the code the pure rules already localize (RN-01/RN-02); the handler pre-checks the state before the
    // domain mutator so an invalid transition returns a clean 422 instead of a 500.
    public static readonly Error StateRuleViolation = new(
        OneTimeIncomeRules.StateRuleViolationCode,
        "The one-time income is not in a state that allows this operation.", ErrorType.UnprocessableEntity);

    // Dedicated code for the re-imputation ("enviar a otro periodo", RF-005): only an AUTORIZADO income can be
    // re-targeted — a distinct, actionable 422 (vs the generic state violation).
    public static readonly Error NotRetargetable = new(
        OneTimeIncomeRules.NotRetargetableCode,
        "Only an authorized one-time income can be re-targeted to another payroll period.", ErrorType.UnprocessableEntity);
}

/// <summary>A generic pass/fail rule outcome carrying the bilingual <c>extensions.code</c> when it fails.</summary>
public readonly record struct OneTimeIncomeRuleResult(bool IsValid, string? ErrorCode)
{
    public static OneTimeIncomeRuleResult Ok { get; } = new(true, null);

    public static OneTimeIncomeRuleResult Fail(string errorCode) => new(false, errorCode);
}

/// <summary>Result of <see cref="OneTimeIncomeRules.ComputeAmount"/>: the rounded amount, or an error code.</summary>
public sealed record OneTimeIncomeAmountComputation(bool IsValid, string? ErrorCode, decimal? Amount)
{
    public static OneTimeIncomeAmountComputation Failure(string errorCode) => new(false, errorCode, null);

    public static OneTimeIncomeAmountComputation Success(decimal amount) => new(true, null, amount);
}

/// <summary>
/// Result of <see cref="OneTimeIncomeRules.ValidateValue"/>: the resolved (expected) amount when valid, or an
/// error code with — on a mismatch — the amount the components actually compute to, so the handler can surface
/// the expected breakdown.
/// </summary>
public sealed record OneTimeIncomeValueValidation(bool IsValid, string? ErrorCode, decimal? ExpectedAmount)
{
    public static OneTimeIncomeValueValidation Failure(string errorCode, decimal? expectedAmount = null) =>
        new(false, errorCode, expectedAmount);

    public static OneTimeIncomeValueValidation Success(decimal expectedAmount) => new(true, null, expectedAmount);
}

/// <summary>
/// The one-time-income value + lifecycle arithmetic (REQ-006 §3.2, golden suite A.3) — 100% pure and
/// deterministic: no clock, no database, no side-effects. This is the SINGLE source of truth for the value
/// coherence (fixed vs computed), the amount derivation (quantity × unit value × multiplier, or a percentage
/// over a base), the concept eligibility, the overdue derivation and the state-machine transitions, so the
/// write validations, the settlement suggestion and the read projections all cuadran by construction.
/// <para>Single rounding rule (mirrors <c>SettlementCalculationRules.Round2</c>): half-up away-from-zero,
/// 2 decimals — the ONLY rounding point of the module.</para>
/// </summary>
public static class OneTimeIncomeRules
{
    /// <summary>The implicit multiplier of a CANTIDAD_POR_VALOR value when none is provided (D-07).</summary>
    public const decimal DefaultMultiplier = 1.00m;

    // ── Value error codes (bilingual; localization parity kept in BackendMessages[.es].resx) ──────────
    public const string ValueMethodRequiredCode = "ONE_TIME_INCOME_VALUE_METHOD_REQUIRED";
    public const string ValueMethodInvalidCode = "ONE_TIME_INCOME_VALUE_METHOD_INVALID";
    public const string ValueComponentsInvalidCode = "ONE_TIME_INCOME_VALUE_COMPONENTS_INVALID";
    public const string ValueFixedWithComponentsCode = "ONE_TIME_INCOME_VALUE_FIXED_WITH_COMPONENTS";
    public const string ValueAmountInvalidCode = "ONE_TIME_INCOME_VALUE_AMOUNT_INVALID";
    public const string AmountMismatchCode = "ONE_TIME_INCOME_AMOUNT_MISMATCH";

    // ── Concept error codes (D-03) ────────────────────────────────────────────────────────────────────
    public const string ConceptNotIncomeCode = "ONE_TIME_INCOME_CONCEPT_NOT_INCOME";
    public const string ConceptIsBaseSalaryCode = "ONE_TIME_INCOME_CONCEPT_IS_BASE_SALARY";

    // ── Application + transition error codes ──────────────────────────────────────────────────────────
    public const string NotApplicableCode = "ONE_TIME_INCOME_NOT_APPLICABLE";
    public const string AlreadyAppliedCode = "ONE_TIME_INCOME_ALREADY_APPLIED";
    public const string NotRetargetableCode = "ONE_TIME_INCOME_NOT_RETARGETABLE";
    public const string ApplicationNotRevertibleCode = "ONE_TIME_INCOME_APPLICATION_NOT_REVERTIBLE";
    public const string StateRuleViolationCode = "ONE_TIME_INCOME_STATE_RULE_VIOLATION";

    /// <summary>Single rounding rule of the module: half-up away-from-zero, 2 decimals.</summary>
    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Computes the amount of a non-fixed one-time income from its calculation method + components (D-07),
    /// validating the method↔component pairing (missing / surplus → error) and positivity:
    /// <list type="bullet">
    /// <item>CANTIDAD_POR_VALOR = <c>quantity × unitValue × multiplier</c> (multiplier defaults to 1.00); the
    /// percentage and base amount must be absent.</item>
    /// <item>PORCENTAJE_SOBRE_BASE = <c>percentage% × baseAmount</c>; the quantity, unit value and multiplier
    /// must be absent.</item>
    /// </list>
    /// The result is rounded to 2 decimals and must be strictly positive.
    /// </summary>
    public static OneTimeIncomeAmountComputation ComputeAmount(
        string? calculationMethod,
        decimal? quantity,
        decimal? unitValue,
        decimal? multiplier,
        decimal? percentage,
        decimal? baseAmount)
    {
        switch (calculationMethod)
        {
            case OneTimeIncomeCalculationMethods.QuantityTimesValue:
            {
                if (percentage is not null || baseAmount is not null)
                {
                    return OneTimeIncomeAmountComputation.Failure(ValueComponentsInvalidCode);
                }

                if (quantity is not { } q || unitValue is not { } u)
                {
                    return OneTimeIncomeAmountComputation.Failure(ValueComponentsInvalidCode);
                }

                var m = multiplier ?? DefaultMultiplier;
                if (q <= 0m || u <= 0m || m <= 0m)
                {
                    return OneTimeIncomeAmountComputation.Failure(ValueComponentsInvalidCode);
                }

                var amount = Round2(q * u * m);
                return amount <= 0m
                    ? OneTimeIncomeAmountComputation.Failure(ValueComponentsInvalidCode)
                    : OneTimeIncomeAmountComputation.Success(amount);
            }

            case OneTimeIncomeCalculationMethods.PercentageOnBase:
            {
                if (quantity is not null || unitValue is not null || multiplier is not null)
                {
                    return OneTimeIncomeAmountComputation.Failure(ValueComponentsInvalidCode);
                }

                if (percentage is not { } p || baseAmount is not { } b)
                {
                    return OneTimeIncomeAmountComputation.Failure(ValueComponentsInvalidCode);
                }

                if (p <= 0m || b <= 0m)
                {
                    return OneTimeIncomeAmountComputation.Failure(ValueComponentsInvalidCode);
                }

                var amount = Round2(p / 100m * b);
                return amount <= 0m
                    ? OneTimeIncomeAmountComputation.Failure(ValueComponentsInvalidCode)
                    : OneTimeIncomeAmountComputation.Success(amount);
            }

            default:
                return OneTimeIncomeAmountComputation.Failure(ValueMethodInvalidCode);
        }
    }

    /// <summary>
    /// Validates the value of a one-time income (D-07). A fixed value carries no method or components and a
    /// positive amount. A computed value carries a method with the complete matching components; its declared
    /// <paramref name="amount"/> must equal the computed one after rounding, otherwise
    /// <see cref="AmountMismatchCode"/> is returned carrying the expected amount.
    /// </summary>
    public static OneTimeIncomeValueValidation ValidateValue(
        bool isFixedValue,
        string? calculationMethod,
        decimal? quantity,
        decimal? unitValue,
        decimal? multiplier,
        decimal? percentage,
        decimal? baseAmount,
        decimal? amount)
    {
        if (isFixedValue)
        {
            if (calculationMethod is not null
                || quantity is not null || unitValue is not null || multiplier is not null
                || percentage is not null || baseAmount is not null)
            {
                return OneTimeIncomeValueValidation.Failure(ValueFixedWithComponentsCode);
            }

            if (amount is not { } fixedAmount || fixedAmount <= 0m)
            {
                return OneTimeIncomeValueValidation.Failure(ValueAmountInvalidCode);
            }

            return OneTimeIncomeValueValidation.Success(Round2(fixedAmount));
        }

        if (calculationMethod is null)
        {
            return OneTimeIncomeValueValidation.Failure(ValueMethodRequiredCode);
        }

        var computation = ComputeAmount(calculationMethod, quantity, unitValue, multiplier, percentage, baseAmount);
        if (!computation.IsValid)
        {
            return OneTimeIncomeValueValidation.Failure(computation.ErrorCode!);
        }

        // The amount is DERIVED from the components (the server is the source of truth, №11): the request may omit
        // it. When it IS supplied it is cross-checked against the computed value after rounding (mismatch → 422 with
        // the expected breakdown); when omitted the computed value stands (plan §0.11, HU-002).
        var expected = computation.Amount!.Value;
        if (amount is { } provided && Round2(provided) != expected)
        {
            return OneTimeIncomeValueValidation.Failure(AmountMismatchCode, expected);
        }

        return OneTimeIncomeValueValidation.Success(expected);
    }

    /// <summary>
    /// Validates the compensation concept (D-03): it must be an income concept (<see cref="CompensationNature.Ingreso"/>)
    /// and must NOT be a base-salary concept.
    /// </summary>
    public static OneTimeIncomeRuleResult ValidateConcept(CompensationNature nature, bool isBaseSalary)
    {
        if (nature != CompensationNature.Ingreso)
        {
            return OneTimeIncomeRuleResult.Fail(ConceptNotIncomeCode);
        }

        return isBaseSalary
            ? OneTimeIncomeRuleResult.Fail(ConceptIsBaseSalaryCode)
            : OneTimeIncomeRuleResult.Ok;
    }

    /// <summary>
    /// The one-time-income state machine (RN-01/RN-02): EN_REVISION → AUTORIZADO / RECHAZADO / ANULADO;
    /// AUTORIZADO → APLICADO / ANULADO; APLICADO → AUTORIZADO (reversal). Terminal RECHAZADO / ANULADO allow no
    /// transition.
    /// </summary>
    public static bool CanTransition(string fromStatus, string toStatus)
    {
        if (fromStatus == toStatus)
        {
            return false;
        }

        return fromStatus switch
        {
            OneTimeIncomeStatuses.EnRevision =>
                toStatus is OneTimeIncomeStatuses.Autorizado
                    or OneTimeIncomeStatuses.Rechazado
                    or OneTimeIncomeStatuses.Anulado,
            OneTimeIncomeStatuses.Autorizado =>
                toStatus is OneTimeIncomeStatuses.Aplicado
                    or OneTimeIncomeStatuses.Anulado,
            OneTimeIncomeStatuses.Aplicado =>
                toStatus is OneTimeIncomeStatuses.Autorizado,
            _ => false,
        };
    }

    /// <summary>
    /// Validates that the single application may be registered now (RN-06): the income must be AUTORIZADO and
    /// must not already carry an active application.
    /// </summary>
    public static OneTimeIncomeRuleResult CanApply(string statusCode, bool hasActiveApplication)
    {
        if (statusCode != OneTimeIncomeStatuses.Autorizado)
        {
            return OneTimeIncomeRuleResult.Fail(NotApplicableCode);
        }

        return hasActiveApplication
            ? OneTimeIncomeRuleResult.Fail(AlreadyAppliedCode)
            : OneTimeIncomeRuleResult.Ok;
    }

    /// <summary>Validates that the payroll destination may be re-targeted now: only while AUTORIZADO.</summary>
    public static OneTimeIncomeRuleResult CanRetarget(string statusCode) =>
        statusCode == OneTimeIncomeStatuses.Autorizado
            ? OneTimeIncomeRuleResult.Ok
            : OneTimeIncomeRuleResult.Fail(NotRetargetableCode);

    /// <summary>Validates that the active application may be reverted now: only while APLICADO.</summary>
    public static OneTimeIncomeRuleResult CanRevertApplication(string statusCode) =>
        statusCode == OneTimeIncomeStatuses.Aplicado
            ? OneTimeIncomeRuleResult.Ok
            : OneTimeIncomeRuleResult.Fail(ApplicationNotRevertibleCode);

    /// <summary>
    /// True when the income is overdue: the target payroll period already ended before <paramref name="today"/>.
    /// A null end date is not derivable (the destination is a free-text label), so it is never overdue.
    /// </summary>
    public static bool IsOverdue(DateOnly? payrollPeriodEndDate, DateOnly today) =>
        payrollPeriodEndDate is { } endDate && endDate < today;
}
