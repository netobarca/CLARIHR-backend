using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.EducationCatalogs.Common;

public enum EducationCatalogType
{
    EducationStatus = 1,
    StudyType = 2,
    Career = 3,
    Shift = 4,
    Modality = 5
}

public static partial class EducationCatalogValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static bool IsValidCode(string value) =>
        CodeRegex().IsMatch(value.Trim());

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_./-]{0,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeRegex();
}

public static class EducationCatalogErrors
{
    public static readonly Error NotFound = new(
        "EDUCATION_CATALOG_ITEM_NOT_FOUND",
        "The requested education catalog item could not be found.",
        ErrorType.NotFound);

    public static readonly Error CodeConflict = new(
        "EDUCATION_CATALOG_CODE_CONFLICT",
        "Another education catalog item already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static readonly Error CatalogItemInUse = new(
        "EDUCATION_CATALOG_ITEM_IN_USE",
        "The education catalog item is in use and cannot be inactivated.",
        ErrorType.Conflict);
}
