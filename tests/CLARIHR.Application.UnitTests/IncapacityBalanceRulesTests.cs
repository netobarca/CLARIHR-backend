using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// The pure employer-cap balance rule (D-27/§3.10). The SAME rule feeds the incapacity-balance endpoint and
/// the profile's <c>DisabilityDaysAvailable</c>, so these assertions pin the identity that keeps the two
/// figures cuadrando by construction.
/// </summary>
public sealed class IncapacityBalanceRulesTests
{
    [Fact]
    public void Compute_WithNullPreferences_UsesLegalDefaults()
    {
        var balance = IncapacityBalanceRules.Compute(employerCoveredDays: null, additionalBenefitDays: null, consumedEmployerDays: 0);

        Assert.Equal(9, balance.EmployerCoveredDays);
        Assert.Equal(0, balance.AdditionalBenefitDays);
        Assert.Equal(9, balance.TotalCapDays);
        Assert.Equal(0, balance.ConsumedEmployerDays);
        Assert.Equal(9, balance.RemainingDays);
    }

    [Theory]
    [InlineData(9, 0, 0, 9)]
    [InlineData(9, 0, 3, 6)]
    [InlineData(9, 0, 9, 0)]
    [InlineData(9, 3, 6, 6)]
    public void Compute_RemainingIsCapMinusConsumed(int covered, int benefit, int consumed, int expectedRemaining)
    {
        var balance = IncapacityBalanceRules.Compute(covered, benefit, consumed);

        Assert.Equal(covered + benefit, balance.TotalCapDays);
        Assert.Equal(expectedRemaining, balance.RemainingDays);
    }

    [Fact]
    public void Compute_WhenConsumedExceedsCap_FloorsRemainingAtZero()
    {
        var balance = IncapacityBalanceRules.Compute(employerCoveredDays: 9, additionalBenefitDays: 0, consumedEmployerDays: 14);

        Assert.Equal(0, balance.RemainingDays);
        Assert.Equal(14, balance.ConsumedEmployerDays);
    }

    [Fact]
    public void Compute_WhenConsumedNegative_FloorsConsumedAtZero()
    {
        var balance = IncapacityBalanceRules.Compute(employerCoveredDays: 9, additionalBenefitDays: 0, consumedEmployerDays: -5);

        Assert.Equal(0, balance.ConsumedEmployerDays);
        Assert.Equal(9, balance.RemainingDays);
    }
}
