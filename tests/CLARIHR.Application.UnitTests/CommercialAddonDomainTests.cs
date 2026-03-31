using CLARIHR.Domain.Companies;

namespace CLARIHR.Application.UnitTests;

public sealed class CommercialAddonDomainTests
{
    [Fact]
    public void Create_ShouldNormalizeCodeAndName()
    {
        var addon = CommercialAddon.Create(
            " addon-attendance ",
            " Attendance ",
            " Time tracking ",
            CommercialAddonType.Massive,
            1.2m,
            35m,
            CommercialAddonPeriodicity.Monthly,
            CommercialAddonStatus.Draft);

        Assert.Equal("ADDON-ATTENDANCE", addon.Code);
        Assert.Equal("ADDON-ATTENDANCE", addon.NormalizedCode);
        Assert.Equal("Attendance", addon.Name);
        Assert.Equal("ATTENDANCE", addon.NormalizedName);
        Assert.Equal("Time tracking", addon.Description);
    }

    [Fact]
    public void Create_ShouldAllowNullMinimumMonthlyFee()
    {
        var addon = CommercialAddon.Create(
            "ADDON-PAYROLL-ES",
            "Payroll ES",
            null,
            CommercialAddonType.Massive,
            2.5m,
            null,
            CommercialAddonPeriodicity.Annual,
            CommercialAddonStatus.Draft);

        Assert.Null(addon.MinimumMonthlyFee);
        Assert.Equal(CommercialAddonPeriodicity.Annual, addon.Periodicity);
    }

    [Fact]
    public void Create_ShouldRejectNegativeAmounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CommercialAddon.Create(
                "ADDON-ATTENDANCE",
                "Attendance",
                null,
                CommercialAddonType.Massive,
                -1m,
                null,
                CommercialAddonPeriodicity.Monthly,
                CommercialAddonStatus.Draft));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CommercialAddon.Create(
                "ADDON-ATTENDANCE",
                "Attendance",
                null,
                CommercialAddonType.Massive,
                1m,
                -10m,
                CommercialAddonPeriodicity.Monthly,
                CommercialAddonStatus.Draft));
    }

    [Fact]
    public void Update_ShouldRefreshConcurrencyToken_AndEditableFields()
    {
        var addon = CommercialAddon.Create(
            "ADDON-ATTENDANCE",
            "Attendance",
            null,
            CommercialAddonType.Massive,
            1.2m,
            20m,
            CommercialAddonPeriodicity.Monthly,
            CommercialAddonStatus.Draft);

        var tokenBefore = addon.ConcurrencyToken;

        addon.Update(
            "ADDON-ATTENDANCE",
            "Attendance Plus",
            "Updated",
            CommercialAddonType.Massive,
            1.5m,
            null,
            CommercialAddonPeriodicity.Annual);

        Assert.NotEqual(tokenBefore, addon.ConcurrencyToken);
        Assert.Equal("Attendance Plus", addon.Name);
        Assert.Equal("Updated", addon.Description);
        Assert.Equal(1.5m, addon.PricePerActiveEmployee);
        Assert.Null(addon.MinimumMonthlyFee);
        Assert.Equal(CommercialAddonPeriodicity.Annual, addon.Periodicity);
    }

    [Fact]
    public void Activate_AndInactivate_ShouldTransitionStatus_AndRefreshConcurrencyToken()
    {
        var addon = CommercialAddon.Create(
            "ADDON-ATTENDANCE",
            "Attendance",
            null,
            CommercialAddonType.Massive,
            1.2m,
            null,
            CommercialAddonPeriodicity.Monthly,
            CommercialAddonStatus.Draft);

        var tokenBeforeActivate = addon.ConcurrencyToken;
        addon.Activate();

        Assert.Equal(CommercialAddonStatus.Active, addon.Status);
        Assert.NotEqual(tokenBeforeActivate, addon.ConcurrencyToken);

        var tokenBeforeInactivate = addon.ConcurrencyToken;
        addon.Inactivate();

        Assert.Equal(CommercialAddonStatus.Inactive, addon.Status);
        Assert.NotEqual(tokenBeforeInactivate, addon.ConcurrencyToken);
    }
}
