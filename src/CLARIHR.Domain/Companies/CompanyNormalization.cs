using System.Text;
using System.Text.RegularExpressions;

namespace CLARIHR.Domain.Companies;

internal static class CompanyNormalization
{
    private static readonly Regex CountryCodeRegex = new("^[A-Za-z]{2,3}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string Clean(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty.", parameterName);
        }

        return value.Trim();
    }

    public static string NormalizePlanCode(string planCode) =>
        Clean(planCode, nameof(planCode)).ToUpperInvariant();

    public static string NormalizeLimitCode(string limitCode) =>
        Clean(limitCode, nameof(limitCode)).ToUpperInvariant();

    public static string NormalizeModuleKey(string moduleKey) =>
        Clean(moduleKey, nameof(moduleKey)).ToUpperInvariant();

    public static string NormalizeCurrencyCode(string currencyCode)
    {
        var normalized = Clean(currencyCode, nameof(currencyCode)).ToUpperInvariant();
        if (normalized.Length != 3)
        {
            throw new ArgumentException("Currency code must contain exactly 3 characters.", nameof(currencyCode));
        }

        return normalized;
    }

    public static string NormalizeName(string name) =>
        Clean(name, nameof(name)).ToUpperInvariant();

    public static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string NormalizeCountryCode(string countryCode)
    {
        var normalized = Clean(countryCode, nameof(countryCode)).ToUpperInvariant();
        if (!CountryCodeRegex.IsMatch(normalized))
        {
            throw new ArgumentException("Country code format is invalid.", nameof(countryCode));
        }

        return normalized;
    }

    public static string NormalizeSlug(string slug)
    {
        var cleaned = Clean(slug, nameof(slug)).ToLowerInvariant();
        if (cleaned.Length > 120)
        {
            throw new ArgumentException("Slug is too long.", nameof(slug));
        }

        return cleaned;
    }

    public static string CreateSlug(string value)
    {
        var cleaned = Clean(value, nameof(value)).ToLowerInvariant();
        var builder = new StringBuilder(cleaned.Length);
        var previousWasSeparator = false;

        foreach (var character in cleaned)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
                continue;
            }

            if (previousWasSeparator)
            {
                continue;
            }

            if (builder.Length == 0)
            {
                continue;
            }

            builder.Append('-');
            previousWasSeparator = true;
        }

        var slug = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "company" : slug;
    }
}
