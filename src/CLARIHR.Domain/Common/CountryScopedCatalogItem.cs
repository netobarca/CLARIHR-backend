using CLARIHR.Domain.Locations;

namespace CLARIHR.Domain.Common;

public abstract class CountryScopedCatalogItem : AuditableEntity
{
    protected CountryScopedCatalogItem()
    {
    }

    protected CountryScopedCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order cannot be negative.");
        }

        PublicId = publicId;
        SetCountry(countryCatalogItemId, countryCode);
        SetCode(code);
        SetName(name);
        IsActive = isActive;
        SortOrder = sortOrder;
        ConcurrencyToken = Guid.NewGuid();
    }

    public long CountryCatalogItemId { get; private set; }

    public string CountryCode { get; private set; } = string.Empty;

    public CountryCatalogItem? CountryCatalogItem { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public int SortOrder { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void Update(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order cannot be negative.");
        }

        SetCountry(countryCatalogItemId, countryCode);
        SetCode(code);
        SetName(name);
        SortOrder = sortOrder;
        RefreshConcurrencyToken();
    }

    public void Activate()
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        RefreshConcurrencyToken();
    }

    public void Inactivate()
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        RefreshConcurrencyToken();
    }

    protected void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();

    private void SetCountry(long countryCatalogItemId, string countryCode)
    {
        if (countryCatalogItemId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(countryCatalogItemId), "Country catalog item id cannot be zero.");
        }

        CountryCatalogItemId = countryCatalogItemId;
        CountryCode = Clean(countryCode, nameof(countryCode), 2).ToUpperInvariant();
    }

    private void SetCode(string code)
    {
        Code = Clean(code, nameof(code), 80).ToUpperInvariant();
        NormalizedCode = Code;
    }

    private void SetName(string name)
    {
        Name = Clean(name, nameof(name), 200);
        NormalizedName = Name.ToUpperInvariant();
    }

    protected static string Clean(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty.", parameterName);
        }

        var cleaned = value.Trim();
        if (cleaned.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"Value cannot exceed {maxLength} characters.");
        }

        return cleaned;
    }
}
