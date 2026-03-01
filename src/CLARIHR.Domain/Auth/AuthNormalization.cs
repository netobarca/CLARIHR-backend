namespace CLARIHR.Domain.Auth;

internal static class AuthNormalization
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

    public static string NormalizeEmail(string email) =>
        Clean(email, nameof(email)).ToLowerInvariant();
}
