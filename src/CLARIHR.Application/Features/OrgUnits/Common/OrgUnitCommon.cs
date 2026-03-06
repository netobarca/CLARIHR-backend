using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Features.OrgUnits.Common;

public static partial class OrgUnitValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxDepth = 15;

    public static bool IsValidCode(string code) =>
        CodeRegex().IsMatch(code.Trim());

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_-]{0,49}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeRegex();
}

public static class OrgUnitPermissionCodes
{
    public const string Read = "OrgUnits.Read";
    public const string Admin = "OrgUnits.Admin";
    public const string ManageAdministration = "iam.administration.manage";
    public const string ResourceKey = "ORG_UNITS";
    public const string PlatformAdminRole = "platform_admin";
}

public static class OrgUnitErrors
{
    public static readonly Error Forbidden = new(
        "ORG_UNITS_FORBIDDEN",
        "You do not have permission to access organization unit administration.",
        ErrorType.Forbidden);

    public static readonly Error OrgUnitNotFound = new(
        "ORG_UNIT_NOT_FOUND",
        "The organization unit could not be found.",
        ErrorType.NotFound);

    public static readonly Error ParentNotFound = new(
        "ORG_UNIT_PARENT_NOT_FOUND",
        "The selected parent organization unit could not be found.",
        ErrorType.NotFound);

    public static readonly Error CodeConflict = new(
        "ORG_UNIT_CODE_CONFLICT",
        "Another organization unit already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error CycleDetected = new(
        "ORG_UNIT_CYCLE_DETECTED",
        "The requested move would create a cycle in the organization unit tree.",
        ErrorType.Conflict);

    public static readonly Error DepthLimitExceeded = new(
        "ORG_UNIT_DEPTH_LIMIT_EXCEEDED",
        "The requested hierarchy depth exceeds the maximum supported levels.",
        ErrorType.Conflict);

    public static readonly Error HasActiveChildren = new(
        "ORG_UNIT_HAS_ACTIVE_CHILDREN",
        "The organization unit cannot be inactivated because it still has active child units.",
        ErrorType.Conflict);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(OrgUnitPermissionCodes.ResourceKey, action);
}
