namespace CLARIHR.Api.Common;

internal static class ETagHeader
{
    public const string HeaderName = "ETag";

    public static string Format(Guid token) => $"\"{token:D}\"";

    public static string Format(string opaqueTag) => $"\"{opaqueTag}\"";

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
