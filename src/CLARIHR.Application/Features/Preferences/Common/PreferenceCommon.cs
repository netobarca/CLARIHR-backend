using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Features.Preferences.Common;

public static class CompanyPreferencePermissionCodes
{
    public const string Read = "CompanyPreferences.Read";
    public const string Admin = "CompanyPreferences.Admin";
    public const string ManageAdministration = "iam.administration.manage";
    public const string ResourceKey = "COMPANY_PREFERENCES";
}

public static class PreferenceErrors
{
    public static readonly Error UserPreferenceNotFound = new(
        "USER_PREFERENCE_NOT_FOUND",
        "The user preferences could not be found.",
        ErrorType.NotFound);

    public static readonly Error CompanyPreferenceNotFound = new(
        "COMPANY_PREFERENCE_NOT_FOUND",
        "The company preferences could not be found.",
        ErrorType.NotFound);

    public static readonly Error CompanyForbidden = new(
        "COMPANY_PREFERENCES_FORBIDDEN",
        "You do not have permission to access company preferences.",
        ErrorType.Forbidden);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static readonly Error InvalidCurrentUser = new(
        "INVALID_CURRENT_USER",
        "Current user context is invalid.",
        ErrorType.Unauthorized);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(CompanyPreferencePermissionCodes.ResourceKey, action);
}
