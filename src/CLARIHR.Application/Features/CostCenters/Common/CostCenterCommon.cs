using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Features.CostCenters.Common;

public static partial class CostCenterValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static bool IsValidCode(string code) =>
        CodeRegex().IsMatch(code.Trim());

    public static bool IsValidAccountCode(string accountCode) =>
        AccountCodeRegex().IsMatch(accountCode.Trim());

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_-]{0,49}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeRegex();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_.-]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex AccountCodeRegex();
}

public static class CostCenterPermissionCodes
{
    public const string Read = "CostCenters.Read";
    public const string Admin = "CostCenters.Admin";
    public const string ManageAdministration = "iam.administration.manage";
    public const string ResourceKey = "COST_CENTERS";
    public const string PlatformAdminRole = "platform_admin";
}

public static class CostCenterErrors
{
    public static readonly Error Forbidden = new(
        "COST_CENTERS_FORBIDDEN",
        "You do not have permission to access cost center administration.",
        ErrorType.Forbidden);

    public static readonly Error CostCenterNotFound = new(
        "COST_CENTER_NOT_FOUND",
        "The cost center could not be found.",
        ErrorType.NotFound);

    public static readonly Error CodeConflict = new(
        "COST_CENTER_CODE_CONFLICT",
        "Another cost center already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error InUseConflict = new(
        "COST_CENTER_IN_USE",
        "The cost center cannot be inactivated because it is used by active organization units or position slots.",
        ErrorType.Conflict);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static readonly Error ExportFormatInvalid = new(
        "COST_CENTER_EXPORT_FORMAT_INVALID",
        "Unsupported export format.",
        ErrorType.Validation);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(CostCenterPermissionCodes.ResourceKey, action);
}
