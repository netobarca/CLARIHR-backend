namespace CLARIHR.Domain.Payroll;

/// <summary>
/// Shared normalization helpers for the payroll configuration masters (payroll definitions — REQ-012;
/// work schedules join in PR-3). Mirrors <c>OvertimeNormalization</c> / <c>CostCenterNormalization</c>:
/// codes/names are trimmed and upper-cased for the filtered unique key and the case-insensitive search.
/// </summary>
internal static class PayrollNormalization
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
