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

    public static readonly Error MasterPlanForbidden = new(
        "ACCOUNT_COMPANY_SUBSCRIPTION_MASTER_FORBIDDEN",
        "The MASTER subscription is reserved for CLARI operators.",
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

    // AC-5: the company resolved and ownership passed, but it has no active commercial subscription/plan to
    // build an access context from. This is NOT "company not found" (the prior code), it is a subscription
    // gap — mirror the sibling AccountCompanySubscriptionsController which surfaces a subscription error.
    public static readonly Error SubscriptionContextUnavailable = new(
        "ACCOUNT_COMPANY_SUBSCRIPTION_NOT_FOUND",
        "The company has no active subscription to resolve its access context.",
        ErrorType.NotFound);

    // AC-5: reading a per-resource policy requires the company to be the caller's active tenant context.
    // Distinct from the switch guard above (this is a read precondition, not a failed switch).
    public static readonly Error ActiveCompanyContextRequired = new(
        "ACCOUNT_COMPANY_ACTIVE_CONTEXT_REQUIRED",
        "Switch to this company before reading its resource policies.",
        ErrorType.Conflict);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The company was modified by another request. Refresh and try again.",
        ErrorType.Conflict);
}
