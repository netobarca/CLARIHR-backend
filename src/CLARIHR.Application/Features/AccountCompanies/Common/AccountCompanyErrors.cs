using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.AccountCompanies.Common;

public static class AccountCompanyErrors
{
    public static readonly Error InvalidCurrentUser = new(
        "account_companies.current_user.invalid",
        "The current user context is invalid.",
        ErrorType.Unauthorized);

    public static readonly Error UserNotFound = new(
        "account_companies.user.not_found",
        "The current user could not be resolved.",
        ErrorType.NotFound);

    public static readonly Error CompanyNotFound = new(
        "COMPANY_NOT_FOUND",
        "The requested company could not be found.",
        ErrorType.NotFound);

    public static readonly Error OwnershipForbidden = new(
        "COMPANY_OWNERSHIP_FORBIDDEN",
        "You do not have permission to manage this company.",
        ErrorType.Forbidden);

    public static readonly Error CompanyLimitReached = new(
        "COMPANY_LIMIT_REACHED",
        "The current account cannot create additional active companies.",
        ErrorType.Conflict);

    public static readonly Error CompanyReactivationLimitReached = new(
        "COMPANY_REACTIVATION_LIMIT_REACHED",
        "The current account cannot reactivate this company because the active company limit has been reached.",
        ErrorType.Conflict);

    public static readonly Error CompanyAlreadyArchived = new(
        "COMPANY_ALREADY_ARCHIVED",
        "The company is already archived.",
        ErrorType.Conflict);

    public static readonly Error CompanyAlreadyActive = new(
        "COMPANY_ALREADY_ACTIVE",
        "The company is already active.",
        ErrorType.Conflict);

    public static readonly Error ActiveCompanyArchiveForbidden = new(
        "ACTIVE_COMPANY_ARCHIVE_FORBIDDEN",
        "Switch to another active company before archiving the current active company.",
        ErrorType.Conflict);

    public static readonly Error ActiveCompanySwitchForbidden = new(
        "ACTIVE_COMPANY_SWITCH_FORBIDDEN",
        "The requested company cannot become the active company.",
        ErrorType.Conflict);
}
