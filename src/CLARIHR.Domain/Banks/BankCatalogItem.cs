using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Banks;

public sealed class BankCatalogItem : CountryScopedCatalogItem
{
    private BankCatalogItem()
    {
    }

    private BankCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        string? alias,
        string? swiftCode,
        string? routingCode,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
        SetAlias(alias);
        SetSwiftCode(swiftCode);
        SetRoutingCode(routingCode);
    }

    public string? Alias { get; private set; }

    public string? NormalizedAlias { get; private set; }

    public string? SwiftCode { get; private set; }

    public string? NormalizedSwiftCode { get; private set; }

    public string? RoutingCode { get; private set; }

    public string? NormalizedRoutingCode { get; private set; }

    public static BankCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        string? alias,
        string? swiftCode,
        string? routingCode,
        bool isActive,
        int sortOrder) =>
        new(
            Guid.NewGuid(),
            countryCatalogItemId,
            countryCode,
            code,
            name,
            alias,
            swiftCode,
            routingCode,
            isActive,
            sortOrder);

    public void Update(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        string? alias,
        string? swiftCode,
        string? routingCode,
        int sortOrder)
    {
        base.Update(countryCatalogItemId, countryCode, code, name, sortOrder);
        SetAlias(alias);
        SetSwiftCode(swiftCode);
        SetRoutingCode(routingCode);
    }

    private void SetAlias(string? alias)
    {
        Alias = CleanOptional(alias, nameof(alias), 120);
        NormalizedAlias = Alias?.ToUpperInvariant();
    }

    private void SetSwiftCode(string? swiftCode)
    {
        SwiftCode = CleanOptional(swiftCode, nameof(swiftCode), 40)?.ToUpperInvariant();
        NormalizedSwiftCode = SwiftCode;
    }

    private void SetRoutingCode(string? routingCode)
    {
        RoutingCode = CleanOptional(routingCode, nameof(routingCode), 40)?.ToUpperInvariant();
        NormalizedRoutingCode = RoutingCode;
    }

    private static string? CleanOptional(string? value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Clean(value, parameterName, maxLength);
    }
}
