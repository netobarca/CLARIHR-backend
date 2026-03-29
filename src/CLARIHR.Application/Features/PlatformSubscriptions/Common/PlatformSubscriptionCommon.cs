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

    public static readonly Error AlreadyAssigned = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_ALREADY_ASSIGNED",
        "The requested company already uses the selected commercial plan.",
        ErrorType.Conflict);
}
