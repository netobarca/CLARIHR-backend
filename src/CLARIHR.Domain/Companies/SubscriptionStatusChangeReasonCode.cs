namespace CLARIHR.Domain.Companies;

public enum SubscriptionStatusChangeReasonCode
{
    InitialAssignment = 1,
    ManualActivation = 2,
    ActivationScheduled = 3,
    ScheduledStartReached = 4,
    TrialEnded = 5,
    ExpirationReached = 6,
    ManualSuspension = 7,
    PaymentDelinquency = 8,
    CustomerRequest = 9,
    CommercialCancellation = 10,
    AuthorizedReactivation = 11,
    SubscriptionReplacement = 12,
    LegacyMigration = 13,
    PlanChangeApplied = 14
}
