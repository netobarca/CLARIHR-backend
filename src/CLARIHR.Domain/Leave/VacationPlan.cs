using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Leave;

/// <summary>Canonical status codes of a company yearly vacation plan (indicative, non-binding — D-24).</summary>
public static class VacationPlanStatuses
{
    public const string Vigente = "VIGENTE";
    public const string Anulado = "ANULADO";

    public static readonly IReadOnlyCollection<string> All = new[] { Vigente, Anulado };

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToUpperInvariant());
}

/// <summary>Input for one planned vacation line when replacing a plan's lines via <see cref="VacationPlan.ReplaceLines"/>.</summary>
public readonly record struct VacationPlanLineInput(
    Guid PersonnelFilePublicId,
    DateOnly StartDate,
    DateOnly EndDate,
    int Days);

/// <summary>
/// A company-level yearly vacation plan ("plan anual de vacaciones") — an indicative schedule of the intended
/// vacation windows per employee for one <see cref="PlanYear"/> (D-24). It is a soft planning artefact (no fund
/// consumption): its lines carry the intended dates and days; the aggregate guarantees a single employee has no
/// overlapping lines. Built here for PR-9 (the tables ship with the fund migration M4) — this PR does not wire
/// its endpoints.
/// </summary>
public sealed class VacationPlan : TenantEntity
{
    private readonly List<VacationPlanLine> _lines = [];

    private VacationPlan()
    {
    }

    private VacationPlan(
        int planYear,
        DateOnly requestDate,
        string requestedByUserId,
        string? requesterNameSnapshot)
    {
        if (planYear < 2000 || planYear > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(planYear), "Plan year is out of the supported range.");
        }

        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        IsActive = true;
        StatusCode = VacationPlanStatuses.Vigente;

        PlanYear = planYear;
        RequestDate = requestDate;
        RequestedByUserId = LeaveNormalization.Clean(requestedByUserId, nameof(requestedByUserId));
        RequesterNameSnapshot = LeaveNormalization.CleanOptional(requesterNameSnapshot);
    }

    public int PlanYear { get; private set; }

    public DateOnly RequestDate { get; private set; }

    public string RequestedByUserId { get; private set; } = string.Empty;

    public string? RequesterNameSnapshot { get; private set; }

    public string StatusCode { get; private set; } = VacationPlanStatuses.Vigente;

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public IReadOnlyCollection<VacationPlanLine> Lines => _lines.AsReadOnly();

    public static VacationPlan Create(
        int planYear,
        DateOnly requestDate,
        string requestedByUserId,
        string? requesterNameSnapshot) =>
        new(planYear, requestDate, requestedByUserId, requesterNameSnapshot);

    /// <summary>
    /// Replaces the full set of planned lines. Each line must be a coherent date range with positive days, and
    /// the lines of one employee must not overlap each other (guard in the aggregate).
    /// </summary>
    public void ReplaceLines(IReadOnlyCollection<VacationPlanLineInput> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        EnsureActive();

        foreach (var employeeLines in lines.GroupBy(line => line.PersonnelFilePublicId))
        {
            var ordered = employeeLines.OrderBy(line => line.StartDate).ThenBy(line => line.EndDate).ToList();
            for (var index = 0; index < ordered.Count; index++)
            {
                var line = ordered[index];
                if (line.PersonnelFilePublicId == Guid.Empty)
                {
                    throw new ArgumentException("Every plan line must reference an employee.", nameof(lines));
                }

                if (line.EndDate < line.StartDate)
                {
                    throw new ArgumentException("A plan line end date cannot precede its start date.", nameof(lines));
                }

                if (line.Days <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(lines), "A plan line must be for a positive number of days.");
                }

                if (index > 0 && line.StartDate <= ordered[index - 1].EndDate)
                {
                    throw new ArgumentException("An employee's plan lines must not overlap each other.", nameof(lines));
                }
            }
        }

        _lines.Clear();
        var sortOrder = 1;
        foreach (var line in lines.OrderBy(line => line.PersonnelFilePublicId).ThenBy(line => line.StartDate))
        {
            _lines.Add(VacationPlanLine.Create(line.PersonnelFilePublicId, line.StartDate, line.EndDate, line.Days, sortOrder++));
        }

        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Annuls the plan (terminal).</summary>
    public void Annul()
    {
        if (StatusCode != VacationPlanStatuses.Vigente)
        {
            throw new InvalidOperationException("Only a VIGENTE vacation plan can be annulled.");
        }

        StatusCode = VacationPlanStatuses.Anulado;
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    private void EnsureActive()
    {
        if (StatusCode != VacationPlanStatuses.Vigente)
        {
            throw new InvalidOperationException("An annulled vacation plan cannot be modified.");
        }
    }
}

/// <summary>
/// One planned vacation window of a <see cref="VacationPlan"/> for one employee (public id + dates + days).
/// Immutable child: the plan replaces the full set via <see cref="VacationPlan.ReplaceLines"/>.
/// </summary>
public sealed class VacationPlanLine : TenantEntity
{
    private VacationPlanLine()
    {
    }

    private VacationPlanLine(
        Guid publicId,
        Guid personnelFilePublicId,
        DateOnly startDate,
        DateOnly endDate,
        int days,
        int sortOrder)
    {
        if (personnelFilePublicId == Guid.Empty)
        {
            throw new ArgumentException("The plan line must reference an employee.", nameof(personnelFilePublicId));
        }

        if (endDate < startDate)
        {
            throw new ArgumentException("The end date cannot precede the start date.", nameof(endDate));
        }

        if (days <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days), "Days must be greater than zero.");
        }

        if (sortOrder < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to one.");
        }

        PublicId = publicId;
        PersonnelFilePublicId = personnelFilePublicId;
        StartDate = startDate;
        EndDate = endDate;
        Days = days;
        SortOrder = sortOrder;
    }

    public long VacationPlanId { get; private set; }

    public Guid PersonnelFilePublicId { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public int Days { get; private set; }

    public int SortOrder { get; private set; }

    internal static VacationPlanLine Create(
        Guid personnelFilePublicId,
        DateOnly startDate,
        DateOnly endDate,
        int days,
        int sortOrder) =>
        new(Guid.NewGuid(), personnelFilePublicId, startDate, endDate, days, sortOrder);
}
