using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Payroll;

/// <summary>Attendance date anchor of a work schedule: which side of the shift owns the calendar date.</summary>
public static class WorkScheduleAnchors
{
    public const string Entrada = "ENTRADA";
    public const string Salida = "SALIDA";

    public static readonly IReadOnlyCollection<string> All = new[] { Entrada, Salida };
}

/// <summary>Classification of a work schedule (REQ-012 D-06).</summary>
public static class WorkScheduleClasses
{
    public const string Ordinaria = "ORDINARIA";
    public const string Extraordinaria = "EXTRAORDINARIA";

    public static readonly IReadOnlyCollection<string> All = new[] { Ordinaria, Extraordinaria };
}

/// <summary>Input row for <see cref="WorkSchedule.ReplaceDays"/> (one weekday of the schedule).</summary>
public sealed record WorkScheduleDayInput(
    int DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    TimeOnly? MealStart,
    TimeOnly? MealEnd);

/// <summary>
/// Company-managed master of work schedules ("jornadas laborales", REQ-012 D-06 — net-new: the plaza's
/// <c>WorkdayCode</c> was free text until M3). The parent carries the identity (code, name, label), the
/// attendance date anchor (<see cref="WorkScheduleAnchors"/> — which side of a midnight-crossing shift owns
/// the calendar date), the classification and the weekly hours (derived from the days, editable); the child
/// collection holds one row per weekday with shift times, optional meal break and derived net hours. The
/// legal-week template (44 h — golden 11) ships via <c>WorkScheduleTemplateSeeder</c>, idempotent by
/// normalized code. Mirrors the governed masters: filtered unique <c>(tenant, normalized_code) WHERE
/// is_active</c>, logical activate/inactivate (an inactive schedule accepts no new plaza references).
/// </summary>
public sealed class WorkSchedule : TenantEntity
{
    public const int MaxCodeLength = 80;
    public const int MaxNameLength = 200;
    public const int MaxScheduleLabelLength = 200;
    public const decimal MaxWeeklyHours = 168m;

    private readonly List<WorkScheduleDay> _days = [];

    private WorkSchedule()
    {
    }

    private WorkSchedule(
        Guid publicId,
        string code,
        string name,
        string? scheduleLabel,
        string attendanceDateAnchor,
        string scheduleClass,
        decimal? totalWeeklyHours,
        IReadOnlyCollection<WorkScheduleDayInput> days)
    {
        PublicId = publicId;
        SetCode(code);
        SetName(name);
        SetScheduleLabel(scheduleLabel);
        SetAttendanceDateAnchor(attendanceDateAnchor);
        SetScheduleClass(scheduleClass);
        ReplaceDaysCore(days);
        SetTotalWeeklyHours(totalWeeklyHours);
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>Free descriptive label of the schedule (e.g. «L-V 08:00-17:00 · sáb 08:00-12:00»).</summary>
    public string? ScheduleLabel { get; private set; }

    /// <summary>Which side of the shift owns the calendar date (<see cref="WorkScheduleAnchors"/>).</summary>
    public string AttendanceDateAnchor { get; private set; } = WorkScheduleAnchors.Entrada;

    /// <summary>ORDINARIA | EXTRAORDINARIA (<see cref="WorkScheduleClasses"/>).</summary>
    public string ScheduleClass { get; private set; } = WorkScheduleClasses.Ordinaria;

    /// <summary>Weekly hours — derived from the days when not supplied, editable (D-06).</summary>
    public decimal TotalWeeklyHours { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public IReadOnlyCollection<WorkScheduleDay> Days => _days.AsReadOnly();

    public static WorkSchedule Create(
        string code,
        string name,
        string? scheduleLabel,
        string attendanceDateAnchor,
        string scheduleClass,
        decimal? totalWeeklyHours,
        IReadOnlyCollection<WorkScheduleDayInput> days) =>
        new(Guid.NewGuid(), code, name, scheduleLabel, attendanceDateAnchor, scheduleClass, totalWeeklyHours, days);

    public void Update(
        string code,
        string name,
        string? scheduleLabel,
        string attendanceDateAnchor,
        string scheduleClass,
        decimal? totalWeeklyHours,
        IReadOnlyCollection<WorkScheduleDayInput> days)
    {
        SetCode(code);
        SetName(name);
        SetScheduleLabel(scheduleLabel);
        SetAttendanceDateAnchor(attendanceDateAnchor);
        SetScheduleClass(scheduleClass);
        ReplaceDaysCore(days);
        SetTotalWeeklyHours(totalWeeklyHours);
        RefreshConcurrencyToken();
    }

    public void Activate()
    {
        IsActive = true;
        RefreshConcurrencyToken();
    }

    public void Inactivate()
    {
        IsActive = false;
        RefreshConcurrencyToken();
    }

    /// <summary>
    /// Net hours of one day: a midnight-crossing shift (end &lt; start ⇒ night shift, legitimized by the
    /// attendance anchor) spans <c>24 − start + end</c>; a meal break (day shifts only) is subtracted.
    /// </summary>
    public static decimal ComputeNetHours(WorkScheduleDayInput day)
    {
        var crossesMidnight = day.EndTime < day.StartTime;
        var shiftHours = crossesMidnight
            ? 24m - (decimal)day.StartTime.ToTimeSpan().TotalHours + (decimal)day.EndTime.ToTimeSpan().TotalHours
            : (decimal)(day.EndTime.ToTimeSpan() - day.StartTime.ToTimeSpan()).TotalHours;

        var mealHours = day.MealStart.HasValue && day.MealEnd.HasValue
            ? (decimal)(day.MealEnd.Value.ToTimeSpan() - day.MealStart.Value.ToTimeSpan()).TotalHours
            : 0m;

        return Math.Round(shiftHours - mealHours, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Replaces the full day set (triple-replace style — the whole week travels on every edit). Guards:
    /// at least one day; DayOfWeek 0-6 unique; a zero-length shift is invalid; the meal break requires a
    /// DAY shift (a midnight-crossing shift carries no meal in F1) and must be contained in it.
    /// </summary>
    public void ReplaceDays(IReadOnlyCollection<WorkScheduleDayInput> days)
    {
        ReplaceDaysCore(days);
        RefreshConcurrencyToken();
    }

    private void ReplaceDaysCore(IReadOnlyCollection<WorkScheduleDayInput> days)
    {
        ArgumentNullException.ThrowIfNull(days);
        if (days.Count == 0)
        {
            throw new ArgumentException("A work schedule requires at least one day.", nameof(days));
        }

        var ordered = days.OrderBy(day => day.DayOfWeek).ToList();
        var seen = new HashSet<int>();
        foreach (var day in ordered)
        {
            if (day.DayOfWeek is < 0 or > 6)
            {
                throw new ArgumentOutOfRangeException(nameof(days), "Day of week must be between 0 (Sunday) and 6 (Saturday).");
            }

            if (!seen.Add(day.DayOfWeek))
            {
                throw new ArgumentException("Each day of week may appear only once in a work schedule.", nameof(days));
            }

            if (day.EndTime == day.StartTime)
            {
                throw new ArgumentException("A work-schedule day cannot have a zero-length shift.", nameof(days));
            }

            var crossesMidnight = day.EndTime < day.StartTime;
            if (day.MealStart.HasValue != day.MealEnd.HasValue)
            {
                throw new ArgumentException("A meal break requires both its start and its end.", nameof(days));
            }

            if (day.MealStart.HasValue && day.MealEnd.HasValue)
            {
                if (crossesMidnight)
                {
                    throw new ArgumentException(
                        "A midnight-crossing (night) shift cannot carry a meal break.", nameof(days));
                }

                if (day.MealEnd.Value <= day.MealStart.Value)
                {
                    throw new ArgumentException("The meal break end must be after its start.", nameof(days));
                }

                if (day.MealStart.Value < day.StartTime || day.MealEnd.Value > day.EndTime)
                {
                    throw new ArgumentException("The meal break must be contained in the shift.", nameof(days));
                }
            }
        }

        _days.Clear();
        foreach (var day in ordered)
        {
            _days.Add(WorkScheduleDay.Create(
                day.DayOfWeek,
                day.StartTime,
                day.EndTime,
                day.MealStart,
                day.MealEnd,
                ComputeNetHours(day)));
        }
    }

    private void SetCode(string code)
    {
        Code = PayrollNormalization.NormalizeCode(code);
        if (Code.Length > MaxCodeLength)
        {
            throw new ArgumentException($"Code must be {MaxCodeLength} characters or fewer.", nameof(code));
        }

        NormalizedCode = Code;
    }

    private void SetName(string name)
    {
        Name = PayrollNormalization.Clean(name, nameof(name));
        if (Name.Length > MaxNameLength)
        {
            throw new ArgumentException($"Name must be {MaxNameLength} characters or fewer.", nameof(name));
        }

        NormalizedName = PayrollNormalization.NormalizeName(Name);
    }

    private void SetScheduleLabel(string? scheduleLabel)
    {
        var cleaned = PayrollNormalization.CleanOptional(scheduleLabel);
        if (cleaned is not null && cleaned.Length > MaxScheduleLabelLength)
        {
            throw new ArgumentException(
                $"Schedule label must be {MaxScheduleLabelLength} characters or fewer.", nameof(scheduleLabel));
        }

        ScheduleLabel = cleaned;
    }

    private void SetAttendanceDateAnchor(string attendanceDateAnchor)
    {
        var normalized = PayrollNormalization.NormalizeCode(attendanceDateAnchor);
        if (!WorkScheduleAnchors.All.Contains(normalized))
        {
            throw new ArgumentException("Attendance date anchor must be ENTRADA or SALIDA.", nameof(attendanceDateAnchor));
        }

        AttendanceDateAnchor = normalized;
    }

    private void SetScheduleClass(string scheduleClass)
    {
        var normalized = PayrollNormalization.NormalizeCode(scheduleClass);
        if (!WorkScheduleClasses.All.Contains(normalized))
        {
            throw new ArgumentException("Schedule class must be ORDINARIA or EXTRAORDINARIA.", nameof(scheduleClass));
        }

        ScheduleClass = normalized;
    }

    private void SetTotalWeeklyHours(decimal? totalWeeklyHours)
    {
        var hours = totalWeeklyHours ?? Math.Round(_days.Sum(day => day.NetHours), 2, MidpointRounding.AwayFromZero);
        if (hours <= 0m || hours > MaxWeeklyHours)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalWeeklyHours), $"Total weekly hours must be greater than zero and at most {MaxWeeklyHours}.");
        }

        TotalWeeklyHours = hours;
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}

/// <summary>
/// One weekday of a <see cref="WorkSchedule"/> (shift times, optional meal break, derived net hours).
/// Immutable child: the parent replaces the full set via <see cref="WorkSchedule.ReplaceDays"/>, so it
/// carries no mutators and no concurrency token of its own (mirrors <c>LactationSchedule</c>).
/// </summary>
public sealed class WorkScheduleDay : TenantEntity
{
    private WorkScheduleDay()
    {
    }

    private WorkScheduleDay(
        Guid publicId,
        int dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        TimeOnly? mealStart,
        TimeOnly? mealEnd,
        decimal netHours)
    {
        PublicId = publicId;
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
        MealStart = mealStart;
        MealEnd = mealEnd;
        NetHours = netHours;
    }

    public long WorkScheduleId { get; private set; }

    /// <summary>0 (Sunday) … 6 (Saturday) — .NET <see cref="System.DayOfWeek"/> numbering.</summary>
    public int DayOfWeek { get; private set; }

    public TimeOnly StartTime { get; private set; }

    /// <summary>End of the shift; earlier than <see cref="StartTime"/> ⇒ the shift crosses midnight.</summary>
    public TimeOnly EndTime { get; private set; }

    public TimeOnly? MealStart { get; private set; }

    public TimeOnly? MealEnd { get; private set; }

    /// <summary>Derived net hours of the day (shift minus meal break), rounded once (Round2).</summary>
    public decimal NetHours { get; private set; }

    internal static WorkScheduleDay Create(
        int dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        TimeOnly? mealStart,
        TimeOnly? mealEnd,
        decimal netHours) =>
        new(Guid.NewGuid(), dayOfWeek, startTime, endTime, mealStart, mealEnd, netHours);
}
