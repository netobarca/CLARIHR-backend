using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PersonnelFiles;

/// <summary>
/// Canonical source codes of a <see cref="PersonnelFileVacationPeriod"/>: created one-off by HR (MANUAL) or
/// produced by the company-wide idempotent mass generation (GENERACION_MASIVA). The codes are validated
/// against the domain constants; there is no visualization catalog for them (they are audit metadata).
/// </summary>
public static class VacationPeriodSources
{
    public const string Manual = "MANUAL";
    public const string MassGeneration = "GENERACION_MASIVA";

    public static readonly IReadOnlyCollection<string> All = new[] { Manual, MassGeneration };

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToUpperInvariant());
}

/// <summary>
/// Canonical status codes for an employee vacation request ("solicitud de vacaciones"). Validated against the
/// country-scoped <c>vacation-request-statuses</c> catalog (visualization / i18n), while the domain transition
/// logic references these constants (leave module D-16). SOLICITADA is the birth state; a decision moves it to
/// APROBADA or RECHAZADA; the owner may cancel it while SOLICITADA; partial/total returns walk it through
/// DEVUELTA_PARCIAL to DEVUELTA (D-14).
/// </summary>
public static class VacationRequestStatuses
{
    public const string Solicitada = "SOLICITADA";
    public const string Aprobada = "APROBADA";
    public const string Rechazada = "RECHAZADA";
    public const string Anulada = "ANULADA";
    public const string DevueltaParcial = "DEVUELTA_PARCIAL";
    public const string Devuelta = "DEVUELTA";

    /// <summary>States a decision (approve/reject) may target.</summary>
    public static readonly IReadOnlyCollection<string> DecisionTargets = new[] { Solicitada };

    /// <summary>States the owner may still cancel.</summary>
    public static readonly IReadOnlyCollection<string> Cancellable = new[] { Solicitada };

    /// <summary>States from which days may still be returned (fully or partially — D-14).</summary>
    public static readonly IReadOnlyCollection<string> Returnable = new[] { Aprobada, DevueltaParcial };

    /// <summary>
    /// The states that consume fund days (an approved request whose days are not yet fully returned). Used by the
    /// fund's available-days and consumption checks (RN-16/RF-016).
    /// </summary>
    public static readonly IReadOnlyCollection<string> ConsumesFund = new[] { Aprobada, DevueltaParcial, Devuelta };

    public static bool CountsAsConsumption(string status) => ConsumesFund.Contains(status);
}

/// <summary>Input for one fund-period allocation when approving a request via <see cref="PersonnelFileVacationRequest.Approve"/>.</summary>
public readonly record struct VacationAllocationInput(long VacationPeriodId, int Days);

/// <summary>One period → days split of a return distribution (persisted with the return for LIFO auditability).</summary>
public readonly record struct VacationReturnDistributionInput(long VacationPeriodId, int Days);

/// <summary>
/// A yearly vacation fund entry for one employee (leave module D-05 — the fund is PER EMPLOYEE). It grants the
/// legal days plus any company-benefit days for one <see cref="PeriodYear"/> whose bounds are derived from the
/// employee's primary-plaza anniversary or the calendar year (<see cref="UsedAnniversary"/>). Balances (enjoyed
/// / pending) are NOT columns — they are derived from the linked requests' allocations and returns. The
/// filtered-unique index <c>(tenant, personnel_file_id, period_year) WHERE is_active</c> guarantees at most one
/// active period per employee-year (RN-19).
/// </summary>
public sealed class PersonnelFileVacationPeriod : TenantEntity
{
    private PersonnelFileVacationPeriod()
    {
    }

    private PersonnelFileVacationPeriod(
        int periodYear,
        DateOnly periodStartDate,
        DateOnly periodEndDate,
        int legalDaysGranted,
        int benefitDaysGranted,
        bool generatesEnjoymentDays,
        bool usedAnniversary,
        string sourceCode)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        IsActive = true;

        ApplyGrants(legalDaysGranted, benefitDaysGranted);
        ApplyBounds(periodYear, periodStartDate, periodEndDate);
        GeneratesEnjoymentDays = generatesEnjoymentDays;
        UsedAnniversary = usedAnniversary;

        var normalizedSource = PersonnelFileNormalization.Clean(sourceCode, nameof(sourceCode)).ToUpperInvariant();
        if (!VacationPeriodSources.All.Contains(normalizedSource))
        {
            throw new ArgumentException(
                $"Source code '{sourceCode}' is not supported. Allowed codes: {string.Join(", ", VacationPeriodSources.All)}.",
                nameof(sourceCode));
        }

        SourceCode = normalizedSource;
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public int PeriodYear { get; private set; }

    public DateOnly PeriodStartDate { get; private set; }

    public DateOnly PeriodEndDate { get; private set; }

    /// <summary>Statutory vacation days granted for the period (Art. 177 — must be &gt; 0).</summary>
    public int LegalDaysGranted { get; private set; }

    /// <summary>Extra company-benefit days on top of the statutory grant (≥ 0).</summary>
    public int BenefitDaysGranted { get; private set; }

    /// <summary>Whether the period contributes enjoyment days to the profile balance / settlement (D-05).</summary>
    public bool GeneratesEnjoymentDays { get; private set; }

    /// <summary>True when the bounds were derived from the primary-plaza anniversary (else calendar year).</summary>
    public bool UsedAnniversary { get; private set; }

    public string SourceCode { get; private set; } = VacationPeriodSources.Manual;

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    /// <summary>Total granted days of the period (legal + benefit).</summary>
    public int TotalDaysGranted => LegalDaysGranted + BenefitDaysGranted;

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public static PersonnelFileVacationPeriod Create(
        int periodYear,
        DateOnly periodStartDate,
        DateOnly periodEndDate,
        int legalDaysGranted,
        int benefitDaysGranted,
        bool generatesEnjoymentDays,
        bool usedAnniversary,
        string sourceCode) =>
        new(
            periodYear,
            periodStartDate,
            periodEndDate,
            legalDaysGranted,
            benefitDaysGranted,
            generatesEnjoymentDays,
            usedAnniversary,
            sourceCode);

    /// <summary>
    /// Edits the granted days (D-05). The domain only validates the ranges (legal &gt; 0, benefit ≥ 0); the
    /// no-consumption guard (RF-016) is enforced by the handler — a period with live allocations cannot change
    /// its grants.
    /// </summary>
    public void UpdateGrants(int legalDaysGranted, int benefitDaysGranted)
    {
        EnsureActive();
        ApplyGrants(legalDaysGranted, benefitDaysGranted);
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Soft-deletes the period (RF-016 — only allowed by the handler when there is no consumption).</summary>
    public void Deactivate()
    {
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    private void ApplyGrants(int legalDaysGranted, int benefitDaysGranted)
    {
        if (legalDaysGranted <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(legalDaysGranted), "Legal days granted must be greater than zero.");
        }

        if (benefitDaysGranted < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(benefitDaysGranted), "Benefit days granted cannot be negative.");
        }

        LegalDaysGranted = legalDaysGranted;
        BenefitDaysGranted = benefitDaysGranted;
    }

    private void ApplyBounds(int periodYear, DateOnly periodStartDate, DateOnly periodEndDate)
    {
        if (periodYear < 2000 || periodYear > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(periodYear), "Period year is out of the supported range.");
        }

        if (periodEndDate < periodStartDate)
        {
            throw new ArgumentException("The period end date cannot precede the start date.", nameof(periodEndDate));
        }

        PeriodYear = periodYear;
        PeriodStartDate = periodStartDate;
        PeriodEndDate = periodEndDate;
    }

    private void EnsureActive()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("An inactive vacation period cannot be modified.");
        }
    }
}

/// <summary>
/// An employee vacation request ("solicitud de vacaciones"). Born SOLICITADA, it carries the requester trío,
/// the date range and requested days, and — once approved — the fund allocations (FIFO by default, editable)
/// and any total/partial returns (LIFO by default), with the return distribution persisted for auditability.
/// The lifecycle guards (<see cref="Approve"/> / <see cref="Reject"/> / <see cref="Cancel"/> /
/// <see cref="Return"/>) are the ONLY write paths; they are wired by PR-8 (this PR only creates the domain +
/// tables so the fund vertical can compute consumption).
/// </summary>
public sealed class PersonnelFileVacationRequest : TenantEntity
{
    private readonly List<VacationRequestAllocation> _allocations = [];
    private readonly List<VacationReturn> _returns = [];

    private PersonnelFileVacationRequest()
    {
    }

    private PersonnelFileVacationRequest(
        Guid? requesterFilePublicId,
        string? requesterNameSnapshot,
        string requestedByUserId,
        DateOnly startDate,
        DateOnly endDate,
        int requestedDays,
        Guid? planLinePublicId,
        string? notes)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        IsActive = true;
        StatusCode = VacationRequestStatuses.Solicitada;

        RequesterFilePublicId = requesterFilePublicId;
        RequesterNameSnapshot = PersonnelFileNormalization.CleanOptional(requesterNameSnapshot);
        RequestedByUserId = PersonnelFileNormalization.Clean(requestedByUserId, nameof(requestedByUserId));

        if (requestedDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedDays), "Requested days must be greater than zero.");
        }

        if (endDate < startDate)
        {
            throw new ArgumentException("The end date cannot precede the start date.", nameof(endDate));
        }

        StartDate = startDate;
        EndDate = endDate;
        RequestedDays = requestedDays;
        PlanLinePublicId = planLinePublicId;
        Notes = PersonnelFileNormalization.CleanOptional(notes);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public Guid? RequesterFilePublicId { get; private set; }

    public string? RequesterNameSnapshot { get; private set; }

    public string RequestedByUserId { get; private set; } = string.Empty;

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public int RequestedDays { get; private set; }

    public string StatusCode { get; private set; } = VacationRequestStatuses.Solicitada;

    /// <summary>Optional link to the yearly plan line the request was raised from (D-24).</summary>
    public Guid? PlanLinePublicId { get; private set; }

    public string? DecidedByUserId { get; private set; }

    public DateTime? DecisionDateUtc { get; private set; }

    public string? DecisionNotes { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public IReadOnlyCollection<VacationRequestAllocation> Allocations => _allocations.AsReadOnly();

    public IReadOnlyCollection<VacationReturn> Returns => _returns.AsReadOnly();

    /// <summary>Σ allocated days (the consumption held against the fund once approved).</summary>
    public int ConsumedDays => _allocations.Sum(allocation => allocation.Days);

    /// <summary>Σ returned days so far.</summary>
    public int ReturnedDays => _returns.Sum(entry => entry.Days);

    /// <summary>Consumed days that have not yet been returned (the net held against the fund).</summary>
    public int NetConsumedDays => ConsumedDays - ReturnedDays;

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public static PersonnelFileVacationRequest Create(
        Guid? requesterFilePublicId,
        string? requesterNameSnapshot,
        string requestedByUserId,
        DateOnly startDate,
        DateOnly endDate,
        int requestedDays,
        Guid? planLinePublicId,
        string? notes) =>
        new(
            requesterFilePublicId,
            requesterNameSnapshot,
            requestedByUserId,
            startDate,
            endDate,
            requestedDays,
            planLinePublicId,
            notes);

    /// <summary>
    /// Approves a SOLICITADA request against the given fund allocations (D-13): the allocations must sum to
    /// <see cref="RequestedDays"/> and each be positive. Moves the request to APROBADA.
    /// </summary>
    public void Approve(IReadOnlyCollection<VacationAllocationInput> allocations, string byUserId, DateTime atUtc)
    {
        ArgumentNullException.ThrowIfNull(allocations);
        var normalizedBy = PersonnelFileNormalization.Clean(byUserId, nameof(byUserId));

        if (!VacationRequestStatuses.DecisionTargets.Contains(StatusCode))
        {
            throw new InvalidOperationException("Only a SOLICITADA vacation request can be approved.");
        }

        if (allocations.Count == 0)
        {
            throw new ArgumentException("At least one fund allocation is required to approve the request.", nameof(allocations));
        }

        var total = 0;
        foreach (var allocation in allocations)
        {
            if (allocation.VacationPeriodId <= 0)
            {
                throw new ArgumentException("Every allocation must reference a valid vacation period.", nameof(allocations));
            }

            if (allocation.Days <= 0)
            {
                throw new ArgumentException("Every allocation must be for a positive number of days.", nameof(allocations));
            }

            total += allocation.Days;
        }

        if (total != RequestedDays)
        {
            throw new InvalidOperationException("The sum of the allocations must equal the requested days.");
        }

        _allocations.Clear();
        foreach (var allocation in allocations)
        {
            _allocations.Add(VacationRequestAllocation.Create(allocation.VacationPeriodId, allocation.Days));
        }

        StatusCode = VacationRequestStatuses.Aprobada;
        DecidedByUserId = normalizedBy;
        DecisionDateUtc = atUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Rejects a SOLICITADA request (terminal); the decision notes are optional.</summary>
    public void Reject(string byUserId, DateTime atUtc, string? notes)
    {
        var normalizedBy = PersonnelFileNormalization.Clean(byUserId, nameof(byUserId));
        if (!VacationRequestStatuses.DecisionTargets.Contains(StatusCode))
        {
            throw new InvalidOperationException("Only a SOLICITADA vacation request can be rejected.");
        }

        StatusCode = VacationRequestStatuses.Rechazada;
        DecidedByUserId = normalizedBy;
        DecisionDateUtc = atUtc;
        DecisionNotes = PersonnelFileNormalization.CleanOptional(notes);
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Cancels a SOLICITADA request (owner self-service while pending, D-18).</summary>
    public void Cancel()
    {
        if (!VacationRequestStatuses.Cancellable.Contains(StatusCode))
        {
            throw new InvalidOperationException("Only a SOLICITADA vacation request can be cancelled.");
        }

        StatusCode = VacationRequestStatuses.Anulada;
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Registers a total/partial return of enjoyed days (D-14). Only an APROBADA or DEVUELTA_PARCIAL request
    /// can return days; the cumulative returned amount cannot exceed the consumed days. When the return
    /// exhausts the consumption the request moves to DEVUELTA, otherwise DEVUELTA_PARCIAL. The distribution
    /// (period → days) is persisted for the LIFO reversal audit.
    /// </summary>
    public void Return(
        int days,
        string? reason,
        string byUserId,
        DateTime atUtc,
        IReadOnlyCollection<VacationReturnDistributionInput> distribution)
    {
        ArgumentNullException.ThrowIfNull(distribution);
        var normalizedBy = PersonnelFileNormalization.Clean(byUserId, nameof(byUserId));

        if (!VacationRequestStatuses.Returnable.Contains(StatusCode))
        {
            throw new InvalidOperationException("Only an approved or partially-returned vacation request can return days.");
        }

        if (days <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days), "Returned days must be greater than zero.");
        }

        if (ReturnedDays + days > ConsumedDays)
        {
            throw new InvalidOperationException("The cumulative returned days cannot exceed the consumed days.");
        }

        var distributionTotal = 0;
        foreach (var entry in distribution)
        {
            if (entry.VacationPeriodId <= 0)
            {
                throw new ArgumentException("Every distribution entry must reference a valid vacation period.", nameof(distribution));
            }

            if (entry.Days <= 0)
            {
                throw new ArgumentException("Every distribution entry must be for a positive number of days.", nameof(distribution));
            }

            distributionTotal += entry.Days;
        }

        if (distributionTotal != days)
        {
            throw new InvalidOperationException("The return distribution must sum to the returned days.");
        }

        _returns.Add(VacationReturn.Create(days, atUtc, reason, normalizedBy, distribution));

        StatusCode = ReturnedDays >= ConsumedDays
            ? VacationRequestStatuses.Devuelta
            : VacationRequestStatuses.DevueltaParcial;
        ConcurrencyToken = Guid.NewGuid();
    }
}

/// <summary>
/// One fund-period allocation of an approved <see cref="PersonnelFileVacationRequest"/> (which period the
/// enjoyed days are drawn from, and how many). Immutable child: the request replaces the full set on approval
/// (mirrors <c>LactationSchedule</c> / <c>IncapacityRiskParameter</c>).
/// </summary>
public sealed class VacationRequestAllocation : TenantEntity
{
    private VacationRequestAllocation()
    {
    }

    private VacationRequestAllocation(Guid publicId, long vacationPeriodId, int days)
    {
        if (vacationPeriodId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(vacationPeriodId), "Vacation period id must be positive.");
        }

        if (days <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days), "Allocated days must be greater than zero.");
        }

        PublicId = publicId;
        VacationPeriodId = vacationPeriodId;
        Days = days;
    }

    public long VacationRequestId { get; private set; }

    public long VacationPeriodId { get; private set; }

    public PersonnelFileVacationPeriod VacationPeriod { get; private set; } = null!;

    public int Days { get; private set; }

    internal static VacationRequestAllocation Create(long vacationPeriodId, int days) =>
        new(Guid.NewGuid(), vacationPeriodId, days);
}

/// <summary>
/// One total/partial return of enjoyed days of a <see cref="PersonnelFileVacationRequest"/> (D-14): how many
/// days came back, when, why, who decided it and the persisted period distribution (jsonb) of the LIFO
/// reversal. Immutable child (appended by <see cref="PersonnelFileVacationRequest.Return"/>).
/// </summary>
public sealed class VacationReturn : TenantEntity
{
    private VacationReturn()
    {
    }

    private VacationReturn(
        Guid publicId,
        int days,
        DateTime returnDateUtc,
        string? reason,
        string decidedByUserId,
        string distributionJson)
    {
        if (days <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days), "Returned days must be greater than zero.");
        }

        PublicId = publicId;
        Days = days;
        ReturnDateUtc = returnDateUtc;
        Reason = PersonnelFileNormalization.CleanOptional(reason);
        DecidedByUserId = PersonnelFileNormalization.Clean(decidedByUserId, nameof(decidedByUserId));
        DistributionJson = distributionJson;
    }

    public long VacationRequestId { get; private set; }

    public int Days { get; private set; }

    public DateTime ReturnDateUtc { get; private set; }

    public string? Reason { get; private set; }

    public string DecidedByUserId { get; private set; } = string.Empty;

    /// <summary>Persisted period → days distribution of the reversal (jsonb) — the LIFO audit trail.</summary>
    public string DistributionJson { get; private set; } = "[]";

    internal static VacationReturn Create(
        int days,
        DateTime returnDateUtc,
        string? reason,
        string decidedByUserId,
        IReadOnlyCollection<VacationReturnDistributionInput> distribution)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(
            distribution.Select(entry => new { entry.VacationPeriodId, entry.Days }).ToArray(),
            DistributionSerializationOptions);
        return new VacationReturn(Guid.NewGuid(), days, returnDateUtc, reason, decidedByUserId, json);
    }

    private static readonly System.Text.Json.JsonSerializerOptions DistributionSerializationOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);
}
