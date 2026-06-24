using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Dedicated errors for medical-claim business rules. Each code requires an EN + ES (+ es-SV) resource
/// entry (parity: <c>BackendMessageLocalizationTests</c>). Field-level validation (required fields,
/// amounts, dates, currency length, claimant-type set) is handled by the validator (400) and is NOT here.
/// </summary>
internal static class MedicalClaimErrors
{
    public static readonly Error InsuranceNotFound = new(
        "MEDICAL_CLAIM_INSURANCE_NOT_FOUND",
        "The selected insurance does not exist for this employee.", ErrorType.UnprocessableEntity);

    public static readonly Error BeneficiaryNotOwned = new(
        "MEDICAL_CLAIM_BENEFICIARY_NOT_OWNED",
        "The selected beneficiary does not belong to the claim's insurance.", ErrorType.UnprocessableEntity);

    public static readonly Error TypeCodeInvalid = new(
        "MEDICAL_CLAIM_TYPE_CODE_INVALID",
        "The claim type code is not valid for the active catalog.", ErrorType.UnprocessableEntity);

    public static readonly Error StatusCodeInvalid = new(
        "MEDICAL_CLAIM_STATUS_CODE_INVALID",
        "The claim status code is not valid for the active catalog.", ErrorType.UnprocessableEntity);
}

/// <summary>
/// Pure, unit-testable medical-claim rules (no database access). Mirrors the rules-module pattern used by
/// <c>EmploymentAssignmentRules</c> / <c>InsuranceRules</c>. The medical claim is an independent record
/// (decision D-01: register only), so there are no sibling-context rules (no overlap, no allocation sum).
/// </summary>
internal static class MedicalClaimRules
{
    /// <summary>
    /// (D-06) Reimbursement model: a paid amount that exceeds the claimed amount is NOT blocked; this signals
    /// a soft, informational warning so the API/UI can surface it without failing the operation.
    /// </summary>
    public static bool IsReimbursementOverpay(decimal? claimAmount, decimal? paidAmount) =>
        claimAmount is { } claimed && paidAmount is { } paid && paid > claimed;
}
