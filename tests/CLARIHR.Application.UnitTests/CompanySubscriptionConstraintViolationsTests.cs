using CLARIHR.Application.Features.PlatformSubscriptions.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// ACS-A guardrail: pins the CompanySubscription-family filtered unique-index names that the account
/// self-service handlers map to <c>409 CONCURRENCY_CONFLICT</c>. The expected names are spelled as
/// literals here (NOT via the const) so a rename of <see cref="CompanySubscriptionConstraintViolations"/>
/// — which is the SAME const the EF mapping uses — fails this test instead of silently degrading the
/// 23505 → 409 race mapping back into an HTTP 500.
/// </summary>
public sealed class CompanySubscriptionConstraintViolationsTests
{
    [Theory]
    [InlineData("uq_company_subscriptions__company_live")]
    [InlineData("uq_company_subscriptions__company_scheduled")]
    [InlineData("uq_company_subscription_plan_changes__company_scheduled")]
    [InlineData("uq_company_commercial_addons__company_addon")]
    [InlineData("uq_company_commercial_addon_changes__company_addon_scheduled")]
    public void IsConcurrencyConflict_IsTrue_ForEverySubscriptionFamilyUniqueIndex(string constraintName) =>
        Assert.True(CompanySubscriptionConstraintViolations.IsConcurrencyConflict(constraintName));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("pk_company_subscriptions")]
    [InlineData("uq_cost_centers__tenant_code")]
    public void IsConcurrencyConflict_IsFalse_ForUnrelatedOrMissingConstraints(string? constraintName) =>
        Assert.False(CompanySubscriptionConstraintViolations.IsConcurrencyConflict(constraintName));
}
