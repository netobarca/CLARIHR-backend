using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Afps;

/// <summary>
/// Country-scoped enriched master catalog of pension fund administrators — AFP (RF-007, D-05).
/// Mirrors <see cref="CLARIHR.Domain.Banks.BankCatalogItem"/>: identity/contact columns beyond the
/// generic code/name pair, delivered by seed (DP-03) and read via the dedicated
/// <c>GET /api/v1/afps</c>. The employee affiliation lives on the person as
/// <c>PersonnelFile.AfpCode</c> (DP-04/RT-05); the country-level calculation parameters live on the
/// AFP compensation concept type (DP-05).
/// </summary>
public sealed class AfpCatalogItem : CountryScopedCatalogItem
{
    private AfpCatalogItem()
    {
    }

    private AfpCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        string? abbreviation,
        string? address,
        string? phone,
        string? fax,
        string? contactName,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
        Abbreviation = CleanOptional(abbreviation, nameof(abbreviation), 20)?.ToUpperInvariant();
        Address = CleanOptional(address, nameof(address), 500);
        Phone = CleanOptional(phone, nameof(phone), 40);
        Fax = CleanOptional(fax, nameof(fax), 40);
        ContactName = CleanOptional(contactName, nameof(contactName), 150);
    }

    public string? Abbreviation { get; private set; }

    public string? Address { get; private set; }

    public string? Phone { get; private set; }

    public string? Fax { get; private set; }

    public string? ContactName { get; private set; }

    public static AfpCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        string? abbreviation,
        string? address,
        string? phone,
        string? fax,
        string? contactName,
        bool isActive,
        int sortOrder) =>
        new(
            Guid.NewGuid(),
            countryCatalogItemId,
            countryCode,
            code,
            name,
            abbreviation,
            address,
            phone,
            fax,
            contactName,
            isActive,
            sortOrder);

    private static string? CleanOptional(string? value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Clean(value, parameterName, maxLength);
    }
}
