namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// H-19 + H-20 — the pure rules that give an overtime record's time range meaning. Until now
/// <c>StartTime</c>/<c>EndTime</c> were persisted and echoed and nothing else consumed them: not one comparison,
/// threshold or exception. Meanwhile the engine pays <c>Σ(DurationDecimalHours × Factor)</c>, so a record could
/// declare 8 hours against a 10:00–11:00 range and be paid for eight while the authorizer approved one.
/// <para>
/// Reference country El Salvador. Labour Code <b>art. 161</b>: daytime work is the span between 06:00 and 19:00
/// of the same day; night work is between 19:00 of one day and 06:00 of the next. The seeded factors follow from
/// arts. 168/169: <c>HED</c> 2.00 (100% surcharge), <c>HEN</c> 2.50 (that plus the 25% night surcharge), and the
/// rest-day/holiday variants at double, <c>HEDF</c> 4.00 and <c>HENF</c> 5.00.
/// </para>
/// </summary>
public static class OvertimeScheduleRules
{
    /// <summary>Art. 161 — daytime starts here.</summary>
    public static readonly TimeOnly DaytimeStart = new(6, 0);

    /// <summary>Art. 161 — daytime ends here (exclusive); night runs from here to <see cref="DaytimeStart"/>.</summary>
    public static readonly TimeOnly NightStart = new(19, 0);

    public const string RangeEmptyCode = "OVERTIME_RANGE_EMPTY";

    /// <summary>
    /// Net duration of the range, as h:m. A range whose end is before its start crosses midnight — legitimate for
    /// overtime, and the same convention the work schedule already uses for a night shift. Equal times are
    /// rejected: a zero-length range is not a span of work, and it used to be accepted.
    /// </summary>
    public static OvertimeRangeDuration DeriveDurationFromRange(TimeOnly startTime, TimeOnly endTime)
    {
        if (startTime == endTime)
        {
            return OvertimeRangeDuration.Failure(RangeEmptyCode);
        }

        var minutes = MinutesBetween(startTime, endTime);
        return OvertimeRangeDuration.Success(minutes / 60, minutes % 60);
    }

    /// <summary>
    /// Whether the range collides with the shift the employee is contracted for that day. The shift is treated as
    /// one envelope from start to end: the meal break is NOT carved out of it, because its length varies by
    /// company (1 h, 2 h) and the employee may take it at any hour agreed with their supervisor — so the stored
    /// window is nominal and cannot be claimed as "outside the shift". Touching bounds do not overlap: overtime
    /// from 17:00 on a shift ending at 17:00 is legitimate.
    /// </summary>
    public static bool OverlapsShift(TimeOnly startTime, TimeOnly endTime, TimeOnly shiftStart, TimeOnly shiftEnd) =>
        OverlapsRange(startTime, endTime, shiftStart, shiftEnd);

    /// <summary>
    /// Whether two same-day ranges intersect. Both may cross midnight, so the comparison runs on minute offsets
    /// from each range's own start. Two records of 14:00–18:00 and 16:00–20:00 used to sum 8 h against the daily
    /// cap while the 2 overlapping hours were paid twice, invisibly.
    /// </summary>
    public static bool OverlapsRange(TimeOnly startTime, TimeOnly endTime, TimeOnly otherStart, TimeOnly otherEnd)
    {
        var (aStart, aEnd) = AbsoluteSpan(startTime, endTime);
        var (bStart, bEnd) = AbsoluteSpan(otherStart, otherEnd);

        // A midnight-crossing span is projected forward, so compare it against the other span shifted by a day
        // too: 02:00-04:00 must be seen inside a 22:00-06:00 shift.
        return Intersects(aStart, aEnd, bStart, bEnd)
            || Intersects(aStart, aEnd, bStart + MinutesPerDay, bEnd + MinutesPerDay)
            || Intersects(aStart + MinutesPerDay, aEnd + MinutesPerDay, bStart, bEnd);
    }

    /// <summary>
    /// Which legal band (art. 161) the range falls in, and whether it stays inside a single one. A range that
    /// crosses 19:00 or 06:00 spans BOTH bands, and a record carries ONE type: 18:00–21:00 is 1 h daytime plus
    /// 2 h night = 7.00 h-factor, which neither all-<c>HED</c> (6.00, short) nor all-<c>HEN</c> (7.50, over) can
    /// express. Those are rejected so the caller splits them, rather than auto-split — that keeps one request
    /// equal to one record in the tray and in the audit trail.
    /// </summary>
    public static OvertimeLegalBand ClassifyLegalBand(TimeOnly startTime, TimeOnly endTime)
    {
        var startsInDaytime = IsDaytime(startTime);

        // Walk the range minute-band by minute-band: the end is exclusive, so the last minute inside the range is
        // what decides, not the boundary the range stops at.
        var minutes = MinutesBetween(startTime, endTime);
        var lastMinute = startTime.AddMinutes(minutes - 1);

        return startsInDaytime == IsDaytime(lastMinute)
            ? OvertimeLegalBand.SingleBand(startsInDaytime)
            : OvertimeLegalBand.Split;
    }

    /// <summary>Whether an instant is daytime work under art. 161: <c>[06:00, 19:00)</c>.</summary>
    public static bool IsDaytime(TimeOnly time) => time >= DaytimeStart && time < NightStart;

    /// <summary>
    /// The overtime type the law prescribes for a band and a day character. Derived, not chosen: with factors from
    /// 2.00 to 5.00 a hand-picked type is an expensive mistake, and both inputs are objective — the clock decides
    /// the band, the holiday calendar (or the absence of a shift that day) decides the character.
    /// </summary>
    public static string DeriveTypeCode(bool isDaytime, bool isRestOrHoliday) => (isDaytime, isRestOrHoliday) switch
    {
        (true, false) => "HED",
        (false, false) => "HEN",
        (true, true) => "HEDF",
        (false, true) => "HENF",
    };

    private const int MinutesPerDay = 24 * 60;

    private static int MinutesBetween(TimeOnly startTime, TimeOnly endTime)
    {
        var minutes = (int)(endTime - startTime).TotalMinutes;
        return minutes > 0 ? minutes : minutes + MinutesPerDay;
    }

    private static (int Start, int End) AbsoluteSpan(TimeOnly startTime, TimeOnly endTime)
    {
        var start = (int)(startTime - TimeOnly.MinValue).TotalMinutes;
        return (start, start + MinutesBetween(startTime, endTime));
    }

    private static bool Intersects(int aStart, int aEnd, int bStart, int bEnd) => aStart < bEnd && bStart < aEnd;
}

/// <summary>H-20 — the h:m duration derived from a time range, or the reason the range is unusable.</summary>
public sealed record OvertimeRangeDuration(bool IsValid, int Hours, int Minutes, string? ErrorCode)
{
    public static OvertimeRangeDuration Success(int hours, int minutes) => new(true, hours, minutes, null);

    public static OvertimeRangeDuration Failure(string errorCode) => new(false, 0, 0, errorCode);
}

/// <summary>
/// H-20 — which legal band (art. 161) a range belongs to. <see cref="IsWithinOneBand"/> is false when the range
/// crosses 19:00 or 06:00, in which case <see cref="IsDaytime"/> is meaningless and the record is rejected.
/// </summary>
public sealed record OvertimeLegalBand(bool IsWithinOneBand, bool IsDaytime)
{
    public static readonly OvertimeLegalBand Split = new(false, false);

    public static OvertimeLegalBand SingleBand(bool isDaytime) => new(true, isDaytime);
}

/// <summary>
/// H-19/H-20 — the employee's contracted day for one date, resolved from a single assignment row.
/// <para>
/// <see cref="GeneratesOvertime"/> comes from the PLAZA, which is what disambiguates the two opposite meanings a
/// missing <c>workdayCode</c> used to have: a director deliberately without a shift, or a configuration gap
/// nobody noticed. The first blocks overtime; the second only warns.
/// </para>
/// <para>
/// <see cref="ShiftStart"/>/<see cref="ShiftEnd"/> are null when the schedule has no row for that weekday — a
/// free day, where every hour worked is overtime. That is what makes a custom schedule work without extra
/// modelling: 06:00-18:00 Monday-Thursday leaves Friday, Saturday and Sunday simply absent.
/// </para>
/// </summary>
public sealed record OvertimeScheduleContext(
    bool AssignmentFound,
    bool GeneratesOvertime,
    string? WorkdayCode,
    bool ScheduleFound,
    TimeOnly? ShiftStart,
    TimeOnly? ShiftEnd,
    bool IsHoliday)
{
    public static readonly OvertimeScheduleContext NotFound =
        new(false, true, null, false, null, null, false);

    /// <summary>True when the employee has no work schedule assigned at all — a configuration gap, not a decision.</summary>
    public bool HasNoSchedule => string.IsNullOrWhiteSpace(WorkdayCode) || !ScheduleFound;

    /// <summary>
    /// True when the date carries no contracted shift: a holiday, or a weekday absent from the schedule. Every
    /// hour is overtime, and the legal type is the rest-day variant (HEDF/HENF).
    /// </summary>
    public bool IsRestOrHoliday => IsHoliday || ShiftStart is null || ShiftEnd is null;
}

/// <summary>H-20 — one already-captured overtime range of the same file and date.</summary>
public sealed record OvertimeRecordRange(Guid RecordPublicId, TimeOnly? StartTime, TimeOnly? EndTime);
