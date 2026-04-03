namespace CLARIHR.Domain.Companies;

public static class SubscriptionStatusPolicy
{
    public static bool CanOperate(SubscriptionStatus status) =>
        status is SubscriptionStatus.Active or SubscriptionStatus.Trial;

    public static bool CanGenerateCharges(SubscriptionStatus status) =>
        status == SubscriptionStatus.Active;

    public static bool IsTerminal(SubscriptionStatus status) =>
        status is SubscriptionStatus.Expired or SubscriptionStatus.Cancelled;

    public static bool OccupiesCurrentSlot(SubscriptionStatus status) =>
        status is SubscriptionStatus.Draft or SubscriptionStatus.Trial or SubscriptionStatus.Active or SubscriptionStatus.Suspended;

    public static bool CanTransition(
        SubscriptionStatus fromStatus,
        SubscriptionStatus toStatus,
        SubscriptionStatusChangeOrigin origin) =>
        origin switch
        {
            SubscriptionStatusChangeOrigin.PlatformOperator => CanTransitionManually(fromStatus, toStatus),
            SubscriptionStatusChangeOrigin.CompanyOwner => CanTransitionManually(fromStatus, toStatus),
            SubscriptionStatusChangeOrigin.SystemProcess => CanTransitionAutomatically(fromStatus, toStatus),
            _ => false
        };

    public static bool IsReasonAllowed(
        SubscriptionStatus? fromStatus,
        SubscriptionStatus toStatus,
        SubscriptionStatusChangeOrigin origin,
        SubscriptionStatusChangeReasonCode reasonCode) =>
        (fromStatus, toStatus, origin) switch
        {
            (null, SubscriptionStatus.Active, SubscriptionStatusChangeOrigin.PlatformOperator) =>
                reasonCode is SubscriptionStatusChangeReasonCode.ManualActivation
                    or SubscriptionStatusChangeReasonCode.PlanChangeApplied,
            (null, SubscriptionStatus.Active, SubscriptionStatusChangeOrigin.CompanyOwner) =>
                reasonCode is SubscriptionStatusChangeReasonCode.ManualActivation
                    or SubscriptionStatusChangeReasonCode.PlanChangeApplied,
            (null, SubscriptionStatus.Active, SubscriptionStatusChangeOrigin.SystemProcess) =>
                reasonCode is SubscriptionStatusChangeReasonCode.InitialAssignment
                    or SubscriptionStatusChangeReasonCode.PlanChangeApplied,
            (null, SubscriptionStatus.Scheduled, SubscriptionStatusChangeOrigin.PlatformOperator) =>
                reasonCode == SubscriptionStatusChangeReasonCode.ActivationScheduled,
            (null, SubscriptionStatus.Scheduled, SubscriptionStatusChangeOrigin.CompanyOwner) =>
                reasonCode == SubscriptionStatusChangeReasonCode.ActivationScheduled,
            (null, SubscriptionStatus.Scheduled, SubscriptionStatusChangeOrigin.SystemProcess) =>
                reasonCode == SubscriptionStatusChangeReasonCode.InitialAssignment,
            (SubscriptionStatus.Scheduled, SubscriptionStatus.Active, SubscriptionStatusChangeOrigin.SystemProcess) =>
                reasonCode == SubscriptionStatusChangeReasonCode.ScheduledStartReached,
            (SubscriptionStatus.Active, SubscriptionStatus.Expired, SubscriptionStatusChangeOrigin.SystemProcess) =>
                reasonCode == SubscriptionStatusChangeReasonCode.ExpirationReached,
            (SubscriptionStatus.Active, SubscriptionStatus.Suspended, SubscriptionStatusChangeOrigin.PlatformOperator) =>
                reasonCode is SubscriptionStatusChangeReasonCode.ManualSuspension or SubscriptionStatusChangeReasonCode.PaymentDelinquency,
            (SubscriptionStatus.Active, SubscriptionStatus.Suspended, SubscriptionStatusChangeOrigin.CompanyOwner) =>
                reasonCode is SubscriptionStatusChangeReasonCode.ManualSuspension or SubscriptionStatusChangeReasonCode.PaymentDelinquency,
            (SubscriptionStatus.Suspended, SubscriptionStatus.Active, SubscriptionStatusChangeOrigin.PlatformOperator) =>
                reasonCode == SubscriptionStatusChangeReasonCode.AuthorizedReactivation,
            (SubscriptionStatus.Suspended, SubscriptionStatus.Active, SubscriptionStatusChangeOrigin.CompanyOwner) =>
                reasonCode == SubscriptionStatusChangeReasonCode.AuthorizedReactivation,
            (SubscriptionStatus.Suspended, SubscriptionStatus.Active, SubscriptionStatusChangeOrigin.SystemProcess) =>
                reasonCode == SubscriptionStatusChangeReasonCode.AuthorizedReactivation,
            (_, SubscriptionStatus.Cancelled, SubscriptionStatusChangeOrigin.SystemProcess) =>
                reasonCode == SubscriptionStatusChangeReasonCode.SubscriptionReplacement,
            (_, SubscriptionStatus.Cancelled, SubscriptionStatusChangeOrigin.PlatformOperator) =>
                reasonCode is SubscriptionStatusChangeReasonCode.CustomerRequest
                    or SubscriptionStatusChangeReasonCode.CommercialCancellation
                    or SubscriptionStatusChangeReasonCode.SubscriptionReplacement,
            (_, SubscriptionStatus.Cancelled, SubscriptionStatusChangeOrigin.CompanyOwner) =>
                reasonCode is SubscriptionStatusChangeReasonCode.CustomerRequest
                    or SubscriptionStatusChangeReasonCode.CommercialCancellation
                    or SubscriptionStatusChangeReasonCode.SubscriptionReplacement,
            (_, _, _) => false
        };

    private static bool CanTransitionManually(SubscriptionStatus fromStatus, SubscriptionStatus toStatus) =>
        (fromStatus, toStatus) switch
        {
            (SubscriptionStatus.Active, SubscriptionStatus.Suspended) => true,
            (SubscriptionStatus.Active, SubscriptionStatus.Cancelled) => true,
            (SubscriptionStatus.Suspended, SubscriptionStatus.Active) => true,
            (SubscriptionStatus.Suspended, SubscriptionStatus.Cancelled) => true,
            (SubscriptionStatus.Scheduled, SubscriptionStatus.Cancelled) => true,
            _ => false
        };

    private static bool CanTransitionAutomatically(SubscriptionStatus fromStatus, SubscriptionStatus toStatus) =>
        (fromStatus, toStatus) switch
        {
            (SubscriptionStatus.Scheduled, SubscriptionStatus.Active) => true,
            (SubscriptionStatus.Active, SubscriptionStatus.Cancelled) => true,
            (SubscriptionStatus.Trial, SubscriptionStatus.Cancelled) => true,
            (SubscriptionStatus.Suspended, SubscriptionStatus.Active) => true,
            (SubscriptionStatus.Suspended, SubscriptionStatus.Cancelled) => true,
            (SubscriptionStatus.Active, SubscriptionStatus.Expired) => true,
            _ => false
        };
}
