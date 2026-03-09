namespace CLARIHR.Domain.PersonnelFiles;

public static class PersonnelFileNormalization
{
    public static string Clean(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} is required.", paramName);
        }

        return value.Trim();
    }

    public static string? CleanOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    public static string NormalizeName(string value) =>
        Clean(value, nameof(value)).ToUpperInvariant();

    public static string NormalizeCode(string value) =>
        Clean(value, nameof(value)).ToUpperInvariant();

    public static DateTime NormalizeDate(DateTime value) =>
        DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);

    public static DateTime? NormalizeDate(DateTime? value) =>
        value.HasValue ? NormalizeDate(value.Value) : null;
}
