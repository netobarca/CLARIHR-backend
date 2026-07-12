using CLARIHR.Application.Common.Errors;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles.Compensation;

/// <summary>
/// Dedicated handler-level errors for one-time deductions (REQ-009). Each code requires an EN + ES resource entry
/// (parity: <c>BackendMessageLocalizationTests</c>). The value / transition codes are produced by the pure
/// <see cref="OneTimeDeductionRules"/> and localized alongside these.
/// </summary>
internal static class OneTimeDeductionErrors
{
    public static readonly Error ConceptInvalid = new(
        "ONE_TIME_DEDUCTION_CONCEPT_INVALID",
        "The compensation concept is not a valid active non-statutory deduction concept for the company's country.", ErrorType.UnprocessableEntity);

    public static readonly Error PayrollTypeInvalid = new(
        "ONE_TIME_DEDUCTION_PAYROLL_TYPE_INVALID",
        "The payroll type is not valid for the active catalog.", ErrorType.UnprocessableEntity);

    public static readonly Error AssignedPositionInvalid = new(
        "ONE_TIME_DEDUCTION_ASSIGNED_POSITION_INVALID",
        "The assigned position is not a valid plaza of this employee.", ErrorType.UnprocessableEntity);

    public static readonly Error RequesterInvalid = new(
        "ONE_TIME_DEDUCTION_REQUESTER_INVALID",
        "The requester is not a valid personnel file of the company.", ErrorType.UnprocessableEntity);

    public static readonly Error StatusInvalid = new(
        "ONE_TIME_DEDUCTION_STATUS_INVALID",
        "The target status is not a valid resolution target.", ErrorType.UnprocessableEntity);

    public static readonly Error DecisionNoteRequired = new(
        "ONE_TIME_DEDUCTION_DECISION_NOTE_REQUIRED",
        "A decision note is required to reject a one-time deduction.", ErrorType.UnprocessableEntity);

    public static readonly Error AnnulmentReasonRequired = new(
        "ONE_TIME_DEDUCTION_ANNULMENT_REASON_REQUIRED",
        "An annulment reason is required.", ErrorType.UnprocessableEntity);

    public static readonly Error PayrollPeriodInvalid = new(
        "ONE_TIME_DEDUCTION_PAYROLL_PERIOD_INVALID",
        "The payroll period is not a valid active period for the company.", ErrorType.UnprocessableEntity);

    // Separation of duties (TRIPLE anti-self): neither the subject employee, nor the requester, nor the registrar
    // may decide or revoke the one-time deduction. 403 (Forbidden).
    public static readonly Error SelfApprovalForbidden = new(
        "ONE_TIME_DEDUCTION_SELF_APPROVAL_FORBIDDEN",
        "The subject employee, the requester or the registrar cannot decide or revoke the one-time deduction.", ErrorType.Forbidden);

    public static readonly Error PayrollInputRangeRequired = new(
        "ONE_TIME_DEDUCTION_PAYROLL_INPUT_RANGE_REQUIRED",
        "The payroll-input export requires both a start date and an end date.", ErrorType.UnprocessableEntity);
}

/// <summary>Result of <see cref="OneTimeDeductionRules.ComputeAmount"/>: the resolved amount, or an error code.</summary>
public sealed record OneTimeDeductionAmountComputation(bool IsValid, string? ErrorCode, decimal? Amount)
{
    public static OneTimeDeductionAmountComputation Failure(string errorCode) => new(false, errorCode, null);

    public static OneTimeDeductionAmountComputation Success(decimal amount) => new(true, null, amount);
}

/// <summary>Result of <see cref="OneTimeDeductionRules.ValidateValue"/>: the resolved amount, or an error code
/// (carrying the EXPECTED amount when the client's declared one disagrees with its components).</summary>
public sealed record OneTimeDeductionValueValidation(bool IsValid, string? ErrorCode, decimal? Amount, decimal? ExpectedAmount)
{
    public static OneTimeDeductionValueValidation Failure(string errorCode, decimal? expectedAmount = null) =>
        new(false, errorCode, null, expectedAmount);

    public static OneTimeDeductionValueValidation Success(decimal amount) => new(true, null, amount, null);
}

/// <summary>A generic pass/fail rule outcome carrying the bilingual <c>extensions.code</c> when it fails.</summary>
public readonly record struct OneTimeDeductionRuleResult(bool IsValid, string? ErrorCode)
{
    public static OneTimeDeductionRuleResult Ok { get; } = new(true, null);

    public static OneTimeDeductionRuleResult Fail(string errorCode) => new(false, errorCode);
}

/// <summary>
/// The one-time-deduction value + lifecycle arithmetic (REQ-009) — 100 % pure and deterministic: no clock, no
/// database, no side-effects. It is the SINGLE source of truth for the value coherence (a fixed amount, or one
/// DERIVED from persisted components), the concept validation and the state machine.
/// <para>The amount of a computed value is the SERVER's: the components are the truth, and a client-declared
/// amount that does not follow from them is rejected with the expected figure.</para>
/// <para>Single rounding rule: half-up away-from-zero, 2 decimals.</para>
/// </summary>
public static class OneTimeDeductionRules
{
    public const decimal DefaultMultiplier = 1.00m;

    // ── Value error codes (bilingual; localization parity kept in BackendMessages[.es].resx) ──────────
    public const string ValueMethodRequiredCode = "ONE_TIME_DEDUCTION_VALUE_METHOD_REQUIRED";
    public const string ValueMethodInvalidCode = "ONE_TIME_DEDUCTION_VALUE_METHOD_INVALID";
    public const string ValueComponentsInvalidCode = "ONE_TIME_DEDUCTION_VALUE_COMPONENTS_INVALID";
    public const string ValueFixedWithComponentsCode = "ONE_TIME_DEDUCTION_VALUE_FIXED_WITH_COMPONENTS";
    public const string ValueAmountInvalidCode = "ONE_TIME_DEDUCTION_VALUE_AMOUNT_INVALID";
    public const string AmountMismatchCode = "ONE_TIME_DEDUCTION_AMOUNT_MISMATCH";

    // ── Application + transition error codes ──────────────────────────────────────────────────────────
    public const string NotApplicableCode = "ONE_TIME_DEDUCTION_NOT_APPLICABLE";
    public const string AlreadyAppliedCode = "ONE_TIME_DEDUCTION_ALREADY_APPLIED";
    public const string NotRetargetableCode = "ONE_TIME_DEDUCTION_NOT_RETARGETABLE";
    public const string ApplicationNotRevertibleCode = "ONE_TIME_DEDUCTION_APPLICATION_NOT_REVERTIBLE";
    public const string StateRuleViolationCode = "ONE_TIME_DEDUCTION_STATE_RULE_VIOLATION";

    /// <summary>Single rounding rule of the module: half-up away-from-zero, 2 decimals.</summary>
    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The amount a computed value resolves to: <c>CANTIDAD_POR_VALOR</c> = quantity × unit value × multiplier
    /// (the multiplier defaults to 1.00); <c>PORCENTAJE_SOBRE_BASE</c> = percentage % of the base amount.
    /// </summary>
    public static OneTimeDeductionAmountComputation ComputeAmount(
        string? calculationMethod,
        decimal? quantity,
        decimal? unitValue,
        decimal? multiplier,
        decimal? percentage,
        decimal? baseAmount)
    {
        if (string.IsNullOrWhiteSpace(calculationMethod))
        {
            return OneTimeDeductionAmountComputation.Failure(ValueMethodRequiredCode);
        }

        switch (calculationMethod.Trim().ToUpperInvariant())
        {
            case OneTimeDeductionCalculationMethods.QuantityTimesValue:
            {
                if (quantity is not { } q || q <= 0m || unitValue is not { } u || u <= 0m)
                {
                    return OneTimeDeductionAmountComputation.Failure(ValueComponentsInvalidCode);
                }

                var resolvedMultiplier = multiplier ?? DefaultMultiplier;
                if (resolvedMultiplier <= 0m || percentage is not null || baseAmount is not null)
                {
                    return OneTimeDeductionAmountComputation.Failure(ValueComponentsInvalidCode);
                }

                var amount = Round2(q * u * resolvedMultiplier);
                return amount <= 0m
                    ? OneTimeDeductionAmountComputation.Failure(ValueComponentsInvalidCode)
                    : OneTimeDeductionAmountComputation.Success(amount);
            }

            case OneTimeDeductionCalculationMethods.PercentageOnBase:
            {
                if (percentage is not { } p || p <= 0m || baseAmount is not { } b || b <= 0m)
                {
                    return OneTimeDeductionAmountComputation.Failure(ValueComponentsInvalidCode);
                }

                if (quantity is not null || unitValue is not null || multiplier is not null)
                {
                    return OneTimeDeductionAmountComputation.Failure(ValueComponentsInvalidCode);
                }

                var amount = Round2(p / 100m * b);
                return amount <= 0m
                    ? OneTimeDeductionAmountComputation.Failure(ValueComponentsInvalidCode)
                    : OneTimeDeductionAmountComputation.Success(amount);
            }

            default:
                return OneTimeDeductionAmountComputation.Failure(ValueMethodInvalidCode);
        }
    }

    /// <summary>
    /// Validates the value. A fixed value carries no method or components and a positive amount. A computed value
    /// carries a method with the complete matching components; its declared <paramref name="amount"/> may be
    /// OMITTED (the server's computed value stands) but, when supplied, must equal the computed one after
    /// rounding — otherwise <see cref="AmountMismatchCode"/> is returned carrying the expected amount, so a client
    /// cannot smuggle an arbitrary figure past its own components.
    /// </summary>
    public static OneTimeDeductionValueValidation ValidateValue(
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
                return OneTimeDeductionValueValidation.Failure(ValueFixedWithComponentsCode);
            }

            if (amount is not { } fixedAmount || fixedAmount <= 0m)
            {
                return OneTimeDeductionValueValidation.Failure(ValueAmountInvalidCode);
            }

            return OneTimeDeductionValueValidation.Success(Round2(fixedAmount));
        }

        if (calculationMethod is null)
        {
            return OneTimeDeductionValueValidation.Failure(ValueMethodRequiredCode);
        }

        var computation = ComputeAmount(calculationMethod, quantity, unitValue, multiplier, percentage, baseAmount);
        if (!computation.IsValid)
        {
            return OneTimeDeductionValueValidation.Failure(computation.ErrorCode!);
        }

        var expected = computation.Amount!.Value;
        if (amount is { } provided && Round2(provided) != expected)
        {
            return OneTimeDeductionValueValidation.Failure(AmountMismatchCode, expected);
        }

        return OneTimeDeductionValueValidation.Success(expected);
    }

    /// <summary>
    /// The one-time-deduction state machine: EN_REVISION → AUTORIZADO / RECHAZADO / ANULADO;
    /// AUTORIZADO → APLICADO / ANULADO; APLICADO → AUTORIZADO (the REVERSAL). Terminal RECHAZADO / ANULADO allow
    /// no transition.
    /// </summary>
    public static bool CanTransition(string fromStatus, string toStatus)
    {
        if (fromStatus == toStatus)
        {
            return false;
        }

        return fromStatus switch
        {
            OneTimeDeductionStatuses.EnRevision =>
                toStatus is OneTimeDeductionStatuses.Autorizado
                    or OneTimeDeductionStatuses.Rechazado
                    or OneTimeDeductionStatuses.Anulado,
            OneTimeDeductionStatuses.Autorizado =>
                toStatus is OneTimeDeductionStatuses.Aplicado
                    or OneTimeDeductionStatuses.Anulado,
            OneTimeDeductionStatuses.Aplicado =>
                toStatus is OneTimeDeductionStatuses.Autorizado,
            _ => false,
        };
    }

    /// <summary>Validates that the deduction may be charged: AUTORIZADO and with no active application.</summary>
    public static OneTimeDeductionRuleResult CanApply(string statusCode, bool hasActiveApplication)
    {
        if (statusCode != OneTimeDeductionStatuses.Autorizado)
        {
            return OneTimeDeductionRuleResult.Fail(NotApplicableCode);
        }

        return hasActiveApplication
            ? OneTimeDeductionRuleResult.Fail(AlreadyAppliedCode)
            : OneTimeDeductionRuleResult.Ok;
    }

    /// <summary>Validates that the payroll destination may be re-targeted: only while AUTORIZADO.</summary>
    public static OneTimeDeductionRuleResult CanRetarget(string statusCode) =>
        statusCode == OneTimeDeductionStatuses.Autorizado
            ? OneTimeDeductionRuleResult.Ok
            : OneTimeDeductionRuleResult.Fail(NotRetargetableCode);

    /// <summary>Validates that the application may be reverted: the deduction must be APLICADO.</summary>
    public static OneTimeDeductionRuleResult CanRevertApplication(string statusCode) =>
        statusCode == OneTimeDeductionStatuses.Aplicado
            ? OneTimeDeductionRuleResult.Ok
            : OneTimeDeductionRuleResult.Fail(ApplicationNotRevertibleCode);
}
