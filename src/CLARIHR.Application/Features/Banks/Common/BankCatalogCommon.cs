using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.Banks.Common;

public static partial class BankCatalogValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static bool IsValidCode(string code) =>
        BankCodeRegex().IsMatch(code.Trim());

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_-]{0,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex BankCodeRegex();
}

public static class BankCatalogErrors
{
    public static readonly Error NotFound = new(
        "BANK_CATALOG_NOT_FOUND",
        "The requested bank catalog item could not be found.",
        ErrorType.NotFound);

    public static readonly Error CodeConflict = new(
        "BANK_CATALOG_CODE_CONFLICT",
        "Another bank catalog item already uses the requested code for that country.",
        ErrorType.Conflict);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static readonly Error CountryChangeForbidden = new(
        "BANK_CATALOG_COUNTRY_CHANGE_FORBIDDEN",
        "Changing the country of an existing bank catalog item is not supported.",
        ErrorType.Validation);

    public static readonly Error AlreadyActive = new(
        "BANK_CATALOG_ALREADY_ACTIVE",
        "The bank catalog item is already active.",
        ErrorType.Conflict);

    public static readonly Error AlreadyInactive = new(
        "BANK_CATALOG_ALREADY_INACTIVE",
        "The bank catalog item is already inactive.",
        ErrorType.Conflict);

    public static readonly Error CompanyCountryNotFound = new(
        "BANK_CATALOG_COMPANY_COUNTRY_NOT_FOUND",
        "The company country could not be resolved for bank catalog lookup.",
        ErrorType.NotFound);

    public static Error CountryNotFound(string countryCode) =>
        new(
            "BANK_CATALOG_COUNTRY_NOT_FOUND",
            $"Country '{countryCode.Trim().ToUpperInvariant()}' is not active.",
            ErrorType.NotFound,
            MessageArguments: [countryCode.Trim().ToUpperInvariant()]);
}
