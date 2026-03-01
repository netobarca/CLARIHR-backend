using System.Text.RegularExpressions;

namespace CLARIHR.Application.Features.Auth.Common;

internal static partial class AuthValidationRules
{
    public static bool BeValidPersonName(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        PersonNameRegex().IsMatch(value.Trim());

    public static bool BeValidCountry(string? value) =>
        string.IsNullOrWhiteSpace(value) || CountryRegex().IsMatch(value.Trim());

    public static bool BeValidSource(string? value) =>
        string.IsNullOrWhiteSpace(value) || SourceRegex().IsMatch(value.Trim());

    [GeneratedRegex(@"^[\p{L}\p{M}][\p{L}\p{M}'\-\s]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex PersonNameRegex();

    [GeneratedRegex(@"^[\p{L}\p{M}][\p{L}\p{M}'\-\s]{1,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex CountryRegex();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9 ._:/-]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex SourceRegex();
}
