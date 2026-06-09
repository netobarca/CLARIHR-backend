namespace CLARIHR.Application.Features.PlatformSubscriptions.Common;

/// <summary>
/// Single source of truth for the CompanySubscription-family filtered unique-index names — referenced
/// by both the EF mappings (<c>CompanySubscriptionConfiguration</c>) and the account self-service
/// command handlers' concurrency-race backstop (ACS-A), so a rename cannot silently degrade the
/// Postgres 23505 → clean <c>409 CONCURRENCY_CONFLICT</c> mapping back into an HTTP 500. Every one of
/// these constraints can only be violated when a concurrent request has already mutated the company's
/// subscription / add-on state (at most one live + one scheduled subscription, one scheduled plan-change,
/// one company-add-on row, one scheduled add-on-change), so they all map to the same conflict.
/// </summary>
public static class CompanySubscriptionConstraintViolations
{
    public const string LiveSubscriptionConstraintName = "uq_company_subscriptions__company_live";

    public const string ScheduledSubscriptionConstraintName = "uq_company_subscriptions__company_scheduled";

    public const string ScheduledPlanChangeConstraintName = "uq_company_subscription_plan_changes__company_scheduled";

    public const string CompanyAddonConstraintName = "uq_company_commercial_addons__company_addon";

    public const string ScheduledAddonChangeConstraintName = "uq_company_commercial_addon_changes__company_addon_scheduled";

    public static bool IsConcurrencyConflict(string? constraintName) =>
        constraintName is LiveSubscriptionConstraintName
            or ScheduledSubscriptionConstraintName
            or ScheduledPlanChangeConstraintName
            or CompanyAddonConstraintName
            or ScheduledAddonChangeConstraintName;
}
