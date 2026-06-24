using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Dedicated errors for off-payroll-transaction business rules ("transacciones fuera de nómina"). Each code
/// requires an EN + ES resource entry (parity: <c>BackendMessageLocalizationTests</c>). Field-level validation
/// (required type, non-zero amount, month/year range, currency length, future date) is handled by the validator
/// (400) and is NOT here.
/// </summary>
internal static class OffPayrollTransactionErrors
{
    public static readonly Error TypeCodeInvalid = new(
        "OFF_PAYROLL_TX_TYPE_CODE_INVALID",
        "The off-payroll transaction type is not valid for the active catalog.", ErrorType.UnprocessableEntity);

    public static readonly Error CurrencyRequired = new(
        "OFF_PAYROLL_TX_CURRENCY_REQUIRED",
        "A currency is required and no default currency is configured for the company.", ErrorType.UnprocessableEntity);

    public static readonly Error AssetAccessNotFound = new(
        "OFF_PAYROLL_TX_ASSET_ACCESS_NOT_FOUND",
        "The linked asset/access does not exist for this employee.", ErrorType.UnprocessableEntity);

    public static readonly Error CorrectionRequired = new(
        "OFF_PAYROLL_TX_CORRECTION_REQUIRED",
        "A negative amount must reference the original transaction it corrects.", ErrorType.UnprocessableEntity);

    public static readonly Error CorrectedNotFound = new(
        "OFF_PAYROLL_TX_CORRECTED_NOT_FOUND",
        "The referenced original transaction does not exist for this employee.", ErrorType.UnprocessableEntity);

    public static readonly Error CorrectedInvalid = new(
        "OFF_PAYROLL_TX_CORRECTED_INVALID",
        "The referenced original transaction must be an active original expense in the same currency.", ErrorType.UnprocessableEntity);
}

/// <summary>
/// Pure, unit-testable off-payroll-transaction rules (no database access). Mirrors the rules-module pattern used
/// by <c>MedicalClaimRules</c> / <c>InsuranceRules</c>. Cross-aggregate checks (catalog, currency default,
/// AssetAccess ownership, correction-reference existence) are database-backed and live in
/// <c>OffPayrollTransactionWriteSupport</c>, not here.
/// </summary>
internal static class OffPayrollTransactionRules
{
    /// <summary>
    /// (D-12) A negative amount (adjustment / credit note) must reference the original transaction it corrects;
    /// a positive amount never requires one. Returns true when the reference is missing for a negative amount.
    /// </summary>
    public static bool RequiresCorrectionReference(decimal amount, Guid? correctsTransactionPublicId) =>
        amount < 0 && correctsTransactionPublicId is null;

    /// <summary>(D-05) Imputation period is valid: month in 1..12 and year in a sane range.</summary>
    public static bool IsValidPeriod(int year, int month) =>
        month is >= 1 and <= 12 && year is >= 2000 and <= 2100;
}
