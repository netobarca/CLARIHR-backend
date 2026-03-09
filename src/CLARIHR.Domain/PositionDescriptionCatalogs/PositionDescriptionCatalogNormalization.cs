using System.Globalization;
using System.Text;

namespace CLARIHR.Domain.PositionDescriptionCatalogs;

public static class PositionDescriptionCatalogNormalization
{
    public static string Clean(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        return CollapseWhitespace(value);
    }

    public static string? CleanOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return CollapseWhitespace(value);
    }

    public static string NormalizeCode(string value) => Clean(value, nameof(value)).ToUpperInvariant();

    public static string NormalizeName(string value) => NormalizeFreeText(value);

    public static string NormalizeDescription(string value) => NormalizeFreeText(value);

    private static string NormalizeFreeText(string value)
    {
        var cleaned = Clean(value, nameof(value));
        var normalized = cleaned.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }

    private static string CollapseWhitespace(string value)
    {
        var parts = value
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Where(part => part.Length > 0);

        return string.Join(' ', parts);
    }
}
