using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>Company date-policy defaults for a vacation request (Art. 178, RN-27).</summary>
public readonly record struct VacationDateRulePreferences(
    bool AllowStartOnHoliday,
    bool AllowEndOnHoliday,
    bool AllowStartOnRestDay);

/// <summary>One fund period's availability (granted − net consumed) for the FIFO allocation suggestion.</summary>
public readonly record struct VacationPeriodAvailability(long PeriodId, int PeriodYear, int AvailableDays);

/// <summary>
/// One period's still-outstanding days (allocated − already returned) for the LIFO return suggestion. The list
/// is passed in allocation order (oldest allocation first); the rule reverses it internally (LIFO).
/// </summary>
public readonly record struct VacationPeriodOutstanding(long PeriodId, int OutstandingDays);

/// <summary>
/// Pure, unit-testable vacation arithmetic (leave module §3.5) — no clock, no database, no side-effects. PR-7
/// (fund) added the period bounds (anniversary vs calendar year, leap-year safe), the available days of a
/// period (granted − consumed) and the Art. 177 eligibility (≥ 1 year of service). PR-8 (requests) adds the
/// Art. 178 date validation (RN-27) and the FIFO/LIFO allocation/return suggestions.
/// </summary>
public static class VacationRules
{
    /// <summary>Art. 178 violation codes surfaced by <see cref="ValidateRequestDates"/> (422 on a request, warnings on the annual plan).</summary>
    public const string StartOnHolidayForbiddenCode = "VACATION_START_ON_HOLIDAY_FORBIDDEN";
    public const string StartOnRestDayForbiddenCode = "VACATION_START_ON_REST_DAY_FORBIDDEN";
    public const string EndOnHolidayForbiddenCode = "VACATION_END_ON_HOLIDAY_FORBIDDEN";

    /// <summary>
    /// Validates a vacation request's date range against the Art. 178 CT defaults (RN-27): a vacation cannot
    /// start on a company holiday (unless <see cref="VacationDateRulePreferences.AllowStartOnHoliday"/>), start on
    /// the employee's weekly rest day (unless <see cref="VacationDateRulePreferences.AllowStartOnRestDay"/>) nor
    /// end on a holiday (unless <see cref="VacationDateRulePreferences.AllowEndOnHoliday"/>). Returns the ordered
    /// list of violated codes (empty when valid). On a request each violation is a 422; on the annual plan (PR-9)
    /// they become non-blocking warnings.
    /// </summary>
    public static IReadOnlyList<string> ValidateRequestDates(
        DateOnly start,
        DateOnly end,
        IReadOnlySet<DateOnly> holidays,
        DayOfWeek restDay,
        VacationDateRulePreferences prefs)
    {
        ArgumentNullException.ThrowIfNull(holidays);

        var violations = new List<string>();
        if (!prefs.AllowStartOnHoliday && holidays.Contains(start))
        {
            violations.Add(StartOnHolidayForbiddenCode);
        }

        if (!prefs.AllowStartOnRestDay && start.DayOfWeek == restDay)
        {
            violations.Add(StartOnRestDayForbiddenCode);
        }

        if (!prefs.AllowEndOnHoliday && holidays.Contains(end))
        {
            violations.Add(EndOnHolidayForbiddenCode);
        }

        return violations;
    }

    /// <summary>
    /// Suggests the fund allocations for <paramref name="requestedDays"/> consuming the oldest period with a
    /// balance first (FIFO — RN by seniority of the fund). Editable by HR. When the total availability is smaller
    /// than the requested days the result sums to less than <paramref name="requestedDays"/> — the caller detects
    /// the insufficiency (<c>VACATION_FUND_INSUFFICIENT</c>).
    /// </summary>
    public static IReadOnlyList<VacationAllocationInput> SuggestFifoAllocations(
        IEnumerable<VacationPeriodAvailability> periods, int requestedDays)
    {
        ArgumentNullException.ThrowIfNull(periods);
        if (requestedDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedDays), "Requested days must be greater than zero.");
        }

        var result = new List<VacationAllocationInput>();
        var remaining = requestedDays;
        foreach (var period in periods.Where(period => period.AvailableDays > 0)
                     .OrderBy(period => period.PeriodYear)
                     .ThenBy(period => period.PeriodId))
        {
            if (remaining <= 0)
            {
                break;
            }

            var take = Math.Min(remaining, period.AvailableDays);
            result.Add(new VacationAllocationInput(period.PeriodId, take));
            remaining -= take;
        }

        return result;
    }

    /// <summary>
    /// Suggests the return distribution for <paramref name="daysToReturn"/> undoing the most recent allocation
    /// first (LIFO). <paramref name="outstandingInAllocationOrder"/> is the per-period outstanding (allocated −
    /// already returned) in allocation order; the rule walks it in reverse. Editable by HR. When the outstanding
    /// is smaller than the requested return the result sums to less than <paramref name="daysToReturn"/> — the
    /// caller detects it (<c>VACATION_RETURN_EXCEEDS_CONSUMED</c>).
    /// </summary>
    public static IReadOnlyList<VacationReturnDistributionInput> SuggestLifoReturn(
        IReadOnlyList<VacationPeriodOutstanding> outstandingInAllocationOrder, int daysToReturn)
    {
        ArgumentNullException.ThrowIfNull(outstandingInAllocationOrder);
        if (daysToReturn <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(daysToReturn), "Days to return must be greater than zero.");
        }

        var result = new List<VacationReturnDistributionInput>();
        var remaining = daysToReturn;
        for (var index = outstandingInAllocationOrder.Count - 1; index >= 0 && remaining > 0; index--)
        {
            var entry = outstandingInAllocationOrder[index];
            if (entry.OutstandingDays <= 0)
            {
                continue;
            }

            var take = Math.Min(remaining, entry.OutstandingDays);
            result.Add(new VacationReturnDistributionInput(entry.PeriodId, take));
            remaining -= take;
        }

        return result;
    }

    /// <summary>
    /// Derives the [start, end] bounds of a vacation period for one year. When
    /// <paramref name="useAnniversary"/> is false the period is the calendar year (Jan 1 → Dec 31). When true
    /// it runs from the HIRE anniversary that falls in <paramref name="year"/> to the day before the next year's
    /// anniversary. A Feb-29 anniversary lands on Feb 28 in a non-leap year.
    /// <para>
    /// H-28 — el aniversario es el del <b>ingreso a la empresa</b>, no el del registro de la plaza. Antes este
    /// parámetro se llamaba <c>primaryPlazaStartDate</c> y era literal: la ventana corría sobre la fecha en que la
    /// plaza se dio de alta en el sistema, así que una empresa que arrancaba hoy le ponía a todos su aniversario
    /// hoy.
    /// </para>
    /// </summary>
    public static (DateOnly Start, DateOnly End) PeriodBounds(int year, bool useAnniversary, DateOnly hireDate)
    {
        if (!useAnniversary)
        {
            return (new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));
        }

        var start = AnniversaryInYear(hireDate, year);
        var end = AnniversaryInYear(hireDate, year + 1).AddDays(-1);
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
    /// <paramref name="asOf"/> (i.e. <paramref name="asOf"/> is on or after the first anniversary of the hire
    /// date). Feb-29 hire dates fold to Feb 28 via <see cref="DateOnly.AddYears"/>.
    /// <para>
    /// H-28 — la antigüedad es la de la <b>relación laboral</b>, que es lo que mide <c>hireDate</c>; el parámetro
    /// se llamaba <c>hireOrPlazaStart</c> («el ingreso <i>o</i> el inicio de plaza») y ese «o» era el defecto: el
    /// resolvedor del ancla siempre prefería la plaza.
    /// </para>
    /// </summary>
    public static bool IsEligible(DateOnly hireDate, DateOnly asOf) =>
        asOf >= hireDate.AddYears(1);

    /// <summary>
    /// Art. 177 eligibility for a whole period: the first anniversary must fall on or before the period's END, not
    /// its start. Único punto de decisión de la elegibilidad — los dos caminos (alta individual y generación
    /// masiva) llaman acá.
    /// <para>
    /// H-28 — en modo <b>aniversario</b> (el default) da la misma respuesta que medir contra el inicio, porque la
    /// ventana arranca justo en el aniversario. La diferencia aparece en modo <b>año calendario</b>: quien ingresó
    /// el 2026-01-16 cumple su año el 2027-01-16, o sea dentro del periodo calendario 2027, y tiene derecho a ese
    /// fondo. Midiendo contra el inicio (<c>2027-01-01</c>) se le negaba y su primer fondo salía en 2028 — un año
    /// tarde, sin que nada lo dijera.
    /// </para>
    /// </summary>
    public static bool IsEligibleForPeriod(DateOnly hireDate, (DateOnly Start, DateOnly End) bounds) =>
        IsEligible(hireDate, bounds.End);

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
