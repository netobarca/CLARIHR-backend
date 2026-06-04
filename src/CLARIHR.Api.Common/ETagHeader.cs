namespace CLARIHR.Api.Common;

public static class ETagHeader
{
    public const string HeaderName = "ETag";

    public static string Format(Guid token) => $"\"{token:D}\"";

    public static string Format(string opaqueTag) => $"\"{opaqueTag}\"";

    // §S4: an aggregate/list ETag is hashed over an order-dependent enumeration,
    // so a non-deterministic ORDER BY on equal sort keys can yield a different
    // hash for two logically identical pages. That is a WEAK validator and must
    // be advertised as such (`W/`) — otherwise a caller treats it as strong and
    // a spurious 200 (instead of 304) looks like a correctness bug. Strong
    // ETags remain reserved for the single-entity ConcurrencyToken path.
    public static string FormatWeak(string opaqueTag) => $"W/\"{opaqueTag}\"";

    public static bool Matches(string? ifNoneMatch, string currentETag)
    {
        if (string.IsNullOrWhiteSpace(ifNoneMatch))
        {
            return false;
        }

        foreach (var candidate in ifNoneMatch.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (candidate == "*")
            {
                return true;
            }

            if (Normalize(candidate) == Normalize(currentETag))
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string value)
    {
        var normalized = value.Trim();
        if (normalized.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[2..].TrimStart();
        }

        return normalized;
    }
}
