using System.Text.RegularExpressions;

namespace CLARIHR.Domain.LegalRepresentatives;

internal static partial class LegalRepresentativeNormalization
{
    public static string Clean(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty.", parameterName);
        }

        return value.Trim();
    }

    public static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    public static string NormalizeName(string value) =>
        Clean(value, nameof(value)).ToUpperInvariant();

    /// <summary>
    /// Strips every separator before upper-casing, so the uniqueness key is the document itself and not the
    /// way somebody happened to punctuate it: <c>01234567-8</c> and <c>012345678</c> are one person, not two.
    /// This value backs the unique index on (tenant, document type, normalized number).
    /// </summary>
    public static string NormalizeDocumentNumber(string value) =>
        DocumentSeparatorRegex().Replace(Clean(value, nameof(value)), string.Empty).ToUpperInvariant();

    [GeneratedRegex(@"[^A-Za-z0-9]", RegexOptions.CultureInvariant)]
    private static partial Regex DocumentSeparatorRegex();
}
