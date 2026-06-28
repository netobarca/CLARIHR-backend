using CLARIHR.Application.Common.Errors;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Dedicated errors for employee certificate requests ("constancias"). Each code requires an EN + ES resource
/// entry (parity: <c>BackendMessageLocalizationTests</c>). Field-level validation (required type/purpose/method,
/// copies &gt; 0, non-future request date, language) is handled by the validator (400) and is NOT here. The
/// export-format error reuses <c>PersonnelFileErrors.ExportFormatInvalid</c>.
/// </summary>
internal static class CertificateRequestErrors
{
    public static readonly Error TypeCodeInvalid = new(
        "CERTIFICATE_TYPE_CODE_INVALID",
        "The certificate type is not valid for the active catalog.", ErrorType.UnprocessableEntity);

    public static readonly Error StatusCodeInvalid = new(
        "CERTIFICATE_REQUEST_STATUS_CODE_INVALID",
        "The certificate-request status is not valid for the active catalog.", ErrorType.UnprocessableEntity);

    public static readonly Error PurposeCodeInvalid = new(
        "CERTIFICATE_PURPOSE_CODE_INVALID",
        "The certificate purpose is not valid for the active catalog.", ErrorType.UnprocessableEntity);

    public static readonly Error DeliveryMethodCodeInvalid = new(
        "CERTIFICATE_DELIVERY_METHOD_CODE_INVALID",
        "The certificate delivery method is not valid for the active catalog.", ErrorType.UnprocessableEntity);

    public static readonly Error AddresseeRequired = new(
        "CERTIFICATE_ADDRESSEE_REQUIRED",
        "An addressee ('dirigida a') is required for an embassy certificate.", ErrorType.UnprocessableEntity);

    public static readonly Error DateIncoherent = new(
        "CERTIFICATE_DATE_INCOHERENT",
        "The issue date cannot precede the request date, nor the delivery date the issue date.", ErrorType.UnprocessableEntity);

    public static readonly Error StateRuleViolation = new(
        "CERTIFICATE_STATE_RULE_VIOLATION",
        "The certificate request is not in a state that allows this operation.", ErrorType.UnprocessableEntity);

    public static readonly Error GenerationDataUnavailable = new(
        "CERTIFICATE_GENERATION_DATA_UNAVAILABLE",
        "The data required to generate this certificate (active assignment, job title or salary) is unavailable.", ErrorType.UnprocessableEntity);

    public static readonly Error CompensationForbidden = new(
        "CERTIFICATE_COMPENSATION_FORBIDDEN",
        "Issuing a certificate that prints salary requires the compensation-view permission.", ErrorType.Forbidden);

    public static readonly Error GenerationFailed = new(
        "CERTIFICATE_GENERATION_FAILED",
        "The certificate PDF could not be generated.", ErrorType.Failure);
}

/// <summary>
/// Pure, unit-testable certificate rules (no database access). Catalog-code validity is database-backed and
/// lives in the handlers. Domain transition guards live on <see cref="PersonnelFileCertificateRequest"/>.
/// </summary>
internal static class CertificateRequestRules
{
    /// <summary>(D-06) The addressee ("dirigida a") is mandatory for an embassy certificate.</summary>
    public static bool RequiresAddressee(string? certificateTypeCode) =>
        string.Equals(certificateTypeCode?.Trim(), CertificateTypes.Embajada, StringComparison.OrdinalIgnoreCase);

    /// <summary>(D-20) Whether the certificate type prints salary → needs ViewCompensation + salary data.</summary>
    public static bool PrintsSalary(string? certificateTypeCode) =>
        certificateTypeCode is not null
        && CertificateTypes.PrintsSalary.Contains(certificateTypeCode.Trim().ToUpperInvariant());
}
