using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Features.Locations.Common;

public static partial class LocationValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MinSearchLength = 2;
    public const string DefaultGroupCode = "GENERAL";
    public const string DefaultGroupName = "General";
    public const string GeneralLevelDisplayName = "General";
    public const string CountryLevelDisplayName = "Pais";
    public const string DepartmentLevelDisplayName = "Departamento";
    public const string MunicipalityLevelDisplayName = "Municipio";
    public const string ElSalvadorCountryCode = "SV";
    public const string ElSalvadorCountryName = "El Salvador";

    // Single-source the (TenantId, LevelOrder) unique-index name so the EF config (HasDatabaseName) and
    // the create-handler's UniqueConstraintViolationException→409 mapping cannot drift (mirrors CostCenters R2).
    public const string LevelOrderUniqueConstraintName = "uq_location_levels__tenant_order";

    public static string NormalizeCountryCode(string countryCode) =>
        countryCode.Trim().ToUpperInvariant();

    public static bool SupportsSeedCountry(string countryCode) =>
        NormalizeCountryCode(countryCode) == ElSalvadorCountryCode;

    // §12.8: free-text search must impose a minimum length (after Trim) so the non-sargable
    // Normalized*.Contains(q) LIKE '%x%' scan cannot be triggered by a 1-char query. Empty/whitespace
    // = "no filter" (valid). Mirrors LegalRepresentatives/PersonnelFiles. Per-tenant location-group
    // cardinality is small, so the bounded scan above MinSearchLength is acceptable (ADR-0002).
    public static bool IsValidSearchLength(string? search) =>
        string.IsNullOrWhiteSpace(search) || search.Trim().Length >= MinSearchLength;

    public static bool IsValidCode(string code) =>
        CodeRegex().IsMatch(code.Trim());

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_-]{0,49}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeRegex();
}

public static class LocationPermissionCodes
{
    public const string Read = "Locations.Read";
    public const string Admin = "Locations.Admin";
    public const string ManageAdministration = "iam.administration.manage";
    public const string ResourceKey = "LOCATIONS";
}

public static class LocationErrors
{
    public static readonly Error Forbidden = new(
        "LOCATIONS_FORBIDDEN",
        "You do not have permission to access location administration.",
        ErrorType.Forbidden);

    public static readonly Error HierarchyNotFound = new(
        "LOCATION_HIERARCHY_NOT_FOUND",
        "The location hierarchy configuration could not be found.",
        ErrorType.NotFound);

    public static readonly Error LevelNotFound = new(
        "LOCATION_LEVEL_NOT_FOUND",
        "The location level could not be found.",
        ErrorType.NotFound);

    public static readonly Error GroupNotFound = new(
        "LOCATION_GROUP_NOT_FOUND",
        "The location group could not be found.",
        ErrorType.NotFound);

    public static readonly Error WorkCenterTypeNotFound = new(
        "WORK_CENTER_TYPE_NOT_FOUND",
        "The work center type could not be found.",
        ErrorType.NotFound);

    public static readonly Error WorkCenterNotFound = new(
        "WORK_CENTER_NOT_FOUND",
        "The work center could not be found.",
        ErrorType.NotFound);

    public static readonly Error LevelOrderConflict = new(
        "LOCATION_LEVEL_ORDER_CONFLICT",
        "Another location level already uses the requested level order.",
        ErrorType.Conflict);

    public static readonly Error GroupCodeConflict = new(
        "LOCATION_GROUP_CODE_CONFLICT",
        "Another location group already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error WorkCenterTypeCodeConflict = new(
        "WORK_CENTER_TYPE_CODE_CONFLICT",
        "Another work center type already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error WorkCenterCodeConflict = new(
        "WORK_CENTER_CODE_CONFLICT",
        "Another work center already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error GroupParentRequired = new(
        "LOCATION_GROUP_PARENT_REQUIRED",
        "A parent group is required for non-root levels.",
        ErrorType.Conflict);

    public static readonly Error GroupInvalidParent = new(
        "LOCATION_GROUP_INVALID_PARENT",
        "The selected parent group is not valid for the requested level.",
        ErrorType.Conflict);

    public static readonly Error GroupCycleDetected = new(
        "LOCATION_GROUP_CYCLE_DETECTED",
        "The requested move would create a cycle in the location group tree.",
        ErrorType.Conflict);

    public static readonly Error GroupHasActiveChildren = new(
        "LOCATION_GROUP_HAS_ACTIVE_CHILDREN",
        "The location group cannot be inactivated because it still has active child groups.",
        ErrorType.Conflict);

    public static readonly Error GroupHasActiveWorkCenters = new(
        "LOCATION_GROUP_HAS_ACTIVE_WORK_CENTERS",
        "The location group cannot be inactivated because it still has active work centers.",
        ErrorType.Conflict);

    public static readonly Error WorkCenterTypeInUse = new(
        "WORK_CENTER_TYPE_IN_USE",
        "The work center type cannot be inactivated because active work centers still use it.",
        ErrorType.Conflict);

    public static readonly Error WorkCenterHasActiveDependencies = new(
        "WORK_CENTER_HAS_ACTIVE_DEPENDENCIES",
        "The work center cannot be inactivated because it still has active dependencies.",
        ErrorType.Conflict);

    public static readonly Error DefaultGroupProtected = new(
        "DEFAULT_GROUP_PROTECTED",
        "The default location group is protected and cannot be renamed, recoded or inactivated.",
        ErrorType.Conflict);

    public static readonly Error LastActiveLevelRequired = new(
        "LAST_ACTIVE_LEVEL_REQUIRED",
        "At least one active location level must remain available.",
        ErrorType.Conflict);

    public static readonly Error SingleLevelRequiresOneActiveLevel = new(
        "LOCATION_SINGLE_LEVEL_REQUIRES_ONE_ACTIVE_LEVEL",
        "Single-level hierarchies require exactly one active level.",
        ErrorType.Conflict);

    public static readonly Error WorkCentersAllowedOnlyOnLastLevel = new(
        "WORK_CENTERS_ALLOWED_ONLY_ON_LAST_LEVEL",
        "Only the last active location level can allow work centers.",
        ErrorType.Conflict);

    public static readonly Error GroupLevelNotAllowedForWorkCenter = new(
        "LOCATION_GROUP_LEVEL_NOT_ALLOWED_FOR_WORK_CENTER",
        "The selected location group level cannot host work centers.",
        ErrorType.Conflict);

    public static readonly Error RequiredLevelMustRemainActive = new(
        "LOCATION_LEVEL_REQUIRED_ACTIVE",
        "Required location levels must remain active.",
        ErrorType.Conflict);

    public static readonly Error LocationLevelHasActiveGroups = new(
        "LOCATION_LEVEL_HAS_ACTIVE_GROUPS",
        "The location level cannot be inactivated because it still has active groups.",
        ErrorType.Conflict);

    public static readonly Error LocationLevelAllowsWorkCentersInUse = new(
        "LOCATION_LEVEL_ALLOWS_WORK_CENTERS_IN_USE",
        "The location level cannot be changed because active work centers depend on it.",
        ErrorType.Conflict);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static readonly Error WorkCenterTypeInactive = new(
        "WORK_CENTER_TYPE_INACTIVE",
        "The selected work center type is inactive.",
        ErrorType.Conflict);

    public static readonly Error LocationGroupInactive = new(
        "LOCATION_GROUP_INACTIVE",
        "The selected location group is inactive.",
        ErrorType.Conflict);

    public static readonly Error ParentGroupInactive = new(
        "LOCATION_GROUP_PARENT_INACTIVE",
        "The selected parent group is inactive.",
        ErrorType.Conflict);

    public static readonly Error InvalidCoordinates = new(
        "WORK_CENTER_INVALID_COORDINATES",
        "The provided latitude or longitude is outside the supported range.",
        ErrorType.Validation);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(LocationPermissionCodes.ResourceKey, action);
}

public static class LocationConstraintViolations
{
    // The (TenantId, LevelOrder) unique index is the real guard against duplicate level orders; the
    // up-front LevelOrderExistsAsync probe only closes the sequential case. On a concurrent create of the
    // same order, the second writer trips this index — map it to the same clean 409 as the probe instead
    // of letting the 23505 escape as an HTTP 500 (mirrors CostCenters R2).
    public static bool IsLevelOrderConflict(string? constraintName) =>
        string.Equals(constraintName, LocationValidationRules.LevelOrderUniqueConstraintName, StringComparison.Ordinal);
}
