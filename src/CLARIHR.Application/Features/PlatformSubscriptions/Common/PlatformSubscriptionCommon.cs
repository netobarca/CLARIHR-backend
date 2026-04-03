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

    public static readonly Error ExpirationBeforeStartDate = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_EXPIRATION_BEFORE_START_DATE",
        "The requested subscription expiration date cannot be earlier than the start date.",
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

    public static readonly Error InvalidStatusTransition = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_INVALID_STATUS_TRANSITION",
        "The requested subscription status transition is not allowed.",
        ErrorType.Conflict);

    public static readonly Error InvalidStatusReason = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_INVALID_STATUS_REASON",
        "The provided reason code is not valid for the requested status transition.",
        ErrorType.Validation);

    public static readonly Error ReactivationPastExpiration = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_REACTIVATION_PAST_EXPIRATION",
        "The requested subscription can no longer be reactivated because it is already past its expiration date.",
        ErrorType.Conflict);

    public static readonly Error StatusChangeEffectiveDateRequired = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_STATUS_CHANGE_EFFECTIVE_DATE_REQUIRED",
        "An effective date is required for subscription reactivation.",
        ErrorType.Validation);

    public static readonly Error StatusChangeEffectiveDateInPast = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_STATUS_CHANGE_EFFECTIVE_DATE_IN_PAST",
        "The requested status change effective date cannot be in the past.",
        ErrorType.Validation);

    public static readonly Error StatusChangeSchedulingNotAllowed = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_STATUS_CHANGE_SCHEDULING_NOT_ALLOWED",
        "Only suspended to active reactivation can be scheduled in this version.",
        ErrorType.Validation);

    public static readonly Error StatusChangePendingConflict = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_STATUS_CHANGE_PENDING_CONFLICT",
        "The subscription already has a scheduled status change pending application.",
        ErrorType.Conflict);

    public static readonly Error ReactivationRequiresSuspendedStatus = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_REACTIVATION_REQUIRES_SUSPENDED_STATUS",
        "Only suspended subscriptions can be reactivated.",
        ErrorType.Conflict);

    public static readonly Error PlanChangeNotFound = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_PLAN_CHANGE_NOT_FOUND",
        "The requested subscription plan change could not be found.",
        ErrorType.NotFound);

    public static readonly Error PlanChangeUnsupportedCurrentStatus = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_PLAN_CHANGE_UNSUPPORTED_CURRENT_STATUS",
        "The current subscription status does not allow plan changes.",
        ErrorType.Conflict);

    public static readonly Error PlanChangeSamePlanVersion = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_PLAN_CHANGE_SAME_PLAN_VERSION",
        "The requested change does not modify the effective plan or version.",
        ErrorType.Conflict);

    public static readonly Error PlanChangePendingConflict = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_PLAN_CHANGE_PENDING_CONFLICT",
        "The company already has a scheduled plan change pending application.",
        ErrorType.Conflict);

    public static readonly Error PlanChangeInvalidMode = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_PLAN_CHANGE_INVALID_MODE",
        "The requested plan change mode is invalid.",
        ErrorType.Validation);

    public static readonly Error PlanChangeEffectiveDateRequired = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_PLAN_CHANGE_EFFECTIVE_DATE_REQUIRED",
        "A future effective date is required for the selected plan change mode.",
        ErrorType.Validation);

    public static readonly Error PlanChangeEffectiveDateInPast = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_PLAN_CHANGE_EFFECTIVE_DATE_IN_PAST",
        "The requested effective date cannot be in the past.",
        ErrorType.Validation);

    public static readonly Error PlanChangeDatePastExpiration = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_PLAN_CHANGE_DATE_PAST_EXPIRATION",
        "The requested effective date is not valid because the current subscription expires before that date.",
        ErrorType.Conflict);

    public static readonly Error PlanChangeCancellationNotAllowed = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_PLAN_CHANGE_CANCELLATION_NOT_ALLOWED",
        "Only scheduled plan changes that are still pending can be cancelled.",
        ErrorType.Conflict);

    public static readonly Error AddonNotFound = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_ADDON_NOT_FOUND",
        "The requested commercial add-on could not be found.",
        ErrorType.NotFound);

    public static readonly Error AddonInactive = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_ADDON_INACTIVE",
        "Only active commercial add-ons can be assigned to companies.",
        ErrorType.Conflict);

    public static readonly Error AddonUnsupportedCurrentStatus = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_ADDON_UNSUPPORTED_CURRENT_STATUS",
        "The current subscription status does not allow add-on changes.",
        ErrorType.Conflict);

    public static readonly Error AddonAlreadyActive = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_ADDON_ALREADY_ACTIVE",
        "The requested add-on is already active for the company.",
        ErrorType.Conflict);

    public static readonly Error AddonNotActive = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_ADDON_NOT_ACTIVE",
        "Only active company add-ons can be deactivated.",
        ErrorType.Conflict);

    public static readonly Error AddonPendingConflict = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_ADDON_PENDING_CONFLICT",
        "The company already has a scheduled change pending for the requested add-on.",
        ErrorType.Conflict);

    public static readonly Error AddonChangeNotFound = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_ADDON_CHANGE_NOT_FOUND",
        "The requested company add-on change could not be found.",
        ErrorType.NotFound);

    public static readonly Error AddonChangeInvalidAction = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_ADDON_CHANGE_INVALID_ACTION",
        "The requested add-on change action is invalid.",
        ErrorType.Validation);

    public static readonly Error AddonChangeInvalidMode = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_ADDON_CHANGE_INVALID_MODE",
        "The requested add-on change mode is invalid.",
        ErrorType.Validation);

    public static readonly Error AddonChangeEffectiveDateRequired = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_ADDON_CHANGE_EFFECTIVE_DATE_REQUIRED",
        "A future effective date is required for the selected add-on change mode.",
        ErrorType.Validation);

    public static readonly Error AddonChangeEffectiveDateInPast = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_ADDON_CHANGE_EFFECTIVE_DATE_IN_PAST",
        "The requested add-on effective date cannot be in the past.",
        ErrorType.Validation);

    public static readonly Error AddonChangeReasonRequired = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_ADDON_CHANGE_REASON_REQUIRED",
        "A reason code is required for the requested add-on change.",
        ErrorType.Validation);

    public static readonly Error AddonChangeCancellationNotAllowed = new(
        "PLATFORM_COMPANY_SUBSCRIPTION_ADDON_CHANGE_CANCELLATION_NOT_ALLOWED",
        "Only scheduled add-on changes that are still pending can be cancelled.",
        ErrorType.Conflict);
}
