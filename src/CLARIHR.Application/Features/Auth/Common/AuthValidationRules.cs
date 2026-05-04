using System.Text.RegularExpressions;

namespace CLARIHR.Application.Features.Auth.Common;

internal static partial class AuthValidationRules
{
    public const int PasswordMinimumLength = 12;
    public const int PasswordMaximumLength = 100;

    public static bool BeValidPersonName(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        PersonNameRegex().IsMatch(value.Trim());

    public static bool BeValidCountry(string? value) =>
        string.IsNullOrWhiteSpace(value) || CountryRegex().IsMatch(value.Trim());

    public static bool BeValidSource(string? value) =>
        string.IsNullOrWhiteSpace(value) || SourceRegex().IsMatch(value.Trim());

    public static IEnumerable<string> GetPasswordPolicyViolations(
        string? password,
        string? firstName,
        string? lastName,
        string? email)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            yield break;
        }

        if (password.Length < PasswordMinimumLength)
        {
            yield return $"Password must be at least {PasswordMinimumLength} characters long.";
        }

        if (password.Length > PasswordMaximumLength)
        {
            yield return $"Password must be {PasswordMaximumLength} characters or fewer.";
        }

        if (!password.Any(char.IsUpper))
        {
            yield return "Password must contain at least one uppercase letter.";
        }

        if (!password.Any(char.IsLower))
        {
            yield return "Password must contain at least one lowercase letter.";
        }

        if (!password.Any(char.IsDigit))
        {
            yield return "Password must contain at least one number.";
        }

        if (!password.Any(static character => !char.IsLetterOrDigit(character)))
        {
            yield return "Password must contain at least one special character.";
        }



        if (ContainsPersonalInfo(password, firstName, lastName, email))
        {
            yield return "Password cannot contain your name or email.";
        }
    }

    private static bool ContainsPersonalInfo(
        string password,
        string? firstName,
        string? lastName,
        string? email)
    {
        var normalizedPassword = NormalizeComparableToken(password);
        if (string.IsNullOrEmpty(normalizedPassword))
        {
            return false;
        }

        foreach (var token in GetComparableTokens(firstName, lastName, email))
        {
            if (normalizedPassword.Contains(token, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> GetComparableTokens(string? firstName, string? lastName, string? email)
    {
        var candidates = new List<string?>(3)
        {
            firstName,
            lastName
        };

        if (!string.IsNullOrWhiteSpace(email))
        {
            var atIndex = email.IndexOf('@');
            candidates.Add(atIndex > 0 ? email[..atIndex] : email);
        }

        return candidates
            .Select(NormalizeComparableToken)
            .Where(static token => token.Length >= 3)
            .Distinct(StringComparer.Ordinal);
    }

    private static string NormalizeComparableToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    [GeneratedRegex(@"^[\p{L}\p{M}][\p{L}\p{M}'\-\s]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex PersonNameRegex();

    [GeneratedRegex(@"^[\p{L}\p{M}][\p{L}\p{M}'\-\s]{1,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex CountryRegex();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9 ._:/-]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex SourceRegex();
}
