using CLARIHR.Application.Common.Errors;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Dedicated errors for employee economic-aid requests ("ayuda económica"). Each code requires an EN + ES
/// resource entry (parity: <c>BackendMessageLocalizationTests</c>). Field-level validation (required type,
/// requested amount &gt; 0, currency length, non-future request date, resolution-target validity) is handled by
/// the validator (400) and is NOT here.
/// </summary>
internal static class EconomicAidErrors
{
    public static readonly Error TypeCodeInvalid = new(
        "ECONOMIC_AID_TYPE_CODE_INVALID",
        "The economic-aid type is not valid for the active catalog.", ErrorType.UnprocessableEntity);

    public static readonly Error StatusCodeInvalid = new(
        "ECONOMIC_AID_STATUS_CODE_INVALID",
        "The economic-aid status is not valid for the active catalog.", ErrorType.UnprocessableEntity);

    public static readonly Error CurrencyRequired = new(
        "ECONOMIC_AID_CURRENCY_REQUIRED",
        "A currency is required and no default currency is configured for the company.", ErrorType.UnprocessableEntity);

    public static readonly Error ApprovedAmountInvalid = new(
        "ECONOMIC_AID_APPROVED_AMOUNT_INVALID",
        "The approved amount must be greater than zero when approving a request.", ErrorType.UnprocessableEntity);

    public static readonly Error DateIncoherent = new(
        "ECONOMIC_AID_DATE_INCOHERENT",
        "The resolution date cannot precede the request date, nor the disbursement date the resolution date.", ErrorType.UnprocessableEntity);

    public static readonly Error EligibilityNotMet = new(
        "ECONOMIC_AID_ELIGIBILITY_NOT_MET",
        "The employee does not meet the minimum seniority required to request economic aid.", ErrorType.UnprocessableEntity);

    public static readonly Error StateRuleViolation = new(
        "ECONOMIC_AID_STATE_RULE_VIOLATION",
        "The economic-aid request is not in a state that allows this operation.", ErrorType.UnprocessableEntity);

    public static readonly Error PaymentMethodInvalid = new(
        "ECONOMIC_AID_PAYMENT_METHOD_INVALID",
        "The payment method is not valid for the active catalog.", ErrorType.UnprocessableEntity);

    public static readonly Error SelfApprovalForbidden = new(
        "ECONOMIC_AID_SELF_APPROVAL_FORBIDDEN",
        "An employee cannot validate their own economic-aid request.", ErrorType.Forbidden);
}

/// <summary>
/// Pure, unit-testable economic-aid rules (no database access). Cross-aggregate checks (catalog codes, currency
/// default, payment-method validity) are database-backed and live in the handlers, not here. Domain transition
/// guards live on <see cref="PersonnelFileEconomicAidRequest"/>.
/// </summary>
internal static class EconomicAidRequestRules
{
    /// <summary>(D-08) Minimum-seniority eligibility in months; <c>null</c>/≤0 ⇒ no restriction.</summary>
    public static bool MeetsMinimumSeniority(DateTime hireDateUtc, DateTime asOfUtc, int? minimumMonths)
    {
        if (minimumMonths is not > 0)
        {
            return true;
        }

        var seniority = EmployeeSeniority.Between(hireDateUtc, asOfUtc);
        return ((seniority.Years * 12) + seniority.Months) >= minimumMonths.Value;
    }

    /// <summary>(D-05) When approving, the approved amount must be &gt; 0 (partial allowed; 0 ⇒ use RECHAZADA).</summary>
    public static bool IsValidApprovedAmount(string targetStatusCode, decimal? approvedAmount) =>
        !string.Equals(targetStatusCode?.Trim(), EconomicAidRequestStatuses.Aprobada, StringComparison.OrdinalIgnoreCase)
        || approvedAmount is > 0m;

    /// <summary>True when the target code is one a manager may set via the resolution action.</summary>
    public static bool IsResolutionTarget(string? targetStatusCode) =>
        targetStatusCode is not null
        && EconomicAidRequestStatuses.ResolutionTargets.Contains(targetStatusCode.Trim().ToUpperInvariant());
}
