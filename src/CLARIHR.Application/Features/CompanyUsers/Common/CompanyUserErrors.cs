using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.CompanyUsers.Common;

public static class CompanyUserErrors
{
    public static readonly Error TenantContextRequired = new(
        "company_users.tenant.required",
        "A tenant context is required to manage company users.",
        ErrorType.Unauthorized);

    public static readonly Error InvalidCurrentUser = new(
        "company_users.current_user.invalid",
        "The current user context is invalid.",
        ErrorType.Unauthorized);

    public static readonly Error ManagementPermissionRequired = new(
        "company_users.management.forbidden",
        "You do not have permission to manage company users.",
        ErrorType.Forbidden);

    public static readonly Error ModuleDisabledByPlan = new(
        "company_users.module.disabled_by_plan",
        "The current plan does not enable company user management.",
        ErrorType.Forbidden);

    public static readonly Error CompanyNotFound = new(
        "company_users.company.not_found",
        "The current company could not be resolved.",
        ErrorType.NotFound);

    public static readonly Error UserNotFound = new(
        "company_users.user.not_found",
        "The requested company user was not found.",
        ErrorType.NotFound);

    public static readonly Error RoleNotFound = new(
        "company_users.role.not_found",
        "The requested role was not found in the current company.",
        ErrorType.NotFound);

    public static readonly Error UserAlreadyInCompany = new(
        "company_users.user_already_in_company",
        "The user already belongs to the current company.",
        ErrorType.Conflict);

    public static readonly Error UserAssignedToAnotherCompany = new(
        "company_users.user_in_another_company",
        "The user is already assigned to a different company.",
        ErrorType.Conflict);

    public static readonly Error LastActiveAdministratorRequired = new(
        "company_users.last_admin_required",
        "The company must keep at least one active administrator.",
        ErrorType.Conflict);

    public static readonly Error InvitationNotSupportedForExternalUser = new(
        "company_users.reset_invitation.external_user_not_supported",
        "Reset invitation is only available for local users.",
        ErrorType.Conflict);

    public static readonly Error FieldEditForbidden = new(
        "FIELD_EDIT_FORBIDDEN",
        "One or more submitted fields cannot be modified by the current user.",
        ErrorType.Forbidden);
}
