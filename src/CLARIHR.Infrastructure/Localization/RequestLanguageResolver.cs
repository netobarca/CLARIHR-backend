using System.Globalization;
using System.Text.RegularExpressions;

namespace CLARIHR.Infrastructure.Localization;

public static class RequestLanguageResolver
{
    private const string FallbackLanguage = "en";
    private static readonly Regex LanguageRegex = new(
        "^[a-z]{2,3}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public const string LanguageClaimType = "language";

    public static string ResolveLanguage(string? preferredLanguage, string? acceptLanguageHeader)
    {
        if (TryNormalizeLanguage(preferredLanguage, out var resolvedFromPreference))
        {
            return resolvedFromPreference;
        }

        if (TryResolveFromAcceptLanguageHeader(acceptLanguageHeader, out var resolvedFromHeader))
        {
            return resolvedFromHeader;
        }

        return FallbackLanguage;
    }

    public static CultureInfo ResolveCulture(string? preferredLanguage, string? acceptLanguageHeader)
    {
        var language = ResolveLanguage(preferredLanguage, acceptLanguageHeader);
        try
        {
            return CultureInfo.GetCultureInfo(language);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo(FallbackLanguage);
        }
    }

    public static bool TryNormalizeLanguage(string? language, out string normalized)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            normalized = string.Empty;
            return false;
        }

        var candidate = language.Trim();
        var separatorIndex = candidate.IndexOf('-');
        if (separatorIndex > 0)
        {
            candidate = candidate[..separatorIndex];
        }

        candidate = candidate.ToLowerInvariant();
        if (!LanguageRegex.IsMatch(candidate))
        {
            normalized = string.Empty;
            return false;
        }

        normalized = candidate;
        return true;
    }

    private static bool TryResolveFromAcceptLanguageHeader(string? acceptLanguageHeader, out string language)
    {
        if (string.IsNullOrWhiteSpace(acceptLanguageHeader))
        {
            language = string.Empty;
            return false;
        }

        var segments = acceptLanguageHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var segment in segments)
        {
            var token = segment;
            var qualityIndex = token.IndexOf(';');
            if (qualityIndex >= 0)
            {
                token = token[..qualityIndex];
            }

            if (TryNormalizeLanguage(token, out language))
            {
                return true;
            }
        }

        language = string.Empty;
        return false;
    }
}
