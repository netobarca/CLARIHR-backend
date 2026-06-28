namespace CLARIHR.Api.Contracts.PersonnelFiles;

/// <summary>Body for creating a certificate request (employee self-service, or HR on the employee's behalf).</summary>
public sealed record AddCertificateRequestRequest(
    string TypeCode,
    string PurposeCode,
    string? AddressedTo,
    string DeliveryMethodCode,
    string? LanguageCode,
    int? Copies,
    DateTime RequestDateUtc,
    DateTime? NeededByDateUtc);

/// <summary>Body for editing a certificate request's business fields (HR).</summary>
public sealed record UpdateCertificateRequestRequest(
    string TypeCode,
    string PurposeCode,
    string? AddressedTo,
    string DeliveryMethodCode,
    string? LanguageCode,
    int? Copies,
    DateTime RequestDateUtc,
    DateTime? NeededByDateUtc);

/// <summary>Body for the HR issuance action: generates the PDF and transitions the request to EMITIDA.</summary>
public sealed record IssueCertificateRequestRequest(string? Notes);

/// <summary>Body for marking an issued certificate as delivered.</summary>
public sealed record DeliverCertificateRequestRequest(DateTime DeliveredDateUtc);

/// <summary>Body for rejecting a pending certificate request.</summary>
public sealed record RejectCertificateRequestRequest(string? Notes);

/// <summary>
/// Body for attaching a manual override document (PR-7). <c>FilePublicId</c> references an already-uploaded file
/// (purpose <c>CertificateRequestDocument</c>).
/// </summary>
public sealed record AddCertificateRequestDocumentRequest(
    Guid FilePublicId,
    string? Observations);

/// <summary>Filters for the company-wide certificate-request bandeja (D-08).</summary>
public sealed record QueryCertificateRequestsRequest(
    string? TypeCode,
    string? StatusCode,
    string? PurposeCode,
    Guid? EmployeeId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Search,
    int? PageNumber,
    int? PageSize);

/// <summary>Body for replacing the company certificate settings (D-17): letterhead, signatory and footer.</summary>
public sealed record UpdateCompanyCertificateSettingsRequest(
    Guid? LogoFilePublicId,
    string? IssuingCity,
    string? SignatoryName,
    string? SignatoryTitle,
    string? FooterText);
