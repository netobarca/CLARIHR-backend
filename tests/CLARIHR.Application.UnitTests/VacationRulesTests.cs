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

    // ── Art. 178 date rules (RN-27) ──────────────────────────────────────────────────────────────

    private static readonly VacationDateRulePreferences LegalDefaults =
        new(AllowStartOnHoliday: false, AllowEndOnHoliday: true, AllowStartOnRestDay: false);

    [Fact]
    public void ValidateRequestDates_UnderLegalDefaults_ValidRangeHasNoViolations()
    {
        // 2026-03-17 (Tue) → 2026-03-19 (Thu): not a holiday, not the Sunday rest day.
        var violations = VacationRules.ValidateRequestDates(
            new DateOnly(2026, 3, 17), new DateOnly(2026, 3, 19), new HashSet<DateOnly>(), DayOfWeek.Sunday, LegalDefaults);

        Assert.Empty(violations);
    }

    [Fact]
    public void ValidateRequestDates_StartOnHoliday_IsForbiddenByDefault()
    {
        var holidays = new HashSet<DateOnly> { new(2026, 3, 16) };

        var violations = VacationRules.ValidateRequestDates(
            new DateOnly(2026, 3, 16), new DateOnly(2026, 3, 18), holidays, DayOfWeek.Sunday, LegalDefaults);

        Assert.Contains(VacationRules.StartOnHolidayForbiddenCode, violations);
    }

    [Fact]
    public void ValidateRequestDates_StartOnRestDay_IsForbiddenByDefault()
    {
        // 2026-03-01 is a Sunday.
        var violations = VacationRules.ValidateRequestDates(
            new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 3), new HashSet<DateOnly>(), DayOfWeek.Sunday, LegalDefaults);

        Assert.Contains(VacationRules.StartOnRestDayForbiddenCode, violations);
    }

    [Fact]
    public void ValidateRequestDates_EndOnHoliday_IsAllowedByDefaultButForbiddenWhenDisallowed()
    {
        var holidays = new HashSet<DateOnly> { new(2026, 3, 19) };

        // Default: end on holiday is allowed (Art. 178).
        Assert.DoesNotContain(
            VacationRules.EndOnHolidayForbiddenCode,
            VacationRules.ValidateRequestDates(new DateOnly(2026, 3, 17), new DateOnly(2026, 3, 19), holidays, DayOfWeek.Sunday, LegalDefaults));

        // When the company disallows it, ending on a holiday is a violation.
        var strict = new VacationDateRulePreferences(AllowStartOnHoliday: false, AllowEndOnHoliday: false, AllowStartOnRestDay: false);
        Assert.Contains(
            VacationRules.EndOnHolidayForbiddenCode,
            VacationRules.ValidateRequestDates(new DateOnly(2026, 3, 17), new DateOnly(2026, 3, 19), holidays, DayOfWeek.Sunday, strict));
    }

    [Fact]
    public void ValidateRequestDates_AllowancesSuppressTheViolations()
    {
        var holidays = new HashSet<DateOnly> { new(2026, 3, 1) };
        var permissive = new VacationDateRulePreferences(AllowStartOnHoliday: true, AllowEndOnHoliday: true, AllowStartOnRestDay: true);

        // 2026-03-01 is both a Sunday and a holiday, yet all three allowances are on.
        var violations = VacationRules.ValidateRequestDates(
            new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 3), holidays, DayOfWeek.Sunday, permissive);

        Assert.Empty(violations);
    }

    // ── FIFO allocation suggestion ───────────────────────────────────────────────────────────────

    [Fact]
    public void SuggestFifoAllocations_ConsumesTheOldestPeriodWithBalanceFirst_AcrossTwoPeriods()
    {
        var periods = new[]
        {
            new VacationPeriodAvailability(PeriodId: 20, PeriodYear: 2026, AvailableDays: 15),
            new VacationPeriodAvailability(PeriodId: 10, PeriodYear: 2025, AvailableDays: 15),
        };

        var allocations = VacationRules.SuggestFifoAllocations(periods, requestedDays: 20);

        // Oldest first: 15 from 2025 (id 10), then 5 from 2026 (id 20).
        Assert.Equal(2, allocations.Count);
        Assert.Equal(10, allocations[0].VacationPeriodId);
        Assert.Equal(15, allocations[0].Days);
        Assert.Equal(20, allocations[1].VacationPeriodId);
        Assert.Equal(5, allocations[1].Days);
        Assert.Equal(20, allocations.Sum(allocation => allocation.Days));
    }

    [Fact]
    public void SuggestFifoAllocations_SkipsEmptyPeriodsAndStopsWhenSatisfied()
    {
        var periods = new[]
        {
            new VacationPeriodAvailability(PeriodId: 1, PeriodYear: 2024, AvailableDays: 0),
            new VacationPeriodAvailability(PeriodId: 2, PeriodYear: 2025, AvailableDays: 10),
            new VacationPeriodAvailability(PeriodId: 3, PeriodYear: 2026, AvailableDays: 10),
        };

        var allocations = VacationRules.SuggestFifoAllocations(periods, requestedDays: 4);

        var single = Assert.Single(allocations);
        Assert.Equal(2, single.VacationPeriodId);
        Assert.Equal(4, single.Days);
    }

    [Fact]
    public void SuggestFifoAllocations_WhenFundInsufficient_ReturnsLessThanRequested()
    {
        var periods = new[] { new VacationPeriodAvailability(PeriodId: 1, PeriodYear: 2026, AvailableDays: 5) };

        var allocations = VacationRules.SuggestFifoAllocations(periods, requestedDays: 12);

        Assert.Equal(5, allocations.Sum(allocation => allocation.Days));
    }

    // ── LIFO return suggestion ───────────────────────────────────────────────────────────────────

    [Fact]
    public void SuggestLifoReturn_UndoesTheMostRecentAllocationFirst()
    {
        // Allocation order (oldest first): period 10 with 15 outstanding, then period 20 with 5 outstanding.
        var outstanding = new[]
        {
            new VacationPeriodOutstanding(PeriodId: 10, OutstandingDays: 15),
            new VacationPeriodOutstanding(PeriodId: 20, OutstandingDays: 5),
        };

        // Return 4 → all from the last allocation (period 20).
        var distribution = VacationRules.SuggestLifoReturn(outstanding, daysToReturn: 4);
        var single = Assert.Single(distribution);
        Assert.Equal(20, single.VacationPeriodId);
        Assert.Equal(4, single.Days);
    }

    [Fact]
    public void SuggestLifoReturn_SpillsIntoEarlierAllocationsWhenNeeded()
    {
        var outstanding = new[]
        {
            new VacationPeriodOutstanding(PeriodId: 10, OutstandingDays: 15),
            new VacationPeriodOutstanding(PeriodId: 20, OutstandingDays: 1),
        };

        // Return 16 → 1 from period 20 (newest), then 15 from period 10.
        var distribution = VacationRules.SuggestLifoReturn(outstanding, daysToReturn: 16);

        Assert.Equal(2, distribution.Count);
        Assert.Equal(20, distribution[0].VacationPeriodId);
        Assert.Equal(1, distribution[0].Days);
        Assert.Equal(10, distribution[1].VacationPeriodId);
        Assert.Equal(15, distribution[1].Days);
        Assert.Equal(16, distribution.Sum(entry => entry.Days));
    }

    [Fact]
    public void SuggestLifoReturn_WhenOutstandingIsExhausted_ReturnsLessThanRequested()
    {
        var outstanding = new[] { new VacationPeriodOutstanding(PeriodId: 10, OutstandingDays: 3) };

        var distribution = VacationRules.SuggestLifoReturn(outstanding, daysToReturn: 8);

        Assert.Equal(3, distribution.Sum(entry => entry.Days));
    }
}
