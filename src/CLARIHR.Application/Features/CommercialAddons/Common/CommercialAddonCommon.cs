using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.CommercialAddons.Common;

public static partial class CommercialAddonValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static bool IsValidAddonCode(string code) =>
        AddonCodeRegex().IsMatch(code.Trim());

    public static bool HasSupportedScale(decimal value) =>
        decimal.Round(value, 2) == value;

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_-]{0,39}$", RegexOptions.CultureInvariant)]
    private static partial Regex AddonCodeRegex();
}

public static class CommercialAddonErrors
{
    public static readonly Error Forbidden = new(
        "COMMERCIAL_ADDON_FORBIDDEN",
        "You do not have permission to access commercial addon administration.",
        ErrorType.Forbidden);

    public static readonly Error NotFound = new(
        "COMMERCIAL_ADDON_NOT_FOUND",
        "The requested commercial addon could not be found.",
        ErrorType.NotFound);

    public static readonly Error CodeConflict = new(
        "COMMERCIAL_ADDON_CODE_CONFLICT",
        "Another commercial addon already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static readonly Error AlreadyActive = new(
        "COMMERCIAL_ADDON_ALREADY_ACTIVE",
        "The commercial addon is already active.",
        ErrorType.Conflict);

    public static readonly Error AlreadyInactive = new(
        "COMMERCIAL_ADDON_ALREADY_INACTIVE",
        "The commercial addon is already inactive.",
        ErrorType.Conflict);
}
