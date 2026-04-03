namespace CLARIHR.Domain.Companies;

public enum SubscriptionPlanChangeReasonCode
{
    UpgradeCommercial = 1,
    DowngradeRequestedByCustomer = 2,
    CommercialStrategyMigration = 3,
    OperationalCorrection = 4,
    LegacyToNewPlan = 5,
    Other = 6
}
