using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Features.CompetencyFramework.Common;

public static partial class CompetencyFrameworkValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static bool IsValidCode(string code) =>
        CodeRegex().IsMatch(code.Trim());

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_-]{0,49}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeRegex();
}

public static class CompetencyFrameworkPermissionCodes
{
    public const string Read = "CompetencyFramework.Read";
    public const string Admin = "CompetencyFramework.Admin";
    public const string ManageAdministration = "iam.administration.manage";
    public const string ResourceKey = "COMPETENCY_FRAMEWORK";
    public const string PlatformAdminRole = "platform_admin";
}

public static class CompetencyFrameworkErrors
{
    public static readonly Error Forbidden = new(
        "COMPETENCY_FRAMEWORK_FORBIDDEN",
        "You do not have permission to access competency framework administration.",
        ErrorType.Forbidden);

    public static readonly Error OccupationalPyramidLevelNotFound = new(
        "OCCUPATIONAL_PYRAMID_LEVEL_NOT_FOUND",
        "The occupational pyramid level could not be found.",
        ErrorType.NotFound);

    public static readonly Error OccupationalPyramidLevelCodeConflict = new(
        "OCCUPATIONAL_PYRAMID_LEVEL_CODE_CONFLICT",
        "Another occupational pyramid level already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error OccupationalPyramidLevelOrderConflict = new(
        "OCCUPATIONAL_PYRAMID_LEVEL_ORDER_CONFLICT",
        "Another occupational pyramid level already uses the requested level order.",
        ErrorType.Conflict);

    public static readonly Error OccupationalPyramidLevelInUse = new(
        "OCCUPATIONAL_PYRAMID_LEVEL_IN_USE",
        "The occupational pyramid level cannot be inactivated while it has active usage.",
        ErrorType.Conflict);

    public static readonly Error CompetencyConductNotFound = new(
        "COMPETENCY_CONDUCT_NOT_FOUND",
        "The competency conduct could not be found.",
        ErrorType.NotFound);

    public static readonly Error CompetencyConductDuplicate = new(
        "COMPETENCY_CONDUCT_DUPLICATE",
        "A conduct already exists for the same competency, type, level and description.",
        ErrorType.Conflict);

    public static readonly Error CompetencyConductInUse = new(
        "RESOURCE_IN_USE",
        "The competency conduct cannot be inactivated while it is associated to active job profile expectations.",
        ErrorType.Conflict);

    public static readonly Error CompetencyNotFound = new(
        "COMPETENCY_NOT_FOUND",
        "The selected competency could not be found or is inactive.",
        ErrorType.NotFound);

    public static readonly Error CompetencyTypeNotFound = new(
        "COMPETENCY_TYPE_NOT_FOUND",
        "The selected competency type could not be found or is inactive.",
        ErrorType.NotFound);

    public static readonly Error BehaviorLevelNotFound = new(
        "BEHAVIOR_LEVEL_NOT_FOUND",
        "The selected behavior level could not be found or is inactive.",
        ErrorType.NotFound);

    public static readonly Error BehaviorNotFound = new(
        "BEHAVIOR_NOT_FOUND",
        "The selected behavior could not be found or is inactive.",
        ErrorType.NotFound);

    public static readonly Error JobProfileNotFound = new(
        "JOB_PROFILE_NOT_FOUND",
        "The job profile could not be found.",
        ErrorType.NotFound);

    public static readonly Error JobProfileCompetencyMatrixConflict = new(
        "JOB_PROFILE_COMPETENCY_MATRIX_CONFLICT",
        "The requested competency matrix change is not valid for the current state.",
        ErrorType.Conflict);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static readonly Error ExportFormatInvalid = new(
        "COMPETENCY_FRAMEWORK_EXPORT_FORMAT_INVALID",
        "Unsupported export format.",
        ErrorType.Validation);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(CompetencyFrameworkPermissionCodes.ResourceKey, action);
}
