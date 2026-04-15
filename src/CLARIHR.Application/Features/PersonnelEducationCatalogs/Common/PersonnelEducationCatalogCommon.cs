using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Features.PersonnelEducationCatalogs.Common;

public enum PersonnelEducationCatalogType
{
    EducationStatus = 1,
    StudyType = 2,
    Career = 3,
    Shift = 4,
    Modality = 5
}

public static partial class PersonnelEducationCatalogValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static bool IsValidCode(string value) =>
        CodeRegex().IsMatch(value.Trim());

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_./-]{0,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeRegex();
}

public static class PersonnelEducationCatalogErrors
{
    public static readonly Error Forbidden = new(
        "PERSONNEL_EDUCATION_CATALOGS_FORBIDDEN",
        "You do not have permission to access personnel education catalogs.",
        ErrorType.Forbidden);

    public static readonly Error CatalogItemNotFound = new(
        "PERSONNEL_EDUCATION_CATALOG_ITEM_NOT_FOUND",
        "The requested personnel education catalog item could not be found.",
        ErrorType.NotFound);

    public static readonly Error CatalogCodeConflict = new(
        "PERSONNEL_EDUCATION_CATALOG_CODE_CONFLICT",
        "Another personnel education catalog item already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static readonly Error CatalogItemInUse = new(
        "PERSONNEL_EDUCATION_CATALOG_ITEM_IN_USE",
        "The personnel education catalog item is in use and cannot be inactivated.",
        ErrorType.Conflict);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch("PERSONNEL_EDUCATION_CATALOGS", action);
}
