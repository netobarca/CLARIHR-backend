using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Preferences;

public sealed class UserSocialLink : Entity
{
    internal UserSocialLink()
    {
    }

    internal UserSocialLink(string providerCode, string url)
    {
        ProviderCode = NormalizeProviderCode(providerCode);
        Url = NormalizeUrl(url);
    }

    public long UserPreferenceId { get; private set; }

    public string ProviderCode { get; private set; } = string.Empty;

    public string Url { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    internal void AssignSortOrder(int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order cannot be negative.");
        }

        SortOrder = sortOrder;
    }

    private static string NormalizeProviderCode(string providerCode)
    {
        var normalized = PreferenceNormalization.NormalizeRequired(providerCode, nameof(providerCode)).ToUpperInvariant();
        if (normalized.Length > 50)
        {
            throw new ArgumentException("Provider code is too long.", nameof(providerCode));
        }

        if (!normalized.All(static character => char.IsLetterOrDigit(character) || character is '_' or '-' or '.'))
        {
            throw new ArgumentException("Provider code format is invalid.", nameof(providerCode));
        }

        return normalized;
    }

    private static string NormalizeUrl(string url)
    {
        var normalized = PreferenceNormalization.NormalizeRequired(url, nameof(url));
        if (normalized.Length > 500)
        {
            throw new ArgumentException("Url is too long.", nameof(url));
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var parsedUri) ||
            !string.Equals(parsedUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Url must be an absolute https URL.", nameof(url));
        }

        return parsedUri.AbsoluteUri;
    }
}
