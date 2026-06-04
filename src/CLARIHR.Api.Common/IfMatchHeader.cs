namespace CLARIHR.Api.Common;

public static class IfMatchHeader
{
    public const string HeaderName = "If-Match";

    public const string MissingDetail =
        "The 'If-Match' header is required and must contain the current resource concurrency token.";

    public static bool TryParseConcurrencyToken(string? headerValue, out Guid token)
    {
        token = Guid.Empty;
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return false;
        }

        var trimmed = headerValue.Trim();
        if (trimmed.Length >= 2 && trimmed.StartsWith('"') && trimmed.EndsWith('"'))
        {
            trimmed = trimmed[1..^1];
        }

        return Guid.TryParse(trimmed, out token);
    }
}
