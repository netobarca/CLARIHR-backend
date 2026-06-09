using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.IdentityAccess.Common;

public static class IdentityAccessErrors
{
    public static readonly Error TenantContextRequired = new(
        "iam.tenant.required",
        "A tenant context is required to access identity administration.",
        ErrorType.Unauthorized);

    public static readonly Error InvalidCurrentUser = new(
        "iam.current_user.invalid",
        "The current user context is invalid.",
        ErrorType.Unauthorized);

    public static readonly Error ManagementPermissionRequired = new(
        "iam.management.forbidden",
        "You do not have permission to manage identity access.",
        ErrorType.Forbidden);

    public static readonly Error ModuleDisabledByPlan = new(
        "iam.module.disabled_by_plan",
        "The current plan does not enable this module.",
        ErrorType.Forbidden);

    public static readonly Error UserAlreadyExists = new(
        "iam.users.email_conflict",
        "A user with the same email already exists in this tenant.",
        ErrorType.Conflict);

    public static readonly Error RoleAlreadyExists = new(
        "iam.roles.name_conflict",
        "A role with the same name already exists in this tenant.",
        ErrorType.Conflict);

    public static readonly Error PermissionAlreadyExists = new(
        "iam.permissions.code_conflict",
        "A permission with the same code already exists in this tenant.",
        ErrorType.Conflict);

    public static readonly Error UserNotFound = new(
        "iam.users.not_found",
        "The requested user was not found.",
        ErrorType.NotFound);

    public static readonly Error RoleNotFound = new(
        "iam.roles.not_found",
        "The requested role was not found.",
        ErrorType.NotFound);

    public static readonly Error PermissionNotFound = new(
        "iam.permissions.not_found",
        "The requested permission was not found.",
        ErrorType.NotFound);

    public static readonly Error RolesNotFound = new(
        "iam.roles.collection_not_found",
        "One or more requested roles were not found.",
        ErrorType.NotFound);

    public static readonly Error UsersNotFound = new(
        "iam.users.collection_not_found",
        "One or more requested users were not found.",
        ErrorType.NotFound);

    public static readonly Error PermissionsNotFound = new(
        "iam.permissions.collection_not_found",
        "One or more requested permissions were not found.",
        ErrorType.NotFound);

    public static readonly Error ProtectedRoleModificationForbidden = new(
        "iam.roles.protected_role.forbidden",
        "Protected system roles cannot be modified.",
        ErrorType.Forbidden);

    public static readonly Error ProtectedRoleDeletionForbidden = new(
        "iam.roles.protected_role.delete_forbidden",
        "Protected system roles cannot be deleted.",
        ErrorType.Forbidden);

    public static readonly Error RoleAssignedToUsers = new(
        "iam.roles.in_use",
        "Roles with assigned users cannot be deleted.",
        ErrorType.Conflict);

    public static readonly Error LastAdministratorRequired = new(
        "iam.roles.last_administrator_required",
        "The tenant must keep at least one active administrator.",
        ErrorType.Conflict);

    // A stale If-Match on a role write (strong token on IamRole) or a user-roles write (weak computed
    // ETag over the IamUser projection) maps to 409 CONCURRENCY_CONFLICT, consistent with the app-wide
    // convention (no 412/428). The "CONCURRENCY_CONFLICT" code is shared and already localized.
    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);
}
