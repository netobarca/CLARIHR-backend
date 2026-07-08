using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Pure, unit-testable vacation arithmetic (leave module §3.5) — no clock, no database, no side-effects. PR-7
/// (fund) needs three primitives: the period bounds (anniversary vs calendar year, leap-year safe), the
/// available days of a period (granted − consumed) and the Art. 177 eligibility (≥ 1 year of service).
/// <para>
/// The FIFO allocation suggestion (<c>SuggestFifoAllocations</c>) and the LIFO return suggestion
/// (<c>SuggestLifoReturn</c>) belong to the requests vertical (PR-8) and are intentionally NOT implemented
/// here — the fund does not need them.
/// </para>
/// </summary>
public static class VacationRules
{
    /// <summary>
    /// Derives the [start, end] bounds of a vacation period for one year. When
    /// <paramref name="useAnniversary"/> is false the period is the calendar year (Jan 1 → Dec 31). When true
    /// it runs from the plaza-start anniversary that falls in <paramref name="year"/> to the day before the next
    /// year's anniversary. A Feb-29 anniversary lands on Feb 28 in a non-leap year.
    /// </summary>
    public static (DateOnly Start, DateOnly End) PeriodBounds(int year, bool useAnniversary, DateOnly primaryPlazaStartDate)
    {
        if (!useAnniversary)
        {
            return (new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));
        }

        var start = AnniversaryInYear(primaryPlazaStartDate, year);
        var end = AnniversaryInYear(primaryPlazaStartDate, year + 1).AddDays(-1);
        return (start, end);
    }

    /// <summary>
    /// Available days of a period: the granted days (legal + benefit) minus the net days already consumed
    /// against it (approved-not-returned allocations), floored at zero. Each element of
    /// <paramref name="netConsumptions"/> is a net-consumed day count attributed to this period.
    /// </summary>
    public static int AvailableDays(PersonnelFileVacationPeriod period, IEnumerable<int> netConsumptions)
    {
        ArgumentNullException.ThrowIfNull(period);
        var consumed = netConsumptions?.Sum() ?? 0;
        return Math.Max(0, period.TotalDaysGranted - consumed);
    }

    /// <summary>
    /// Art. 177 eligibility: the employee must have completed at least one year of service by
    /// <paramref name="asOf"/> (i.e. <paramref name="asOf"/> is on or after the first anniversary of the hire /
    /// primary-plaza start). Feb-29 hire dates fold to Feb 28 via <see cref="DateOnly.AddYears"/>.
    /// </summary>
    public static bool IsEligible(DateOnly hireOrPlazaStart, DateOnly asOf) =>
        asOf >= hireOrPlazaStart.AddYears(1);

    private static DateOnly AnniversaryInYear(DateOnly start, int year)
    {
        var month = start.Month;
        var day = start.Day;
        if (month == 2 && day == 29 && !DateTime.IsLeapYear(year))
        {
            day = 28;
        }

        return new DateOnly(year, month, day);
    }
}
