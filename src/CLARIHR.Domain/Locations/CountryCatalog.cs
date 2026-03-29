namespace CLARIHR.Domain.Locations;

public static class CountryCatalog
{
    public static IReadOnlyList<CountryCatalogDefinition> Items => CountryCatalogData.Items;
}

public sealed record CountryCatalogDefinition(
    long Id,
    string Code,
    string Name,
    int SortOrder);
