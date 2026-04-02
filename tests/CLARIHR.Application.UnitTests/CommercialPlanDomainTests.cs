using CLARIHR.Domain.Companies;

namespace CLARIHR.Application.UnitTests;

public sealed class CommercialPlanDomainTests
{
    [Fact]
    public void Create_ShouldNormalizePlanAndLimitCodes_AndOrderLimits()
    {
        var plan = CommercialPlan.Create(
            " pro ",
            " Professional ",
            " Base plan ",
            120m,
            3.5m,
            CommercialPlanStatus.Draft,
            isSystemPlan: false,
            limits:
            [
                (" employees ", 25m),
                ("locations", 3m)
            ]);

        Assert.Equal("PRO", plan.Code);
        Assert.Equal("PRO", plan.NormalizedCode);
        Assert.Equal("Professional", plan.Name);
        Assert.Equal("PROFESSIONAL", plan.NormalizedName);
        Assert.Equal("Base plan", plan.Description);
        Assert.Collection(
            plan.Limits,
            limit =>
            {
                Assert.Equal("EMPLOYEES", limit.LimitCode);
                Assert.Equal("EMPLOYEES", limit.NormalizedLimitCode);
                Assert.Equal(25m, limit.Value);
            },
            limit =>
            {
                Assert.Equal("LOCATIONS", limit.LimitCode);
                Assert.Equal("LOCATIONS", limit.NormalizedLimitCode);
                Assert.Equal(3m, limit.Value);
            });
    }

    [Fact]
    public void Create_ShouldRejectNegativeAmounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CommercialPlan.Create(
                "PRO",
                "Professional",
                null,
                -1m,
                3m,
                CommercialPlanStatus.Draft,
                isSystemPlan: false,
                limits: []));

        Assert.Throws<ArgumentOutOfRangeException>(() => CommercialPlanLimit.Create("EMPLOYEES", -5m));
    }

    [Fact]
    public void Create_ShouldRejectDuplicateLimitCodes()
    {
        Assert.Throws<InvalidOperationException>(() =>
            CommercialPlan.Create(
                "PRO",
                "Professional",
                null,
                120m,
                3m,
                CommercialPlanStatus.Draft,
                isSystemPlan: false,
                limits:
                [
                    ("employees", 25m),
                    (" EMPLOYEES ", 30m)
                ]));
    }

    [Fact]
    public void Update_ShouldRefreshConcurrencyToken_AndReplaceLimits()
    {
        var plan = CommercialPlan.Create(
            "PRO",
            "Professional",
            null,
            120m,
            3m,
            CommercialPlanStatus.Draft,
            isSystemPlan: false,
            limits: [("employees", 25m)]);

        var tokenBefore = plan.ConcurrencyToken;

        plan.Update(
            "PRO",
            "Professional Plus",
            "Updated",
            180m,
            4m,
            [("work_centers", 10m)]);

        Assert.NotEqual(tokenBefore, plan.ConcurrencyToken);
        Assert.Equal("Professional Plus", plan.Name);
        Assert.Equal("Updated", plan.Description);
        Assert.Equal(180m, plan.BaseMonthlyFee);
        Assert.Equal(4m, plan.PricePerActiveEmployee);
        var limit = Assert.Single(plan.Limits);
        Assert.Equal("WORK_CENTERS", limit.LimitCode);
        Assert.Equal(10m, limit.Value);
    }

    [Fact]
    public void Create_ShouldCreateInitialPricingVersion()
    {
        var effectiveFromUtc = DateTime.Parse("2026-04-02T00:00:00Z").ToUniversalTime();

        var plan = CommercialPlan.Create(
            "PRO",
            "Professional",
            null,
            120m,
            3m,
            CommercialPlanStatus.Active,
            isSystemPlan: false,
            limits: [],
            initialVersionEffectiveFromUtc: effectiveFromUtc);

        var version = Assert.Single(plan.Versions);
        Assert.Equal(1, version.VersionNumber);
        Assert.Equal("USD", version.CurrencyCode);
        Assert.Equal(effectiveFromUtc, version.EffectiveFromUtc);
        Assert.Equal(120m, version.BaseMonthlyFee);
        Assert.Equal(3m, version.PricePerActiveEmployee);
    }

    [Fact]
    public void Update_WhenPricingChanges_ShouldCreateNewVersionAndClosePreviousOne()
    {
        var initialUtc = DateTime.Parse("2026-04-02T00:00:00Z").ToUniversalTime();
        var nextUtc = DateTime.Parse("2026-05-01T00:00:00Z").ToUniversalTime();

        var plan = CommercialPlan.Create(
            "PRO",
            "Professional",
            null,
            120m,
            3m,
            CommercialPlanStatus.Active,
            isSystemPlan: false,
            limits: [],
            initialVersionEffectiveFromUtc: initialUtc);

        plan.Update(
            "PRO",
            "Professional",
            null,
            180m,
            4m,
            [],
            priceEffectiveFromUtc: nextUtc);

        Assert.Equal(2, plan.Versions.Count);
        var previous = plan.Versions.Single(version => version.VersionNumber == 1);
        var current = plan.Versions.Single(version => version.VersionNumber == 2);

        Assert.Equal(nextUtc, previous.EffectiveToUtc);
        Assert.Equal(nextUtc, current.EffectiveFromUtc);
        Assert.Equal(180m, current.BaseMonthlyFee);
        Assert.Equal(4m, current.PricePerActiveEmployee);
    }

    [Fact]
    public void Activate_AndInactivate_ShouldTransitionStatus_AndRefreshConcurrencyToken()
    {
        var plan = CommercialPlan.Create(
            "PRO",
            "Professional",
            null,
            120m,
            3m,
            CommercialPlanStatus.Draft,
            isSystemPlan: false,
            limits: []);

        var tokenBeforeActivate = plan.ConcurrencyToken;

        plan.Activate();

        Assert.Equal(CommercialPlanStatus.Active, plan.Status);
        Assert.NotEqual(tokenBeforeActivate, plan.ConcurrencyToken);

        var tokenBeforeInactivate = plan.ConcurrencyToken;

        plan.Inactivate();

        Assert.Equal(CommercialPlanStatus.Inactive, plan.Status);
        Assert.NotEqual(tokenBeforeInactivate, plan.ConcurrencyToken);
    }
}
