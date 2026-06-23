using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Coded errors for the employee asset/access hardening (Nivel A): catalog-backed type code (RF-102) and
/// delivery-status code (RF-103), plus intra-record date coherence (RF-101). Every code must have a matching
/// entry in BackendMessages.resx and BackendMessages.es.resx (parity is enforced by
/// <c>BackendMessageLocalizationTests</c>).
/// </summary>
internal static class AssetAccessErrors
{
    public static readonly Error AssetTypeCodeInvalid = new(
        "ASSET_ACCESS_TYPE_CODE_INVALID",
        "The asset/access type code is not valid for the active catalog.",
        ErrorType.UnprocessableEntity);

    public static readonly Error DeliveryStatusCodeInvalid = new(
        "ASSET_ACCESS_DELIVERY_STATUS_CODE_INVALID",
        "The delivery status code is not valid for the active catalog.",
        ErrorType.UnprocessableEntity);

    public static readonly Error DateRangeInvalid = new(
        "ASSET_ACCESS_DATE_RANGE_INVALID",
        "The end date cannot be earlier than the start date.",
        ErrorType.UnprocessableEntity);

    public static readonly Error DeliveryDateInvalid = new(
        "ASSET_ACCESS_DELIVERY_DATE_INVALID",
        "The delivery date cannot be earlier than the start date.",
        ErrorType.UnprocessableEntity);
}

/// <summary>
/// Pure intra-record rules for employee assets/accesses. Unlike employment assignments (<see
/// cref="EmploymentAssignmentRules"/>) or authorization substitutions (<see cref="AuthorizationSubstitutionRules"/>),
/// asset/access records legitimately coexist — an employee holds a laptop, a phone and a uniform at the same
/// time — so there is no cross-row overlap / single-active invariant; the only invariant is date coherence.
/// Keeping it pure makes every check unit-testable without a database.
/// </summary>
internal static class AssetAccessRules
{
    /// <summary>
    /// Validates that the optional end and delivery dates are coherent with the start date. Both are optional
    /// (a deregistration or a delivery may not have happened yet), so coherence is only enforced when present.
    /// </summary>
    public static Result ValidateDates(DateTime startDateUtc, DateTime? endDateUtc, DateTime? deliveryDateUtc)
    {
        if (endDateUtc is { } end && end < startDateUtc)
        {
            return Result.Failure(AssetAccessErrors.DateRangeInvalid);
        }

        if (deliveryDateUtc is { } delivery && delivery < startDateUtc)
        {
            return Result.Failure(AssetAccessErrors.DeliveryDateInvalid);
        }

        return Result.Success();
    }
}
