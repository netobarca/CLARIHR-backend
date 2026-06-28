using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.Abstractions.PersonnelFiles;

/// <summary>
/// Server-side merge data for generating a certificate PDF (D-15/D-16). All values are resolved from the
/// expediente (never accepted from the client). For a salary-printing type (D-20) <see cref="MonthlySalary"/>
/// and <see cref="CurrencyCode"/> are populated; otherwise null. The data provider returns <c>null</c> when a
/// required piece is missing (no active assignment / job title / hire date, or no salary for a salary type) →
/// the handler maps that to <c>CERTIFICATE_GENERATION_DATA_UNAVAILABLE</c> (E-17).
/// </summary>
public sealed record CertificatePrintPayload(
    string CertificateTypeCode,
    string LanguageCode,
    string? AddressedTo,
    int Copies,
    string FullName,
    string? IdentificationType,
    string? IdentificationNumber,
    string JobTitle,
    DateTime HireDate,
    int SeniorityYears,
    int SeniorityMonths,
    decimal? MonthlySalary,
    string? CurrencyCode,
    Guid? LogoFilePublicId,
    string? IssuingCity,
    string? SignatoryName,
    string? SignatoryTitle,
    string? FooterText,
    DateTime GeneratedAtUtc);

/// <summary>Resolves the server-side merge data for a certificate (D-16). Returns null when data is insufficient.</summary>
public interface ICertificatePrintDataProvider
{
    Task<CertificatePrintPayload?> BuildAsync(
        Guid personnelFilePublicId,
        Guid tenantId,
        PersonnelFileCertificateRequestResponse request,
        DateTime generatedAtUtc,
        CancellationToken cancellationToken);
}

/// <summary>
/// Generates the certificate PDF from the merge payload, uploads it to blob storage (server-side) and registers
/// it as the issued <c>CertificateRequestDocument</c> (system-generated). The entities are added to the unit of
/// work; the calling handler commits them together with the status transition.
/// </summary>
public interface ICertificateIssuanceService
{
    Task GenerateAndStoreAsync(
        Guid tenantId,
        long certificateRequestInternalId,
        Guid certificateRequestPublicId,
        CertificatePrintPayload payload,
        string createdByUserId,
        CancellationToken cancellationToken);
}
