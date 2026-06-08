using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Features.OrgStructureCatalogs.Common;

public static partial class OrgStructureCatalogValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MinSearchLength = 2;

    // OSC-005: single source of truth for the (TenantId, NormalizedCode) unique index names, shared by
    // the EF configuration and the OrgStructureCatalogConstraintViolations guard that maps a concurrent
    // duplicate-code race to a clean 409 instead of a 500 (mirrors OrgUnits OU-004).
    public const string UnitTypeCodeUniqueConstraintName = "uq_org_unit_type_catalog_items__tenant_code";
    public const string FunctionalAreaCodeUniqueConstraintName = "uq_functional_area_catalog_items__tenant_code";

    public static bool IsValidCode(string code) =>
        CodeRegex().IsMatch(code.Trim());

    // OSC-004 (§12.8 / OrgUnits OU-002): free-text catalog search must impose a minimum length (after
    // Trim) so the non-sargable Normalized*.Contains(q) LIKE '%x%' scan cannot be triggered by a 1-char
    // query. Empty/whitespace = "no filter" (valid). Mirrors OrgUnitValidationRules.
    public static bool IsValidSearchLength(string? search) =>
        string.IsNullOrWhiteSpace(search) || search.Trim().Length >= MinSearchLength;

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_-]{0,49}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeRegex();
}

public static class OrgStructureCatalogPermissionCodes
{
    public const string Read = "OrgStructureCatalogs.Read";
    public const string Admin = "OrgStructureCatalogs.Admin";
    public const string OrgUnitsRead = "OrgUnits.Read";
    public const string OrgUnitsAdmin = "OrgUnits.Admin";
    public const string ManageAdministration = "iam.administration.manage";
    public const string ResourceKey = "ORG_STRUCTURE_CATALOGS";
}

public static class OrgStructureCatalogErrors
{
    public static readonly Error Forbidden = new(
        "ORG_STRUCTURE_CATALOG_FORBIDDEN",
        "You do not have permission to access organization structure catalogs.",
        ErrorType.Forbidden);

    public static readonly Error CatalogNotFound = new(
        "ORG_STRUCTURE_CATALOG_NOT_FOUND",
        "The requested organization structure catalog item could not be found.",
        ErrorType.NotFound);

    public static readonly Error CatalogCodeConflict = new(
        "ORG_STRUCTURE_CATALOG_CODE_CONFLICT",
        "Another catalog item already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error CatalogInUse = new(
        "ORG_STRUCTURE_CATALOG_IN_USE",
        "The catalog item cannot be inactivated while it is in use.",
        ErrorType.Conflict);

    public static readonly Error ResourceInUse = new(
        "RESOURCE_IN_USE",
        "The resource cannot be inactivated while it is in use.",
        ErrorType.Conflict);

    public static readonly Error OrgUnitTypeNotFound = new(
        "ORG_UNIT_TYPE_NOT_FOUND",
        "The selected org unit type could not be found or is inactive.",
        ErrorType.NotFound);

    public static readonly Error FunctionalAreaNotFound = new(
        "FUNCTIONAL_AREA_NOT_FOUND",
        "The selected functional area could not be found or is inactive.",
        ErrorType.NotFound);

    public static readonly Error CompanyTypeNotFound = new(
        "COMPANY_TYPE_NOT_FOUND",
        "The selected company type could not be found or is inactive.",
        ErrorType.NotFound);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(OrgStructureCatalogPermissionCodes.ResourceKey, action);
}
