using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.DocumentTypeCatalogs.Common;

public static partial class DocumentTypeCatalogValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static bool IsValidCode(string value) =>
        CodeRegex().IsMatch(value.Trim());

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_./-]{0,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeRegex();
}

public static class DocumentTypeCatalogErrors
{
    public static readonly Error NotFound = new(
        "DOCUMENT_TYPE_CATALOG_ITEM_NOT_FOUND",
        "The requested document type catalog item could not be found.",
        ErrorType.NotFound);

    public static readonly Error CodeConflict = new(
        "DOCUMENT_TYPE_CATALOG_CODE_CONFLICT",
        "Another document type catalog item already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static readonly Error CatalogItemInUse = new(
        "DOCUMENT_TYPE_CATALOG_ITEM_IN_USE",
        "The document type catalog item is in use and cannot be inactivated.",
        ErrorType.Conflict);
}
