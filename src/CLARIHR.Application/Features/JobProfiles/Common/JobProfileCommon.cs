using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Features.JobProfiles.Common;

public static partial class JobProfileValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static bool IsValidCode(string code) =>
        CodeRegex().IsMatch(code.Trim());

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_-]{0,49}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeRegex();
}

public static class JobProfilePermissionCodes
{
    public const string Read = "JobProfiles.Read";
    public const string Admin = "JobProfiles.Admin";
    public const string CatalogAdmin = "JobCatalogs.Admin";
    public const string ManageAdministration = "iam.administration.manage";
    public const string ResourceKey = "JOB_PROFILES";
}

public static class JobProfileErrors
{
    public static readonly Error Forbidden = new(
        "JOB_PROFILES_FORBIDDEN",
        "You do not have permission to access job profile administration.",
        ErrorType.Forbidden);

    public static readonly Error JobProfileNotFound = new(
        "JOB_PROFILE_NOT_FOUND",
        "The job profile could not be found.",
        ErrorType.NotFound);

    public static readonly Error CatalogItemNotFound = new(
        "JOB_CATALOG_ITEM_NOT_FOUND",
        "The job catalog item could not be found.",
        ErrorType.NotFound);

    public static readonly Error RequirementNotFound = new(
        "JOB_PROFILE_REQUIREMENT_NOT_FOUND",
        "The job profile requirement could not be found.",
        ErrorType.NotFound);

    public static readonly Error FunctionNotFound = new(
        "JOB_PROFILE_FUNCTION_NOT_FOUND",
        "The job profile function could not be found.",
        ErrorType.NotFound);

    public static readonly Error OrgUnitNotFound = new(
        "JOB_PROFILE_ORG_UNIT_NOT_FOUND",
        "The selected organization unit could not be found.",
        ErrorType.NotFound);

    public static readonly Error OrgUnitRequired = new(
        "JOB_PROFILE_ORG_UNIT_REQUIRED",
        "An organization unit is required for the job profile.",
        ErrorType.Validation);

    public static readonly Error ReportsToProfileNotFound = new(
        "JOB_PROFILE_REPORTS_TO_NOT_FOUND",
        "The selected reporting profile could not be found.",
        ErrorType.NotFound);

    public static readonly Error CodeConflict = new(
        "JOB_PROFILE_CODE_CONFLICT",
        "Another job profile already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error CatalogCodeConflict = new(
        "JOB_CATALOG_ITEM_CODE_CONFLICT",
        "Another catalog item already uses the requested code for this category.",
        ErrorType.Conflict);

    public static readonly Error DependencyCycle = new(
        "JOB_PROFILE_DEPENDENCY_CYCLE",
        "This change cannot be saved because it would make the selected job profiles depend on each other in a circular way. Review the reporting profile and dependent positions, then try again.",
        ErrorType.Conflict);

    public static readonly Error StateConflict = new(
        "JOB_PROFILE_STATE_CONFLICT",
        "The requested action is not allowed for the current profile state.",
        ErrorType.Conflict);

    public static readonly Error PublishRequirementsMissing = new(
        "JOB_PROFILE_PUBLISH_REQUIREMENTS_MISSING",
        "The job profile does not meet the minimum requirements to be published.",
        ErrorType.UnprocessableEntity);

    public static readonly Error InlineCatalogCreateForbidden = new(
        "JOB_CATALOG_INLINE_CREATE_FORBIDDEN",
        "You do not have permission to create catalog items inline.",
        ErrorType.Forbidden);

    public static readonly Error CatalogItemInactive = new(
        "JOB_CATALOG_ITEM_INACTIVE",
        "The selected catalog item is inactive.",
        ErrorType.Conflict);

    public static readonly Error CompensationTabulatorLineNotFound = new(
        "JOB_PROFILE_COMPENSATION_TABULATOR_LINE_NOT_FOUND",
        "The selected salary tabulator line is not active for the profile effective date.",
        ErrorType.UnprocessableEntity);

    public static readonly Error ExportFormatInvalid = new(
        "JOB_PROFILE_EXPORT_FORMAT_INVALID",
        "Unsupported export format. Supported values are json and csv.",
        ErrorType.Validation);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(JobProfilePermissionCodes.ResourceKey, action);
}
