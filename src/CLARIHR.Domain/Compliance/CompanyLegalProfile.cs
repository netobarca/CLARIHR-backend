using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Compliance;

/// <summary>
/// Employer legal identity used as the header of the payroll compliance reports (F-14, Planilla Única,
/// Planilla Patronal — REQ-016). One record per company; created and edited by the company administrator.
/// Its existence gates payroll generation once <c>CompanyPreference.PayrollComplianceGatesEnabled</c> is
/// turned on for the tenant (RF-006, ratified P-03).
/// </summary>
public sealed class CompanyLegalProfile : TenantEntity
{
    private CompanyLegalProfile()
    {
    }

    private CompanyLegalProfile(
        Guid publicId,
        string legalName,
        string employerNitNumber,
        string isssEmployerRegistrationNumber,
        string fiscalAddress,
        string? economicActivityDescription,
        Guid? legalRepresentativePublicId)
    {
        PublicId = publicId;
        LegalName = RequireText(legalName, nameof(legalName));
        EmployerNitNumber = RequireText(employerNitNumber, nameof(employerNitNumber));
        IsssEmployerRegistrationNumber = RequireText(isssEmployerRegistrationNumber, nameof(isssEmployerRegistrationNumber));
        FiscalAddress = RequireText(fiscalAddress, nameof(fiscalAddress));
        EconomicActivityDescription = string.IsNullOrWhiteSpace(economicActivityDescription)
            ? null
            : economicActivityDescription.Trim();
        LegalRepresentativePublicId = legalRepresentativePublicId;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Razón social — the company's legal name before Hacienda.</summary>
    public string LegalName { get; private set; } = string.Empty;

    /// <summary>NIT patronal — the employer's own tax identification number.</summary>
    public string EmployerNitNumber { get; private set; } = string.Empty;

    /// <summary>NRC / registro patronal — the employer's registration number before the ISSS.</summary>
    public string IsssEmployerRegistrationNumber { get; private set; } = string.Empty;

    public string FiscalAddress { get; private set; } = string.Empty;

    /// <summary>
    /// Free-text economic activity (no catalog in this phase — D-06 of the technical plan; a typed CIIU
    /// catalog is out of scope unless the business asks for it).
    /// </summary>
    public string? EconomicActivityDescription { get; private set; }

    /// <summary>Optional link to an already-registered <c>LegalRepresentative</c>.</summary>
    public Guid? LegalRepresentativePublicId { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static CompanyLegalProfile Create(
        string legalName,
        string employerNitNumber,
        string isssEmployerRegistrationNumber,
        string fiscalAddress,
        string? economicActivityDescription,
        Guid? legalRepresentativePublicId) =>
        new(
            Guid.NewGuid(),
            legalName,
            employerNitNumber,
            isssEmployerRegistrationNumber,
            fiscalAddress,
            economicActivityDescription,
            legalRepresentativePublicId);

    public void Update(
        string legalName,
        string employerNitNumber,
        string isssEmployerRegistrationNumber,
        string fiscalAddress,
        string? economicActivityDescription,
        Guid? legalRepresentativePublicId)
    {
        LegalName = RequireText(legalName, nameof(legalName));
        EmployerNitNumber = RequireText(employerNitNumber, nameof(employerNitNumber));
        IsssEmployerRegistrationNumber = RequireText(isssEmployerRegistrationNumber, nameof(isssEmployerRegistrationNumber));
        FiscalAddress = RequireText(fiscalAddress, nameof(fiscalAddress));
        EconomicActivityDescription = string.IsNullOrWhiteSpace(economicActivityDescription)
            ? null
            : economicActivityDescription.Trim();
        LegalRepresentativePublicId = legalRepresentativePublicId;
        ConcurrencyToken = Guid.NewGuid();
    }

    private static string RequireText(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", paramName);
        }

        return value.Trim();
    }
}
