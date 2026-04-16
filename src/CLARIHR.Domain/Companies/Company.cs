using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Companies;

public sealed class Company : AuditableEntity
{
    private Company()
    {
    }

    private Company(
        Guid publicId,
        string name,
        string slug,
        string countryCode,
        long countryCatalogItemId,
        string defaultLocale,
        CompanyStatus status,
        Guid createdByUserPublicId,
        long? companyTypeCatalogItemId,
        bool isBillable,
        DateTime? billableSinceUtc)
    {
        if (createdByUserPublicId == Guid.Empty)
        {
            throw new ArgumentException("Created by user id cannot be empty.", nameof(createdByUserPublicId));
        }

        PublicId = publicId;
        Name = CompanyNormalization.Clean(name, nameof(name));
        Slug = CompanyNormalization.NormalizeSlug(slug);
        SetCountry(countryCatalogItemId, countryCode);
        SetDefaultLocale(defaultLocale);
        Status = status;
        CreatedByUserPublicId = createdByUserPublicId;
        SetCompanyType(companyTypeCatalogItemId);
        IsBillable = isBillable;
        BillableSinceUtc = billableSinceUtc;
    }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public string CountryCode { get; private set; } = string.Empty;

    public long CountryCatalogItemId { get; private set; }

    public string DefaultLocale { get; private set; } = "en-US";

    public CompanyStatus Status { get; private set; }

    public Guid CreatedByUserPublicId { get; private set; }

    public long? CompanyTypeCatalogItemId { get; private set; }

    public bool IsBillable { get; private set; }

    public DateTime? BillableSinceUtc { get; private set; }

    public static Company Create(
        string name,
        string slug,
        Guid createdByUserPublicId,
        string countryCode,
        long countryCatalogItemId,
        long? companyTypeCatalogItemId = null,
        string? defaultLocale = null) =>
        new(
            Guid.NewGuid(),
            name,
            slug,
            countryCode,
            countryCatalogItemId,
            defaultLocale ?? CompanyNormalization.DefaultLocaleForCountryCode(countryCode),
            CompanyStatus.Active,
            createdByUserPublicId,
            companyTypeCatalogItemId,
            isBillable: false,
            billableSinceUtc: null);

    private void SetCountry(long countryCatalogItemId, string countryCode)
    {
        if (countryCatalogItemId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(countryCatalogItemId), "Country catalog id cannot be zero.");
        }

        CountryCatalogItemId = countryCatalogItemId;
        CountryCode = CompanyNormalization.NormalizeCountryCode(countryCode);
    }

    private void SetDefaultLocale(string defaultLocale)
    {
        DefaultLocale = CompanyNormalization.NormalizeLocale(defaultLocale);
    }

    public void Rename(string name)
    {
        Name = CompanyNormalization.Clean(name, nameof(name));
    }

    public void SetCompanyType(long? companyTypeCatalogItemId)
    {
        if (companyTypeCatalogItemId.HasValue && companyTypeCatalogItemId.Value == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyTypeCatalogItemId), "Company type catalog id cannot be zero.");
        }

        CompanyTypeCatalogItemId = companyTypeCatalogItemId;
    }

    public void Archive()
    {
        if (Status == CompanyStatus.Archived)
        {
            throw new InvalidOperationException("Company is already archived.");
        }

        Status = CompanyStatus.Archived;
    }

    public void Reactivate()
    {
        if (Status == CompanyStatus.Active)
        {
            throw new InvalidOperationException("Company is already active.");
        }

        Status = CompanyStatus.Active;
    }

    public void MarkBillable(DateTime effectiveFromUtc)
    {
        if (!IsBillable)
        {
            BillableSinceUtc = effectiveFromUtc;
        }

        IsBillable = true;
    }

    public void ClearBillable()
    {
        IsBillable = false;
        BillableSinceUtc = null;
    }
}
