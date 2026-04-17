using System.Globalization;
using System.Text.RegularExpressions;
using System.Resources;
using CLARIHR.Application.Abstractions.Localization;

namespace CLARIHR.Infrastructure.Localization;

internal sealed class ResourceBackendMessageLocalizer : IBackendMessageLocalizer
{
    private static readonly Regex NonAlphaNumericRegex = new(
        "[^a-z0-9]+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FormatInvalidRegex = new(
        "^(?<field>.+) format is invalid\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ContainsInvalidCharactersRegex = new(
        "^(?<field>.+) contains invalid characters\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex IsRequiredRegex = new(
        "^(?<field>.+) is required\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex LessOrEqualRegex = new(
        "^(?<left>.+) must be less than or equal to (?<right>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex GreaterOrEqualRegex = new(
        "^(?<left>.+) must be greater than or equal to (?<right>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CannotBeGreaterRegex = new(
        "^(?<left>.+) cannot be greater than (?<right>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly ResourceManager ResourceManager = new(
        "CLARIHR.Infrastructure.Localization.BackendMessages",
        typeof(ResourceBackendMessageLocalizer).Assembly);

    public string Localize(
        string key,
        string fallback,
        IReadOnlyList<object?>? arguments = null)
    {
        var template = ResolveTemplate(key, fallback);

        if (arguments is null || arguments.Count == 0)
        {
            return template;
        }

        try
        {
            return string.Format(CultureInfo.CurrentUICulture, template, arguments.ToArray());
        }
        catch (FormatException)
        {
            return fallback;
        }
    }

    public string LocalizeValidationMessage(string fallback)
    {
        if (string.IsNullOrWhiteSpace(fallback))
        {
            return fallback;
        }

        var culture = CultureInfo.CurrentUICulture;
        var key = BuildValidationMessageKey(fallback);
        var template = ResourceManager.GetString(key, culture)
            ?? TryTranslateFallback(fallback, culture)
            ?? fallback;

        return template;
    }

    private static string ResolveTemplate(string key, string fallback)
    {
        var culture = CultureInfo.CurrentUICulture;
        if (string.IsNullOrWhiteSpace(key))
        {
            return TryTranslateFallback(fallback, culture) ?? fallback;
        }

        return ResourceManager.GetString(key, culture)
            ?? TryTranslateFallback(fallback, culture)
            ?? fallback;
    }

    private static string BuildValidationMessageKey(string fallback)
    {
        var normalized = NonAlphaNumericRegex
            .Replace(fallback.Trim().ToLowerInvariant(), "_")
            .Trim('_');

        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "generic";
        }

        return $"validation.message.{normalized}";
    }

    private static string? TryTranslateFallback(string fallback, CultureInfo culture)
    {
        if (!culture.TwoLetterISOLanguageName.Equals("es", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (fallback.Equals("Unexpected error", StringComparison.Ordinal))
        {
            return "Error inesperado";
        }

        if (fallback.Equals("An unexpected error occurred.", StringComparison.Ordinal))
        {
            return "Ocurrio un error inesperado.";
        }

        if (FormatInvalidRegex.Match(fallback) is { Success: true } formatMatch)
        {
            return $"El formato de {formatMatch.Groups["field"].Value} no es valido.";
        }

        if (ContainsInvalidCharactersRegex.Match(fallback) is { Success: true } invalidCharacterMatch)
        {
            return $"{invalidCharacterMatch.Groups["field"].Value} contiene caracteres invalidos.";
        }

        if (IsRequiredRegex.Match(fallback) is { Success: true } requiredMatch)
        {
            return $"{requiredMatch.Groups["field"].Value} es requerido.";
        }

        if (LessOrEqualRegex.Match(fallback) is { Success: true } lessOrEqualMatch)
        {
            return $"{lessOrEqualMatch.Groups["left"].Value} debe ser menor o igual que {lessOrEqualMatch.Groups["right"].Value}.";
        }

        if (GreaterOrEqualRegex.Match(fallback) is { Success: true } greaterOrEqualMatch)
        {
            return $"{greaterOrEqualMatch.Groups["left"].Value} debe ser mayor o igual que {greaterOrEqualMatch.Groups["right"].Value}.";
        }

        if (CannotBeGreaterRegex.Match(fallback) is { Success: true } cannotBeGreaterMatch)
        {
            return $"{cannotBeGreaterMatch.Groups["left"].Value} no puede ser mayor que {cannotBeGreaterMatch.Groups["right"].Value}.";
        }

        return null;
    }
}
