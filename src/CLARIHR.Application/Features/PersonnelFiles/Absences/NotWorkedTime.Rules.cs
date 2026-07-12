using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles.Absences;

public static class NotWorkedTimeStatuses
{
    /// <summary>The only birth status: there is NO decision step (P-16). The absence already happened — recording it
    /// is a statement of fact, not a request. Same reasoning as an incapacity.</summary>
    public const string Registrado = "REGISTRADO";

    public const string Anulado = "ANULADO";
}

/// <summary>How the record entered the system. <c>MARCACION</c> is the seam for the future biometric module
/// (P-21): nothing reads it today, and nothing should be invented around it.</summary>
public static class NotWorkedTimeOrigins
{
    public const string Manual = "MANUAL";
    public const string Marcacion = "MARCACION";
}

public static class NotWorkedTimeErrors
{
    public static readonly Error TypeInvalid = new(
        "NOT_WORKED_TIME_TYPE_INVALID",
        "The not-worked-time type does not exist or is not active.",
        ErrorType.UnprocessableEntity);

    public static readonly Error RangeInvalid = new(
        "NOT_WORKED_TIME_RANGE_INVALID",
        "The end date cannot be earlier than the start date.",
        ErrorType.UnprocessableEntity);

    /// <summary>A type captured in hours needs the hours; one captured in days must not carry them.</summary>
    public static readonly Error HoursRequired = new(
        "NOT_WORKED_TIME_HOURS_REQUIRED",
        "This not-worked-time type is captured in hours, so the hours are mandatory.",
        ErrorType.UnprocessableEntity);

    public static readonly Error HoursNotApplicable = new(
        "NOT_WORKED_TIME_HOURS_NOT_APPLICABLE",
        "This not-worked-time type is captured in days, so hours cannot be provided.",
        ErrorType.UnprocessableEntity);

    public static readonly Error AlreadyAnnulled = new(
        "NOT_WORKED_TIME_ALREADY_ANNULLED",
        "The not-worked-time record is already annulled.",
        ErrorType.UnprocessableEntity);
}

/// <summary>One computed day of the scan — the audit trail of "why did we discount 6 days for a 5-day absence".</summary>
public sealed record NotWorkedTimeDayDetail(DateOnly Date, bool IsComputable, string Reason);

/// <summary>Everything the engine needs, resolved beforehand. Pure: no I/O, no clock.</summary>
public sealed record NotWorkedTimeCalculationInput(
    DateOnly StartDate,
    DateOnly EndDate,
    bool CountsHoliday,
    bool CountsSaturday,
    bool CountsRestDay,
    bool CountsSeventhDayPenalty,
    bool UsesWorkSchedule,
    decimal? Hours,
    decimal DiscountPercent,
    IReadOnlySet<DateOnly> Holidays,
    DayOfWeek RestDay,
    decimal MonthlyBaseSalary,
    decimal StandardDailyHours);

public sealed record NotWorkedTimeCalculationResult(
    int CalendarDays,
    int ComputableDays,
    int SeventhDayPenaltyDays,
    decimal DiscountedDays,
    decimal DailySalary,
    decimal DiscountAmount,
    IReadOnlyList<NotWorkedTimeDayDetail> Details);

/// <summary>
/// The not-worked-time engine (REQ-011). The day scan is deliberately the SAME as the incapacity engine's
/// (<c>IncapacityCalculationRules.IsExcluded</c>): two modules that counted the same calendar differently would be a
/// bug waiting to happen.
///
/// <para>The one rule with no precedent is the <b>seventh day</b> (P-18): each affected week costs the employee one
/// extra full day — the paid day of rest they forfeit. The incapacity engine excludes or includes the rest day, but
/// it never ADDS one.</para>
/// </summary>
public static class NotWorkedTimeRules
{
    /// <summary>The daily-salary divisor of the whole product (`salario / 30`) — same as incapacities, vacations and
    /// settlements. Changing it here alone would make this module disagree with all of them.</summary>
    private const decimal MonthDivisorDays = 30m;

    public const decimal DefaultStandardDailyHours = 8m;

    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>The exclusion predicate — a literal copy of the incapacity engine's. Note the rest day is
    /// <paramref name="restDay"/>, NOT a hardcoded Sunday: it is resolved plaza → company → Sunday upstream.</summary>
    public static bool IsExcluded(
        DateOnly day,
        bool countsRestDay,
        bool countsSaturday,
        bool countsHoliday,
        DayOfWeek restDay,
        IReadOnlySet<DateOnly> holidays)
    {
        if (!countsRestDay && day.DayOfWeek == restDay)
        {
            return true;
        }

        if (!countsSaturday && day.DayOfWeek == DayOfWeek.Saturday)
        {
            return true;
        }

        return !countsHoliday && holidays.Contains(day);
    }

    public static NotWorkedTimeCalculationResult Calculate(NotWorkedTimeCalculationInput input)
    {
        var calendarDays = input.EndDate.DayNumber - input.StartDate.DayNumber + 1;

        // [1] The scan. An excluded day is not counted and not discounted; the detail says why.
        var details = new List<NotWorkedTimeDayDetail>(calendarDays);
        var computableDays = 0;
        var affectedWeeks = new HashSet<int>();

        for (var day = input.StartDate; day <= input.EndDate; day = day.AddDays(1))
        {
            var excluded = IsExcluded(
                day, input.CountsRestDay, input.CountsSaturday, input.CountsHoliday, input.RestDay, input.Holidays);

            details.Add(new NotWorkedTimeDayDetail(day, !excluded, excluded ? ReasonFor(day, input) : "COMPUTABLE"));

            if (excluded)
            {
                continue;
            }

            computableDays++;

            // The ISO week the day belongs to. A week with at least one computable day is an AFFECTED week.
            affectedWeeks.Add(WeekKey(day));
        }

        // [2] THE new rule (P-18): one extra full day per affected week — the paid rest the employee forfeits.
        var seventhDayPenaltyDays = input.CountsSeventhDayPenalty ? affectedWeeks.Count : 0;

        // [3] The unit of the discount. In hours mode the range is the day the employee arrived late (or left
        // early): the hours are converted with the company's standard day. Two late hours of an eight-hour day are
        // a QUARTER of a day, not a day — valuing them as a whole day would be a punishment, not a discount.
        var discountedDays = input.UsesWorkSchedule
            ? HoursToDays(input.Hours ?? 0m, input.StandardDailyHours)
            : computableDays + seventhDayPenaltyDays;

        // [4] The daily salary is rounded ONCE, here; every amount derives from that rounded figure (same as the
        // incapacity engine — rounding at the end yields different cents).
        var dailySalary = input.MonthlyBaseSalary > 0m
            ? Round2(input.MonthlyBaseSalary / MonthDivisorDays)
            : 0m;

        // [5] A 0% type is the "con goce": the absence is recorded, the money is not touched.
        var discountAmount = Round2(discountedDays * dailySalary * input.DiscountPercent / 100m);

        return new NotWorkedTimeCalculationResult(
            calendarDays,
            computableDays,
            seventhDayPenaltyDays,
            discountedDays,
            dailySalary,
            discountAmount,
            details);
    }

    /// <summary>Hours → days, with the company's standard working day (null upstream ⇒ 8).</summary>
    public static decimal HoursToDays(decimal hours, decimal standardDailyHours) =>
        standardDailyHours <= 0m ? 0m : Round2(hours / standardDailyHours);

    /// <summary>
    /// The calendar week a day belongs to (Monday–Sunday), as a monotonic key: two days of the same week must
    /// collapse to ONE key, or a plain Monday-to-Friday absence would be charged two seventh days.
    /// <para><c>DateOnly.DayNumber</c> 0 is 0001-01-01, a Monday, so an integer division by 7 lands every Monday on
    /// a new key.</para>
    /// </summary>
    private static int WeekKey(DateOnly day) => day.DayNumber / 7;

    private static string ReasonFor(DateOnly day, NotWorkedTimeCalculationInput input)
    {
        if (!input.CountsRestDay && day.DayOfWeek == input.RestDay)
        {
            return "DIA_DESCANSO";
        }

        return !input.CountsSaturday && day.DayOfWeek == DayOfWeek.Saturday ? "SABADO" : "ASUETO";
    }
}

/// <summary>
/// Everything the not-worked-time engine needs about ONE employee, in a single round trip: the monthly base salary
/// of the plaza, the rest day (plaza → company preference → Sunday), the company's holidays inside the range, and
/// the standard working day (null upstream ⇒ 8).
/// </summary>
public sealed record NotWorkedTimeContextData(
    bool PlazaFound,
    Guid AssignedPositionPublicId,
    decimal MonthlyBaseSalary,
    DayOfWeek RestDay,
    IReadOnlySet<DateOnly> Holidays,
    decimal StandardDailyHours,
    string CurrencyCode);
