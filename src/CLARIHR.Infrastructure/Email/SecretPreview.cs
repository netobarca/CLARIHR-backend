namespace CLARIHR.Infrastructure.Email;

/// <summary>
/// Masks single-use credentials before they reach a log sink. Enough to correlate a log line with a
/// support ticket, never enough to redeem the token.
/// </summary>
internal static class SecretPreview
{
    private const string FullyMasked = "****";

    /// <summary>
    /// Extracts every URL found in a plain-text body and masks the token of each, so the dev
    /// transport can show where a mail would have pointed without leaking a redeemable credential.
    /// </summary>
    public static IReadOnlyList<string> MaskLinksIn(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return [];
        }

        return body
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(static candidate => candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                       candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            .Select(OfUrlToken)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>Masks a bare token: <c>abcd...wxyz</c>.</summary>
    public static string OfToken(string? token) =>
        string.IsNullOrWhiteSpace(token) || token.Length <= 8
            ? FullyMasked
            : $"{token[..4]}...{token[^4..]}";

    /// <summary>
    /// Masks the <c>token</c> query parameter of a link while keeping the destination visible, so a
    /// misconfigured front-end URL is still diagnosable from the logs.
    /// </summary>
    public static string OfUrlToken(string? link)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            return FullyMasked;
        }

        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri))
        {
            return FullyMasked;
        }

        var query = uri.Query.TrimStart('?');
        if (query.Length == 0)
        {
            return link;
        }

        var maskedPairs = query
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(static pair =>
            {
                var separatorIndex = pair.IndexOf('=', StringComparison.Ordinal);
                if (separatorIndex < 0)
                {
                    return pair;
                }

                var key = pair[..separatorIndex];
                return key.Equals("token", StringComparison.OrdinalIgnoreCase)
                    ? $"{key}={OfToken(pair[(separatorIndex + 1)..])}"
                    : pair;
            });

        return $"{uri.GetLeftPart(UriPartial.Path)}?{string.Join('&', maskedPairs)}";
    }
}
