using System.Text.RegularExpressions;
using CLARIHR.Domain.Companies;

namespace CLARIHR.Domain.Preferences;

internal static class PreferenceNormalization
{
    private static readonly Regex LanguageRegex = new(
        "^[a-z]{2,3}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string NormalizeLanguage(string language)
    {
        var normalized = NormalizeRequired(language, nameof(language)).ToLowerInvariant();
        if (!LanguageRegex.IsMatch(normalized))
        {
            throw new ArgumentException("Language format is invalid.", nameof(language));
        }

        return normalized;
    }

    public static string NormalizeCurrencyCode(string currencyCode) =>
        CompanyNormalization.NormalizeCurrencyCode(currencyCode);

    public static string NormalizeTimeZone(string timeZone)
    {
        var normalized = NormalizeRequired(timeZone, nameof(timeZone));
        if (normalized.Length > 100)
        {
            throw new ArgumentException("Time zone is too long.", nameof(timeZone));
        }

        return normalized;
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty.", parameterName);
        }

        return value.Trim();
    }
}
