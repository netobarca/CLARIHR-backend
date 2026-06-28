using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PersonnelFiles;

/// <summary>
/// Company-level certificate formatting settings (D-17): the configurable letterhead/logo, issuing city,
/// signatory and footer/legal text merged into every generated certificate PDF. One row per tenant (mirrors
/// <c>CompanyPreference</c>). The certificate body per type is structural (code), not editable here.
/// </summary>
public sealed class CompanyCertificateSettings : TenantEntity
{
    private CompanyCertificateSettings()
    {
    }

    private CompanyCertificateSettings(Guid publicId)
    {
        PublicId = publicId;
        ConcurrencyToken = Guid.NewGuid();
    }

    public Guid? LogoFilePublicId { get; private set; }

    public string? IssuingCity { get; private set; }

    public string? SignatoryName { get; private set; }

    public string? SignatoryTitle { get; private set; }

    public string? FooterText { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static CompanyCertificateSettings Create() => new(Guid.NewGuid());

    public void Update(
        Guid? logoFilePublicId,
        string? issuingCity,
        string? signatoryName,
        string? signatoryTitle,
        string? footerText)
    {
        LogoFilePublicId = logoFilePublicId is { } id && id != Guid.Empty ? id : null;
        IssuingCity = Trim(issuingCity);
        SignatoryName = Trim(signatoryName);
        SignatoryTitle = Trim(signatoryTitle);
        FooterText = Trim(footerText);
        ConcurrencyToken = Guid.NewGuid();
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
