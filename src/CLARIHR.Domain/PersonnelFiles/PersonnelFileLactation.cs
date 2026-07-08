using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PersonnelFiles;

/// <summary>
/// Input for one daily-permit schedule when replacing a lactation period's schedule set via
/// <see cref="PersonnelFileLactationPeriod.ReplaceSchedules"/>.
/// </summary>
public readonly record struct LactationScheduleInput(
    DateOnly StartDate,
    DateOnly EndDate,
    int DailyPermitsCount,
    int MinutesPerPermit);

/// <summary>
/// A lactation period ("periodo de lactancia") of an employee: an HR-registered date range tied to the
/// LACTANCIA incapacity-type template, with one or more daily-permit schedules (each contained in the
/// period, non-overlapping). Reuses the <see cref="IncapacityStatuses"/> codes WITHOUT EN_REVISION —
/// there is no self-service review flow, the record is born REGISTRADA and can only be annulled.
/// </summary>
public sealed class PersonnelFileLactationPeriod : TenantEntity
{
    private readonly List<LactationSchedule> _schedules = [];

    private PersonnelFileLactationPeriod()
    {
    }

    private PersonnelFileLactationPeriod(
        Guid? requesterFilePublicId,
        string? requesterNameSnapshot,
        string requestedByUserId,
        long incapacityTypeId,
        DateOnly startDate,
        DateOnly endDate,
        string? notes)
    {
        if (incapacityTypeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(incapacityTypeId), "Incapacity type id must be positive.");
        }

        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        IsActive = true;
        StatusCode = IncapacityStatuses.Registrada;

        RequesterFilePublicId = requesterFilePublicId;
        RequesterNameSnapshot = PersonnelFileNormalization.CleanOptional(requesterNameSnapshot);
        RequestedByUserId = PersonnelFileNormalization.Clean(requestedByUserId, nameof(requestedByUserId));
        IncapacityTypeId = incapacityTypeId;
        ApplyDates(startDate, endDate);
        Notes = PersonnelFileNormalization.CleanOptional(notes);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    // Requester trío: the employee file it belongs to is the aggregate anchor; these audit who asked/typed.
    public Guid? RequesterFilePublicId { get; private set; }

    public string? RequesterNameSnapshot { get; private set; }

    public string RequestedByUserId { get; private set; } = string.Empty;

    /// <summary>The LACTANCIA incapacity-type template this period maps to for payroll labels.</summary>
    public long IncapacityTypeId { get; private set; }

    public Leave.IncapacityType? IncapacityType { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    /// <summary>Reuses <see cref="IncapacityStatuses"/> WITHOUT EN_REVISION (REGISTRADA → ANULADA).</summary>
    public string StatusCode { get; private set; } = IncapacityStatuses.Registrada;

    public string? AnnulmentReason { get; private set; }

    public DateTime? AnnulledAtUtc { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public IReadOnlyCollection<LactationSchedule> Schedules => _schedules.AsReadOnly();

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public static PersonnelFileLactationPeriod Create(
        Guid? requesterFilePublicId,
        string? requesterNameSnapshot,
        string requestedByUserId,
        long incapacityTypeId,
        DateOnly startDate,
        DateOnly endDate,
        string? notes) =>
        new(
            requesterFilePublicId,
            requesterNameSnapshot,
            requestedByUserId,
            incapacityTypeId,
            startDate,
            endDate,
            notes);

    /// <summary>
    /// Edits the period dates and notes while not annulled. The existing schedules must remain contained
    /// in the new range — when the update shrinks the period and leaves a schedule out of range it throws
    /// (the handler replaces the schedules together with the update when the range moves).
    /// </summary>
    public void UpdatePeriod(DateOnly startDate, DateOnly endDate, string? notes)
    {
        EnsureNotAnnulled();

        if (endDate < startDate)
        {
            throw new ArgumentException("The end date cannot precede the start date.", nameof(endDate));
        }

        if (_schedules.Any(schedule => schedule.StartDate < startDate || schedule.EndDate > endDate))
        {
            throw new InvalidOperationException(
                "The period cannot shrink below its schedules: every existing schedule must remain contained in the new range.");
        }

        ApplyDates(startDate, endDate);
        Notes = PersonnelFileNormalization.CleanOptional(notes);
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Replaces the full schedule set: every range must be contained in the period, ranges must not
    /// overlap each other and both counts must be at least 1. The set is re-sorted by start date and
    /// persisted with a sequential <c>SortOrder</c> 1..n.
    /// </summary>
    public void ReplaceSchedules(IReadOnlyCollection<LactationScheduleInput> schedules)
    {
        ArgumentNullException.ThrowIfNull(schedules);
        EnsureNotAnnulled();

        var ordered = schedules.OrderBy(schedule => schedule.StartDate).ThenBy(schedule => schedule.EndDate).ToList();

        for (var index = 0; index < ordered.Count; index++)
        {
            var input = ordered[index];

            if (input.EndDate < input.StartDate)
            {
                throw new ArgumentException(
                    "A schedule end date must be greater than or equal to its start date.",
                    nameof(schedules));
            }

            if (input.StartDate < StartDate || input.EndDate > EndDate)
            {
                throw new ArgumentException(
                    "Every schedule range must be contained in the lactation period.",
                    nameof(schedules));
            }

            if (input.DailyPermitsCount < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(schedules),
                    "Daily permits count must be at least one.");
            }

            if (input.MinutesPerPermit < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(schedules),
                    "Minutes per permit must be at least one.");
            }

            if (index > 0 && input.StartDate <= ordered[index - 1].EndDate)
            {
                throw new ArgumentException(
                    "Schedule ranges must not overlap each other.",
                    nameof(schedules));
            }
        }

        _schedules.Clear();
        for (var index = 0; index < ordered.Count; index++)
        {
            var input = ordered[index];
            _schedules.Add(LactationSchedule.Create(
                input.StartDate,
                input.EndDate,
                input.DailyPermitsCount,
                input.MinutesPerPermit,
                sortOrder: index + 1));
        }

        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Annuls the period (terminal, same shape as the incapacity Annul); the reason is mandatory.</summary>
    public void Annul(string reason, DateTime atUtc)
    {
        var normalizedReason = PersonnelFileNormalization.Clean(reason, nameof(reason));
        if (!IncapacityStatuses.Annullable.Contains(StatusCode))
        {
            throw new InvalidOperationException("Only a REGISTRADA lactation period can be annulled.");
        }

        StatusCode = IncapacityStatuses.Anulada;
        AnnulmentReason = normalizedReason;
        AnnulledAtUtc = atUtc;
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    private void EnsureNotAnnulled()
    {
        if (StatusCode == IncapacityStatuses.Anulada)
        {
            throw new InvalidOperationException("An annulled lactation period cannot be modified.");
        }
    }

    private void ApplyDates(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("The end date cannot precede the start date.", nameof(endDate));
        }

        StartDate = startDate;
        EndDate = endDate;
    }
}

/// <summary>
/// One daily-permit schedule of a <see cref="PersonnelFileLactationPeriod"/> (date range → permits per day ×
/// minutes per permit). Immutable child: the parent replaces the full set via
/// <see cref="PersonnelFileLactationPeriod.ReplaceSchedules"/>, so it carries no mutators and no concurrency
/// token of its own (mirrors <c>IncapacityRiskParameter</c>).
/// </summary>
public sealed class LactationSchedule : TenantEntity
{
    private LactationSchedule()
    {
    }

    private LactationSchedule(
        Guid publicId,
        DateOnly startDate,
        DateOnly endDate,
        int dailyPermitsCount,
        int minutesPerPermit,
        int sortOrder)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("The end date cannot precede the start date.", nameof(endDate));
        }

        if (dailyPermitsCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(dailyPermitsCount), "Daily permits count must be at least one.");
        }

        if (minutesPerPermit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minutesPerPermit), "Minutes per permit must be at least one.");
        }

        if (sortOrder < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to one.");
        }

        PublicId = publicId;
        StartDate = startDate;
        EndDate = endDate;
        DailyPermitsCount = dailyPermitsCount;
        MinutesPerPermit = minutesPerPermit;
        SortOrder = sortOrder;
    }

    public long LactationPeriodId { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public int DailyPermitsCount { get; private set; }

    public int MinutesPerPermit { get; private set; }

    public int SortOrder { get; private set; }

    internal static LactationSchedule Create(
        DateOnly startDate,
        DateOnly endDate,
        int dailyPermitsCount,
        int minutesPerPermit,
        int sortOrder) =>
        new(
            Guid.NewGuid(),
            startDate,
            endDate,
            dailyPermitsCount,
            minutesPerPermit,
            sortOrder);
}
