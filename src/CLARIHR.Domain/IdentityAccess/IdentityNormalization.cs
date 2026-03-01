namespace CLARIHR.Domain.IdentityAccess;

internal static class IdentityNormalization
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

    public static string Normalize(string value) =>
        Clean(value, nameof(value)).ToUpperInvariant();
}
