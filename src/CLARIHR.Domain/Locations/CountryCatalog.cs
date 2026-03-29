using System.Globalization;
using System.Text;

namespace CLARIHR.Domain.Locations;

public static class CountryCatalog
{
    private static readonly Lazy<IReadOnlyList<CountryCatalogDefinition>> Definitions = new(CreateDefinitions);

    public static IReadOnlyList<CountryCatalogDefinition> Items => Definitions.Value;

    private static IReadOnlyList<CountryCatalogDefinition> CreateDefinitions()
    {
        var items = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .Select(static culture => TryCreateRegion(culture.Name))
            .Where(static region => region is not null)
            .Select(static region => region!)
            .Where(static region => IsValidCode(region.TwoLetterISORegionName))
            .Select(static region => new
            {
                Code = region.TwoLetterISORegionName.Trim().ToUpperInvariant(),
                Name = NormalizeDisplayName(region.EnglishName)
            })
            .GroupBy(static item => item.Code)
            .Select(static group => group
                .OrderBy(static item => item.Name, StringComparer.Ordinal)
                .ThenBy(static item => item.Code, StringComparer.Ordinal)
                .First())
            .OrderBy(static item => item.Name, StringComparer.Ordinal)
            .ThenBy(static item => item.Code, StringComparer.Ordinal)
            .Select(static (item, index) => new CountryCatalogDefinition(
                -7000L - index,
                item.Code,
                item.Name,
                index + 1))
            .ToArray();

        return items;
    }

    private static RegionInfo? TryCreateRegion(string cultureName)
    {
        try
        {
            return new RegionInfo(cultureName);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsValidCode(string code) =>
        code.Length == 2 && code.All(static character => char.IsLetter(character));

    private static string NormalizeDisplayName(string value)
    {
        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            switch (character)
            {
                case '\u2018':
                case '\u2019':
                    builder.Append('\'');
                    continue;
                case '\u2013':
                case '\u2014':
                    builder.Append('-');
                    continue;
                case '\u00A0':
                    builder.Append(' ');
                    continue;
            }

            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).Trim();
    }
}

public sealed record CountryCatalogDefinition(
    long Id,
    string Code,
    string Name,
    int SortOrder);
