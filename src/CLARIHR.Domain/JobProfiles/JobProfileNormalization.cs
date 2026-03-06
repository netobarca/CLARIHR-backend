namespace CLARIHR.Domain.JobProfiles;

internal static class JobProfileNormalization
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

    public static string NormalizeCode(string value) =>
        Clean(value, nameof(value)).ToUpperInvariant();

    public static string NormalizeName(string value) =>
        Clean(value, nameof(value)).ToUpperInvariant();
}
