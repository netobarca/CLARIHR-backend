using CLARIHR.Domain.Companies;

namespace CLARIHR.Application.UnitTests;

public sealed class CommercialAddonDomainTests
{
    [Fact]
    public void Create_Massive_ShouldNormalizeCodeNameAndReservedPricingUnit()
    {
        var addon = CommercialAddon.Create(
            " addon-attendance ",
            " Attendance ",
            " Time tracking ",
            CommercialAddonType.Massive,
            CommercialAddonBillingModel.PerActiveEmployee,
            " ACTIVE EMPLOYEE ",
            1.2m,
            null,
            35m,
            CommercialAddonPeriodicity.Monthly,
            CommercialAddonStatus.Draft);

        Assert.Equal("ADDON-ATTENDANCE", addon.Code);
        Assert.Equal("ADDON-ATTENDANCE", addon.NormalizedCode);
        Assert.Equal("Attendance", addon.Name);
        Assert.Equal("ATTENDANCE", addon.NormalizedName);
        Assert.Equal("Time tracking", addon.Description);
        Assert.Equal(CommercialAddonBillingModel.PerActiveEmployee, addon.BillingModel);
        Assert.Equal(CommercialAddon.MassiveMeasurementUnit, addon.MeasurementUnit);
        Assert.Equal(1.2m, addon.UnitPrice);
        Assert.Null(addon.MinimumQuantity);
    }

    [Fact]
    public void Create_Massive_ShouldAllowNullMinimumMonthlyFee()
    {
        var addon = CommercialAddon.Create(
            "ADDON-PAYROLL-ES",
            "Payroll ES",
            null,
            CommercialAddonType.Massive,
            CommercialAddonBillingModel.PerActiveEmployee,
            CommercialAddon.MassiveMeasurementUnit,
            2.5m,
            null,
            null,
            CommercialAddonPeriodicity.Annual,
            CommercialAddonStatus.Draft);

        Assert.Null(addon.MinimumMonthlyFee);
        Assert.Equal(CommercialAddonPeriodicity.Annual, addon.Periodicity);
    }

    [Fact]
    public void Create_SpecializedPerSeat_ShouldAllowMinimumQuantity()
    {
        var addon = CommercialAddon.Create(
            "ADDON-RECRUITING",
            "Recruiting ATS",
            "Recruiting seat addon",
            CommercialAddonType.Specialized,
            CommercialAddonBillingModel.PerSeat,
            " recruiter seat ",
            9.5m,
            2,
            null,
            CommercialAddonPeriodicity.Monthly,
            CommercialAddonStatus.Draft);

        Assert.Equal(CommercialAddonType.Specialized, addon.Type);
        Assert.Equal(CommercialAddonBillingModel.PerSeat, addon.BillingModel);
        Assert.Equal("recruiter seat", addon.MeasurementUnit);
        Assert.Equal(9.5m, addon.UnitPrice);
        Assert.Equal(2, addon.MinimumQuantity);
        Assert.Null(addon.MinimumMonthlyFee);
    }

    [Fact]
    public void Create_ShouldRejectNegativeAmountsAndQuantities()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CommercialAddon.Create(
                "ADDON-ATTENDANCE",
                "Attendance",
                null,
                CommercialAddonType.Massive,
                CommercialAddonBillingModel.PerActiveEmployee,
                CommercialAddon.MassiveMeasurementUnit,
                -1m,
                null,
                null,
                CommercialAddonPeriodicity.Monthly,
                CommercialAddonStatus.Draft));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CommercialAddon.Create(
                "ADDON-ATTENDANCE",
                "Attendance",
                null,
                CommercialAddonType.Massive,
                CommercialAddonBillingModel.PerActiveEmployee,
                CommercialAddon.MassiveMeasurementUnit,
                1m,
                null,
                -10m,
                CommercialAddonPeriodicity.Monthly,
                CommercialAddonStatus.Draft));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CommercialAddon.Create(
                "ADDON-RECRUITING",
                "Recruiting ATS",
                null,
                CommercialAddonType.Specialized,
                CommercialAddonBillingModel.PerSeat,
                "recruiter seat",
                10m,
                -1,
                null,
                CommercialAddonPeriodicity.Monthly,
                CommercialAddonStatus.Draft));
    }

    [Fact]
    public void Create_ShouldRejectIncoherentPricingConfiguration()
    {
        Assert.Throws<ArgumentException>(() =>
            CommercialAddon.Create(
                "ADDON-RECRUITING",
                "Recruiting ATS",
                null,
                CommercialAddonType.Specialized,
                CommercialAddonBillingModel.PerSeat,
                CommercialAddon.MassiveMeasurementUnit,
                10m,
                1,
                null,
                CommercialAddonPeriodicity.Monthly,
                CommercialAddonStatus.Draft));

        Assert.Throws<ArgumentException>(() =>
            CommercialAddon.Create(
                "ADDON-API-PACK",
                "API Pack",
                null,
                CommercialAddonType.Specialized,
                CommercialAddonBillingModel.PerVolume,
                "api seat",
                10m,
                100,
                null,
                CommercialAddonPeriodicity.Monthly,
                CommercialAddonStatus.Draft));

        Assert.Throws<ArgumentException>(() =>
            CommercialAddon.Create(
                "ADDON-ATTENDANCE",
                "Attendance",
                null,
                CommercialAddonType.Massive,
                CommercialAddonBillingModel.PerSeat,
                "recruiter seat",
                10m,
                null,
                20m,
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
            CommercialAddonBillingModel.PerActiveEmployee,
            CommercialAddon.MassiveMeasurementUnit,
            1.2m,
            null,
            20m,
            CommercialAddonPeriodicity.Monthly,
            CommercialAddonStatus.Draft);

        var tokenBefore = addon.ConcurrencyToken;

        addon.Update(
            "ADDON-ATTENDANCE",
            "Attendance Plus",
            "Updated",
            CommercialAddonType.Specialized,
            CommercialAddonBillingModel.PerVolume,
            "document",
            1.5m,
            100,
            null,
            CommercialAddonPeriodicity.Annual);

        Assert.NotEqual(tokenBefore, addon.ConcurrencyToken);
        Assert.Equal("Attendance Plus", addon.Name);
        Assert.Equal("Updated", addon.Description);
        Assert.Equal(CommercialAddonType.Specialized, addon.Type);
        Assert.Equal(CommercialAddonBillingModel.PerVolume, addon.BillingModel);
        Assert.Equal("document", addon.MeasurementUnit);
        Assert.Equal(1.5m, addon.UnitPrice);
        Assert.Equal(100, addon.MinimumQuantity);
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
            CommercialAddonBillingModel.PerActiveEmployee,
            CommercialAddon.MassiveMeasurementUnit,
            1.2m,
            null,
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
