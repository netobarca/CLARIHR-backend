using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PlatformSubscriptions.Common;

public static class PlatformSubscriptionValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}

public static class PlatformSubscriptionErrors
{
    public static readonly Error CompanyNotFound = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_COMPANY_NOT_FOUND",
        "The requested company could not be found.",
        ErrorType.NotFound);

    public static readonly Error SubscriptionNotFound = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_NOT_FOUND",
        "The requested company subscription could not be found.",
        ErrorType.NotFound);

    public static readonly Error PlanNotFound = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_PLAN_NOT_FOUND",
        "The requested commercial plan could not be found.",
        ErrorType.NotFound);

    public static readonly Error PlanInactive = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_PLAN_INACTIVE",
        "Only active commercial plans can be assigned to companies.",
        ErrorType.Conflict);

    public static readonly Error PlanVersionNotAvailable = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_PLAN_VERSION_NOT_AVAILABLE",
        "The selected commercial plan does not have an effective version for the requested start date.",
        ErrorType.Conflict);

    public static readonly Error StartDateInPast = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_START_DATE_IN_PAST",
        "The requested subscription start date cannot be in the past.",
        ErrorType.Validation);

    public static readonly Error InvalidPeriodicity = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_INVALID_PERIODICITY",
        "The requested subscription periodicity is invalid.",
        ErrorType.Validation);

    public static readonly Error CompanyNotEligible = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_COMPANY_NOT_ELIGIBLE",
        "The company does not meet the minimum requirements to activate a subscription.",
        ErrorType.Conflict);

    public static readonly Error MissingLegalRepresentative = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_MISSING_LEGAL_REPRESENTATIVE",
        "The company requires at least one active legal representative before activating a subscription.",
        ErrorType.Conflict);

    public static readonly Error MissingAdministrator = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_MISSING_ADMINISTRATOR",
        "The company requires at least one active owner or administrator before activating a subscription.",
        ErrorType.Conflict);

    public static readonly Error ScheduledConflict = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_SCHEDULED_CONFLICT",
        "The company already has a scheduled subscription pending activation.",
        ErrorType.Conflict);

    public static readonly Error AlreadyAssigned = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_ALREADY_ASSIGNED",
        "The requested company already uses the selected commercial plan.",
        ErrorType.Conflict);
}
