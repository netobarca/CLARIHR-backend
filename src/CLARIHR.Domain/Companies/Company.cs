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
        Status = status;
        CreatedByUserPublicId = createdByUserPublicId;
        SetCompanyType(companyTypeCatalogItemId);
        IsBillable = isBillable;
        BillableSinceUtc = billableSinceUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public string CountryCode { get; private set; } = string.Empty;

    public long CountryCatalogItemId { get; private set; }

    public CompanyStatus Status { get; private set; }

    public Guid CreatedByUserPublicId { get; private set; }

    public long? CompanyTypeCatalogItemId { get; private set; }

    public bool IsBillable { get; private set; }

    public DateTime? BillableSinceUtc { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static Company Create(
        string name,
        string slug,
        Guid createdByUserPublicId,
        string countryCode,
        long countryCatalogItemId,
        long? companyTypeCatalogItemId = null) =>
        new(
            Guid.NewGuid(),
            name,
            slug,
            countryCode,
            countryCatalogItemId,
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

    public void Rename(string name)
    {
        Name = CompanyNormalization.Clean(name, nameof(name));
        ConcurrencyToken = Guid.NewGuid();
    }

    public void SetCompanyType(long? companyTypeCatalogItemId)
    {
        if (companyTypeCatalogItemId.HasValue && companyTypeCatalogItemId.Value == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyTypeCatalogItemId), "Company type catalog id cannot be zero.");
        }

        CompanyTypeCatalogItemId = companyTypeCatalogItemId;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void Archive()
    {
        if (Status == CompanyStatus.Archived)
        {
            throw new InvalidOperationException("Company is already archived.");
        }

        Status = CompanyStatus.Archived;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void Reactivate()
    {
        if (Status == CompanyStatus.Active)
        {
            throw new InvalidOperationException("Company is already active.");
        }

        Status = CompanyStatus.Active;
        ConcurrencyToken = Guid.NewGuid();
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
