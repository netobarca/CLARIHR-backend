using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Pure vacation rules (PR-7 fund primitives): period bounds (anniversary vs calendar year, leap-year safe),
/// available days (granted − consumed) and Art. 177 eligibility (≥ 1 year of service).
/// </summary>
public sealed class VacationRulesTests
{
    [Fact]
    public void PeriodBounds_CalendarYear_SpansJanToDec()
    {
        var (start, end) = VacationRules.PeriodBounds(2026, useAnniversary: false, new DateOnly(2020, 3, 15));

        Assert.Equal(new DateOnly(2026, 1, 1), start);
        Assert.Equal(new DateOnly(2026, 12, 31), end);
    }

    [Fact]
    public void PeriodBounds_Anniversary_RunsFromAnniversaryToDayBeforeNext()
    {
        var (start, end) = VacationRules.PeriodBounds(2026, useAnniversary: true, new DateOnly(2020, 3, 15));

        Assert.Equal(new DateOnly(2026, 3, 15), start);
        Assert.Equal(new DateOnly(2027, 3, 14), end);
    }

    [Fact]
    public void PeriodBounds_AnniversaryFeb29_InNonLeapYear_FoldsToFeb28()
    {
        // 2027 is not a leap year; 2028 is.
        var (start, end) = VacationRules.PeriodBounds(2027, useAnniversary: true, new DateOnly(2020, 2, 29));

        Assert.Equal(new DateOnly(2027, 2, 28), start);
        Assert.Equal(new DateOnly(2028, 2, 28), end);
    }

    [Fact]
    public void PeriodBounds_AnniversaryFeb29_InLeapYear_KeepsFeb29()
    {
        var (start, end) = VacationRules.PeriodBounds(2028, useAnniversary: true, new DateOnly(2020, 2, 29));

        // Start keeps Feb 29; the end is the day before the 2029 anniversary, which folds to Feb 28 (non-leap),
        // so the period ends 2029-02-27.
        Assert.Equal(new DateOnly(2028, 2, 29), start);
        Assert.Equal(new DateOnly(2029, 2, 27), end);
    }

    [Fact]
    public void AvailableDays_IsGrantedMinusConsumed_FlooredAtZero()
    {
        var period = PersonnelFileVacationPeriod.Create(
            2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
            legalDaysGranted: 15, benefitDaysGranted: 5, generatesEnjoymentDays: true,
            usedAnniversary: false, VacationPeriodSources.Manual);

        Assert.Equal(20, VacationRules.AvailableDays(period, []));
        Assert.Equal(16, VacationRules.AvailableDays(period, [4]));
        Assert.Equal(11, VacationRules.AvailableDays(period, [4, 5]));
        Assert.Equal(0, VacationRules.AvailableDays(period, [25]));
    }

    [Fact]
    public void IsEligible_RequiresAtLeastOneYearOfService()
    {
        var hire = new DateOnly(2025, 6, 1);

        Assert.False(VacationRules.IsEligible(hire, new DateOnly(2026, 5, 31)));
        Assert.True(VacationRules.IsEligible(hire, new DateOnly(2026, 6, 1)));
        Assert.True(VacationRules.IsEligible(hire, new DateOnly(2027, 1, 1)));
    }

    [Fact]
    public void IsEligible_Feb29Hire_FoldsToFeb28FirstAnniversary()
    {
        var hire = new DateOnly(2024, 2, 29);

        // 2025 is not a leap year → first anniversary lands on Feb 28.
        Assert.False(VacationRules.IsEligible(hire, new DateOnly(2025, 2, 27)));
        Assert.True(VacationRules.IsEligible(hire, new DateOnly(2025, 2, 28)));
    }
}
