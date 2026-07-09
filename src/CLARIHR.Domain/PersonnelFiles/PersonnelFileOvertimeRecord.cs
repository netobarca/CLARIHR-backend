using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PersonnelFiles;

/// <summary>
/// Canonical status codes for an overtime record ("horas extras del empleado", REQ-007 D-01/RN-01). The codes
/// are validated against the country-scoped <c>overtime-record-statuses</c> catalog (visualization / i18n), but
/// the domain transition logic references these constants. A record is born <see cref="EnRevision"/>, only an
/// <see cref="Autorizada"/> record can be applied (RN-06/№13) and the application can be reverted, so
/// <see cref="Aplicada"/> is NOT terminal (the reversal — or a settlement annulment — returns it to
/// <see cref="Autorizada"/>). The two <see cref="Terminal"/> states are closed.
/// </summary>
public static class OvertimeRecordStatuses
{
    public const string EnRevision = "EN_REVISION";
    public const string Autorizada = "AUTORIZADA";
    public const string Rechazada = "RECHAZADA";
    public const string Aplicada = "APLICADA";
    public const string Anulada = "ANULADA";

    /// <summary>States whose declarative fields may still be edited (RN-02): only EN_REVISION.</summary>
    public static readonly IReadOnlyCollection<string> Editable = new[] { EnRevision };

    /// <summary>States from which the single application may be registered (RN-06): only AUTORIZADA.</summary>
    public static readonly IReadOnlyCollection<string> Applicable = new[] { Autorizada };

    /// <summary>Closed states — no further transition (APLICADA is reversible, so it is NOT terminal).</summary>
    public static readonly IReadOnlyCollection<string> Terminal = new[] { Rechazada, Anulada };
}

/// <summary>Origin channel of an overtime record (P-01/№7): registered by HR or self-registered from the portal.</summary>
public static class OvertimeRecordChannels
{
    public const string Rrhh = "RRHH";
    public const string Portal = "PORTAL";
}

/// <summary>Origin of the applied overtime record: registered by hand, by the (future) payroll engine or a settlement.</summary>
public static class OvertimeApplicationOrigins
{
    public const string Manual = "MANUAL";
    public const string Motor = "MOTOR";
    public const string Liquidacion = "LIQUIDACION";
}

/// <summary>Application lifecycle — an APLICADA application counts toward the record; an ANULADA one does not.</summary>
public static class OvertimeApplicationStatuses
{
    public const string Aplicada = "APLICADA";
    public const string Anulada = "ANULADA";
}

/// <summary>
/// An overtime shift of a personnel file ("hora extra", REQ-007 D-01): a declarative record of extra hours
/// worked (or organized for a future date — RN-07/№13). It holds the shift (work date, overtime type + code +
/// name snapshot, the type factor snapshot + the applied factor with its mandatory override note, the h:m
/// duration with the DERIVED decimal hours, the optional start/end time), the motive (justification type + code
/// + name snapshot, observations), the requester file reference + name snapshot (the trío), the origin channel
/// (RRHH / PORTAL), the mandatory plaza reference (the input + settlement scope anchor), the payroll destination
/// (payroll type + optional payroll-period imputation with its label + end date) and the EN_REVISION →
/// AUTORIZADA → APLICADA lifecycle with its rejection / annulment branches. At most one active application hangs
/// off the aggregate (<see cref="Applications"/>, RN-06/№11); the duration arithmetic + factor coherence are pure
/// rules (<c>OvertimeRecordRules</c>) — the handler validates them before <see cref="Create"/> — but the factory
/// re-derives the decimal hours and re-validates the factor note defensively. Every mutation rotates
/// <see cref="ConcurrencyToken"/>.
/// </summary>
public sealed class PersonnelFileOvertimeRecord : TenantEntity
{
    public const int MaxOvertimeTypeCodeSnapshotLength = 80;
    public const int MaxOvertimeTypeNameSnapshotLength = 200;
    public const int MaxFactorOverrideNoteLength = 300;
    public const int MaxJustificationCodeSnapshotLength = 80;
    public const int MaxJustificationNameSnapshotLength = 200;
    public const int MaxObservationsLength = 1000;
    public const int MaxRequesterNameSnapshotLength = 200;
    public const int MaxOriginChannelLength = 20;
    public const int MaxPayrollTypeCodeLength = 80;
    public const int MaxPayrollPeriodLabelLength = 80;
    public const int MaxStatusCodeLength = 80;
    public const int MaxDecisionNoteLength = 500;
    public const int MaxAnnulmentReasonLength = 500;

    private readonly List<PersonnelFileOvertimeRecordApplication> _applications = [];

    private PersonnelFileOvertimeRecord()
    {
    }

    private PersonnelFileOvertimeRecord(
        DateOnly workDate,
        Guid overtimeTypePublicId,
        string overtimeTypeCodeSnapshot,
        string overtimeTypeNameSnapshot,
        decimal typeFactorSnapshot,
        decimal factorApplied,
        string? factorOverrideNote,
        int durationHours,
        int durationMinutes,
        TimeOnly? startTime,
        TimeOnly? endTime,
        Guid justificationTypePublicId,
        string justificationCodeSnapshot,
        string justificationNameSnapshot,
        string? observations,
        string originChannel,
        Guid assignedPositionPublicId,
        Guid requesterFilePublicId,
        string requesterNameSnapshot,
        string payrollTypeCode,
        long? payrollPeriodId,
        Guid? payrollPeriodPublicId,
        string payrollPeriodLabel,
        DateOnly? payrollPeriodEndDate,
        Guid requestedByUserId)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        IsActive = true;
        StatusCode = OvertimeRecordStatuses.EnRevision;

        RequireUser(requestedByUserId, nameof(requestedByUserId));
        RequestedByUserId = requestedByUserId;

        ApplyShift(
            workDate,
            overtimeTypePublicId,
            overtimeTypeCodeSnapshot,
            overtimeTypeNameSnapshot,
            typeFactorSnapshot,
            factorApplied,
            factorOverrideNote,
            durationHours,
            durationMinutes,
            startTime,
            endTime);

        ApplyMotive(justificationTypePublicId, justificationCodeSnapshot, justificationNameSnapshot, observations);
        ApplyChannel(originChannel);
        ApplyPlaza(assignedPositionPublicId);
        ApplyRequester(requesterFilePublicId, requesterNameSnapshot);
        ApplyDestination(payrollTypeCode, payrollPeriodId, payrollPeriodPublicId, payrollPeriodLabel, payrollPeriodEndDate);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    // ── Shift ──────────────────────────────────────────────────────────────────────────────────────
    public DateOnly WorkDate { get; private set; }

    /// <summary>Logical reference to the company overtime-type master (RN-19; snapshot columns below).</summary>
    public Guid OvertimeTypePublicId { get; private set; }

    public string OvertimeTypeCodeSnapshot { get; private set; } = string.Empty;

    public string OvertimeTypeNameSnapshot { get; private set; } = string.Empty;

    /// <summary>The type's reference factor at registration time (> 0).</summary>
    public decimal TypeFactorSnapshot { get; private set; }

    /// <summary>The factor that travels to the payroll input / settlement (> 0). May override the type factor (P-06).</summary>
    public decimal FactorApplied { get; private set; }

    /// <summary>Mandatory (rule) when <see cref="FactorApplied"/> differs from <see cref="TypeFactorSnapshot"/>.</summary>
    public string? FactorOverrideNote { get; private set; }

    public int DurationHours { get; private set; }

    public int DurationMinutes { get; private set; }

    /// <summary>Derived + persisted decimal hours (2 h 30 m = 2.50; №12) — enables sums / indexes without recomputing.</summary>
    public decimal DurationDecimalHours { get; private set; }

    public TimeOnly? StartTime { get; private set; }

    public TimeOnly? EndTime { get; private set; }

    // ── Motive ─────────────────────────────────────────────────────────────────────────────────────
    public Guid JustificationTypePublicId { get; private set; }

    public string JustificationCodeSnapshot { get; private set; } = string.Empty;

    public string JustificationNameSnapshot { get; private set; } = string.Empty;

    public string? Observations { get; private set; }

    // ── Channel (№7) ───────────────────────────────────────────────────────────────────────────────
    public string OriginChannel { get; private set; } = OvertimeRecordChannels.Rrhh;

    // ── Plaza (D-12) ───────────────────────────────────────────────────────────────────────────────
    public Guid AssignedPositionPublicId { get; private set; }

    // ── Requester (trío) ───────────────────────────────────────────────────────────────────────────
    public Guid RequesterFilePublicId { get; private set; }

    public string RequesterNameSnapshot { get; private set; } = string.Empty;

    // ── Payroll destination ────────────────────────────────────────────────────────────────────────
    public string PayrollTypeCode { get; private set; } = string.Empty;

    /// <summary>Optional imputation to a company payroll-period instance (REQ-001 master; FK, §0.14).</summary>
    public long? PayrollPeriodId { get; private set; }

    public Guid? PayrollPeriodPublicId { get; private set; }

    public string PayrollPeriodLabel { get; private set; } = string.Empty;

    public DateOnly? PayrollPeriodEndDate { get; private set; }

    // ── Flow ───────────────────────────────────────────────────────────────────────────────────────
    public string StatusCode { get; private set; } = OvertimeRecordStatuses.EnRevision;

    public Guid RequestedByUserId { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    public DateTime? DecidedUtc { get; private set; }

    public string? DecisionNote { get; private set; }

    public Guid? AnnulledByUserId { get; private set; }

    public DateTime? AnnulledUtc { get; private set; }

    public string? AnnulmentReason { get; private set; }

    /// <summary>The settlement (finiquito) that annulled this FUTURE record so the reversal reopen is symmetric (№13/№15).</summary>
    public Guid? AnnulledBySettlementPublicId { get; private set; }

    /// <summary>The settlement (finiquito) that applied this record so the reversal reopen is symmetric (№15).</summary>
    public Guid? AppliedBySettlementPublicId { get; private set; }

    // ── Seam with compensatory time (RF-013) ─────────────────────────────────────────────────────────
    /// <summary>The compensatory-time credit that already compensated this record (populated when REQ-002 coexists).</summary>
    public Guid? CompensatedByCreditPublicId { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public IReadOnlyCollection<PersonnelFileOvertimeRecordApplication> Applications => _applications.AsReadOnly();

    /// <summary>Total duration in minutes (h × 60 + m) — feeds the daily-cap accumulation (№12).</summary>
    public int DurationTotalMinutes => (DurationHours * 60) + DurationMinutes;

    /// <summary>True when an active (APLICADA) application already exists (RN-06 — at most one).</summary>
    public bool HasActiveApplication =>
        _applications.Exists(item => item.StatusCode == OvertimeApplicationStatuses.Aplicada);

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    /// <summary>
    /// Creates an overtime record (initial status EN_REVISION). The duration is derived to decimal hours and the
    /// factor coherence (override note when the applied factor differs from the type factor) is validated by the
    /// factory (the handler pre-validates through <c>OvertimeRecordRules</c> so the guards produce clean 422s).
    /// </summary>
    public static PersonnelFileOvertimeRecord Create(
        DateOnly workDate,
        Guid overtimeTypePublicId,
        string overtimeTypeCodeSnapshot,
        string overtimeTypeNameSnapshot,
        decimal typeFactorSnapshot,
        decimal factorApplied,
        string? factorOverrideNote,
        int durationHours,
        int durationMinutes,
        TimeOnly? startTime,
        TimeOnly? endTime,
        Guid justificationTypePublicId,
        string justificationCodeSnapshot,
        string justificationNameSnapshot,
        string? observations,
        string originChannel,
        Guid assignedPositionPublicId,
        Guid requesterFilePublicId,
        string requesterNameSnapshot,
        string payrollTypeCode,
        long? payrollPeriodId,
        Guid? payrollPeriodPublicId,
        string payrollPeriodLabel,
        DateOnly? payrollPeriodEndDate,
        Guid requestedByUserId) =>
        new(
            workDate,
            overtimeTypePublicId,
            overtimeTypeCodeSnapshot,
            overtimeTypeNameSnapshot,
            typeFactorSnapshot,
            factorApplied,
            factorOverrideNote,
            durationHours,
            durationMinutes,
            startTime,
            endTime,
            justificationTypePublicId,
            justificationCodeSnapshot,
            justificationNameSnapshot,
            observations,
            originChannel,
            assignedPositionPublicId,
            requesterFilePublicId,
            requesterNameSnapshot,
            payrollTypeCode,
            payrollPeriodId,
            payrollPeriodPublicId,
            payrollPeriodLabel,
            payrollPeriodEndDate,
            requestedByUserId);

    /// <summary>Edits the shift + motive + plaza + requester + destination while EN_REVISION (RN-02).</summary>
    public void Update(
        DateOnly workDate,
        Guid overtimeTypePublicId,
        string overtimeTypeCodeSnapshot,
        string overtimeTypeNameSnapshot,
        decimal typeFactorSnapshot,
        decimal factorApplied,
        string? factorOverrideNote,
        int durationHours,
        int durationMinutes,
        TimeOnly? startTime,
        TimeOnly? endTime,
        Guid justificationTypePublicId,
        string justificationCodeSnapshot,
        string justificationNameSnapshot,
        string? observations,
        Guid assignedPositionPublicId,
        Guid requesterFilePublicId,
        string requesterNameSnapshot,
        string payrollTypeCode,
        long? payrollPeriodId,
        Guid? payrollPeriodPublicId,
        string payrollPeriodLabel,
        DateOnly? payrollPeriodEndDate)
    {
        EnsureStatus(OvertimeRecordStatuses.EnRevision, "edited");

        ApplyShift(
            workDate,
            overtimeTypePublicId,
            overtimeTypeCodeSnapshot,
            overtimeTypeNameSnapshot,
            typeFactorSnapshot,
            factorApplied,
            factorOverrideNote,
            durationHours,
            durationMinutes,
            startTime,
            endTime);
        ApplyMotive(justificationTypePublicId, justificationCodeSnapshot, justificationNameSnapshot, observations);
        ApplyPlaza(assignedPositionPublicId);
        ApplyRequester(requesterFilePublicId, requesterNameSnapshot);
        ApplyDestination(payrollTypeCode, payrollPeriodId, payrollPeriodPublicId, payrollPeriodLabel, payrollPeriodEndDate);

        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Authorizes the record (EN_REVISION → AUTORIZADA); records who decided and when.</summary>
    public void Approve(Guid decidedByUserId, DateTime atUtc)
    {
        EnsureStatus(OvertimeRecordStatuses.EnRevision, "approved");
        RequireUser(decidedByUserId, nameof(decidedByUserId));

        StatusCode = OvertimeRecordStatuses.Autorizada;
        DecidedByUserId = decidedByUserId;
        DecidedUtc = atUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Rejects the record (EN_REVISION → RECHAZADA, terminal); the decision note is mandatory.</summary>
    public void Reject(Guid decidedByUserId, DateTime atUtc, string note)
    {
        EnsureStatus(OvertimeRecordStatuses.EnRevision, "rejected");
        RequireUser(decidedByUserId, nameof(decidedByUserId));
        var normalizedNote = TruncateRequired(note, MaxDecisionNoteLength, nameof(note));

        StatusCode = OvertimeRecordStatuses.Rechazada;
        DecidedByUserId = decidedByUserId;
        DecidedUtc = atUtc;
        DecisionNote = normalizedNote;
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Annuls / revokes the record (EN_REVISION or AUTORIZADA → ANULADA, terminal); the reason is mandatory. A
    /// non-null <paramref name="settlementPublicId"/> stamps a FUTURE record that a settlement annulled, so the
    /// reversal reopen is symmetric (№13/№15). An APLICADA record must have its application reverted first (RN-06).
    /// </summary>
    public void Annul(string reason, Guid byUserId, DateTime atUtc, Guid? settlementPublicId = null)
    {
        if (StatusCode is not (OvertimeRecordStatuses.EnRevision or OvertimeRecordStatuses.Autorizada))
        {
            throw new InvalidOperationException("Only an EN_REVISION or AUTORIZADA overtime record can be annulled.");
        }

        RequireUser(byUserId, nameof(byUserId));
        var normalizedReason = TruncateRequired(reason, MaxAnnulmentReasonLength, nameof(reason));

        StatusCode = OvertimeRecordStatuses.Anulada;
        AnnulmentReason = normalizedReason;
        AnnulledByUserId = byUserId;
        AnnulledUtc = atUtc;
        AnnulledBySettlementPublicId = settlementPublicId;
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Soft-deletes an EN_REVISION draft (RF CRUD): deactivates the record without a terminal transition, so a
    /// never-authorized draft can be discarded. Only an EN_REVISION record may be deleted; an authorized record is
    /// revoked (<see cref="Annul"/>), never soft-deleted.
    /// </summary>
    public void Deactivate()
    {
        EnsureStatus(OvertimeRecordStatuses.EnRevision, "deleted");

        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Re-targets the payroll destination (payroll type + period + label + end date) while AUTORIZADA (RF-005).</summary>
    public void RetargetPeriod(
        string payrollTypeCode,
        long? payrollPeriodId,
        Guid? payrollPeriodPublicId,
        string payrollPeriodLabel,
        DateOnly? payrollPeriodEndDate,
        DateTime atUtc)
    {
        EnsureStatus(OvertimeRecordStatuses.Autorizada, "re-targeted");

        ApplyDestination(payrollTypeCode, payrollPeriodId, payrollPeriodPublicId, payrollPeriodLabel, payrollPeriodEndDate);
        _ = atUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Registers the single application of the record (RF-011). The record must be AUTORIZADA, its work date must
    /// have elapsed (<paramref name="today"/> — a future organized shift is not payable, №13) and it must not
    /// already carry an active application (RN-06); the record becomes APLICADA. Returns the created child (the
    /// caller commits through the unit of work).
    /// </summary>
    public PersonnelFileOvertimeRecordApplication Apply(
        DateOnly appliedDate,
        DateOnly today,
        string payrollTypeCode,
        long? payrollPeriodId,
        Guid? payrollPeriodPublicId,
        string? payrollPeriodLabel,
        string originCode,
        Guid appliedByUserId,
        Guid? settlementPublicId,
        string? notes)
    {
        if (StatusCode != OvertimeRecordStatuses.Autorizada)
        {
            throw new InvalidOperationException("Only an AUTORIZADA overtime record can be applied.");
        }

        if (WorkDate > today)
        {
            throw new InvalidOperationException("An overtime record with a future work date cannot be applied.");
        }

        if (HasActiveApplication)
        {
            throw new InvalidOperationException("The overtime record already has an active application.");
        }

        RequireUser(appliedByUserId, nameof(appliedByUserId));

        var application = PersonnelFileOvertimeRecordApplication.Create(
            appliedDate,
            payrollTypeCode,
            payrollPeriodId,
            payrollPeriodPublicId,
            payrollPeriodLabel,
            originCode,
            appliedByUserId,
            settlementPublicId,
            notes);

        _applications.Add(application);
        StatusCode = OvertimeRecordStatuses.Aplicada;
        ConcurrencyToken = Guid.NewGuid();
        return application;
    }

    /// <summary>
    /// Annuls the active application (RF-013); the reason is mandatory. The record returns to AUTORIZADA so a new
    /// application can be registered.
    /// </summary>
    public void AnnulApplication(Guid applicationPublicId, string reason, Guid byUserId, DateTime atUtc)
    {
        RequireUser(byUserId, nameof(byUserId));

        var application = _applications.FirstOrDefault(item =>
            item.PublicId == applicationPublicId
            && item.StatusCode == OvertimeApplicationStatuses.Aplicada);

        if (application is null)
        {
            throw new InvalidOperationException("The active application was not found on this overtime record.");
        }

        application.Annul(reason, byUserId, atUtc);

        if (StatusCode == OvertimeRecordStatuses.Aplicada)
        {
            StatusCode = OvertimeRecordStatuses.Autorizada;
        }

        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Marks an AUTORIZADA record as applied because the employee was settled (finiquito, №15). Idempotent: a
    /// no-op when the record is not AUTORIZADA (already applied / closed).
    /// </summary>
    public void MarkAppliedBySettlement(Guid settlementPublicId, DateTime atUtc)
    {
        if (settlementPublicId == Guid.Empty)
        {
            throw new ArgumentException("The settlement public id must not be empty.", nameof(settlementPublicId));
        }

        if (StatusCode != OvertimeRecordStatuses.Autorizada)
        {
            return;
        }

        StatusCode = OvertimeRecordStatuses.Aplicada;
        AppliedBySettlementPublicId = settlementPublicId;
        _ = atUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Reopens a record that a specific settlement closed when that settlement is annulled (№15): APLICADA records
    /// it applied AND FUTURE records it annulled return to AUTORIZADA. Only touches records stamped with
    /// <paramref name="settlementPublicId"/>; otherwise a no-op.
    /// </summary>
    public void ReopenFromSettlement(Guid settlementPublicId, DateTime atUtc)
    {
        if (StatusCode == OvertimeRecordStatuses.Aplicada
            && AppliedBySettlementPublicId is { } appliedBy
            && appliedBy == settlementPublicId)
        {
            StatusCode = OvertimeRecordStatuses.Autorizada;
            AppliedBySettlementPublicId = null;
            _ = atUtc;
            ConcurrencyToken = Guid.NewGuid();
            return;
        }

        if (StatusCode == OvertimeRecordStatuses.Anulada
            && AnnulledBySettlementPublicId is { } annulledBy
            && annulledBy == settlementPublicId)
        {
            StatusCode = OvertimeRecordStatuses.Autorizada;
            AnnulledBySettlementPublicId = null;
            AnnulmentReason = null;
            AnnulledByUserId = null;
            AnnulledUtc = null;
            IsActive = true;
            _ = atUtc;
            ConcurrencyToken = Guid.NewGuid();
        }
    }

    /// <summary>Links the record to the compensatory-time credit that already compensated it (RF-013).</summary>
    public void MarkCompensated(Guid creditPublicId)
    {
        if (creditPublicId == Guid.Empty)
        {
            throw new ArgumentException("The credit public id must not be empty.", nameof(creditPublicId));
        }

        CompensatedByCreditPublicId = creditPublicId;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Clears the compensatory-time link when that credit is annulled (RF-013); a no-op when it differs.</summary>
    public void ClearCompensation(Guid creditPublicId)
    {
        if (CompensatedByCreditPublicId is null || CompensatedByCreditPublicId != creditPublicId)
        {
            return;
        }

        CompensatedByCreditPublicId = null;
        ConcurrencyToken = Guid.NewGuid();
    }

    private void ApplyShift(
        DateOnly workDate,
        Guid overtimeTypePublicId,
        string overtimeTypeCodeSnapshot,
        string overtimeTypeNameSnapshot,
        decimal typeFactorSnapshot,
        decimal factorApplied,
        string? factorOverrideNote,
        int durationHours,
        int durationMinutes,
        TimeOnly? startTime,
        TimeOnly? endTime)
    {
        if (overtimeTypePublicId == Guid.Empty)
        {
            throw new ArgumentException("The overtime type reference is required.", nameof(overtimeTypePublicId));
        }

        if (typeFactorSnapshot <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(typeFactorSnapshot), "The type factor must be greater than zero.");
        }

        if (factorApplied <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(factorApplied), "The applied factor must be greater than zero.");
        }

        var normalizedNote = TruncateOptional(
            PersonnelFileNormalization.CleanOptional(factorOverrideNote), MaxFactorOverrideNoteLength, nameof(factorOverrideNote));

        // P-06: an override note is mandatory when the applied factor differs from the type's reference factor.
        if (factorApplied != typeFactorSnapshot && normalizedNote is null)
        {
            throw new ArgumentException(
                "An override note is required when the applied factor differs from the type factor.",
                nameof(factorOverrideNote));
        }

        WorkDate = workDate;
        OvertimeTypePublicId = overtimeTypePublicId;
        OvertimeTypeCodeSnapshot = TruncateRequired(overtimeTypeCodeSnapshot, MaxOvertimeTypeCodeSnapshotLength, nameof(overtimeTypeCodeSnapshot));
        OvertimeTypeNameSnapshot = TruncateRequired(overtimeTypeNameSnapshot, MaxOvertimeTypeNameSnapshotLength, nameof(overtimeTypeNameSnapshot));
        TypeFactorSnapshot = typeFactorSnapshot;
        FactorApplied = factorApplied;
        FactorOverrideNote = normalizedNote;
        DurationHours = durationHours;
        DurationMinutes = durationMinutes;
        DurationDecimalHours = DeriveDecimalHours(durationHours, durationMinutes);
        StartTime = startTime;
        EndTime = endTime;
    }

    private void ApplyMotive(
        Guid justificationTypePublicId,
        string justificationCodeSnapshot,
        string justificationNameSnapshot,
        string? observations)
    {
        if (justificationTypePublicId == Guid.Empty)
        {
            throw new ArgumentException("The justification type reference is required.", nameof(justificationTypePublicId));
        }

        JustificationTypePublicId = justificationTypePublicId;
        JustificationCodeSnapshot = TruncateRequired(justificationCodeSnapshot, MaxJustificationCodeSnapshotLength, nameof(justificationCodeSnapshot));
        JustificationNameSnapshot = TruncateRequired(justificationNameSnapshot, MaxJustificationNameSnapshotLength, nameof(justificationNameSnapshot));
        Observations = TruncateOptional(PersonnelFileNormalization.CleanOptional(observations), MaxObservationsLength, nameof(observations));
    }

    private void ApplyChannel(string originChannel)
    {
        if (originChannel is not (OvertimeRecordChannels.Rrhh or OvertimeRecordChannels.Portal))
        {
            throw new ArgumentException("The origin channel must be RRHH or PORTAL.", nameof(originChannel));
        }

        OriginChannel = originChannel;
    }

    private void ApplyPlaza(Guid assignedPositionPublicId)
    {
        if (assignedPositionPublicId == Guid.Empty)
        {
            throw new ArgumentException("The assigned position reference is required.", nameof(assignedPositionPublicId));
        }

        AssignedPositionPublicId = assignedPositionPublicId;
    }

    private void ApplyRequester(Guid requesterFilePublicId, string requesterNameSnapshot)
    {
        if (requesterFilePublicId == Guid.Empty)
        {
            throw new ArgumentException("The requester file reference is required.", nameof(requesterFilePublicId));
        }

        RequesterFilePublicId = requesterFilePublicId;
        RequesterNameSnapshot = TruncateRequired(requesterNameSnapshot, MaxRequesterNameSnapshotLength, nameof(requesterNameSnapshot));
    }

    private void ApplyDestination(
        string payrollTypeCode,
        long? payrollPeriodId,
        Guid? payrollPeriodPublicId,
        string payrollPeriodLabel,
        DateOnly? payrollPeriodEndDate)
    {
        if (payrollPeriodId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payrollPeriodId), "The payroll period id must be positive when provided.");
        }

        PayrollTypeCode = TruncateRequired(payrollTypeCode, MaxPayrollTypeCodeLength, nameof(payrollTypeCode));
        PayrollPeriodId = payrollPeriodId;
        PayrollPeriodPublicId = payrollPeriodPublicId;
        PayrollPeriodLabel = TruncateRequired(payrollPeriodLabel, MaxPayrollPeriodLabelLength, nameof(payrollPeriodLabel));
        PayrollPeriodEndDate = payrollPeriodEndDate;
    }

    /// <summary>
    /// Derives the persisted decimal hours from the h:m duration (2 h 30 m = 2.50; 0 h 45 m = 0.75), validating
    /// non-negative hours, minutes in 0–59 and a strictly positive total. Half-up away-from-zero, 2 decimals —
    /// the same single rounding rule the pure <c>OvertimeRecordRules.DeriveDecimalHours</c> applies.
    /// </summary>
    private static decimal DeriveDecimalHours(int hours, int minutes)
    {
        if (hours < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hours), "The duration hours must be zero or greater.");
        }

        if (minutes is < 0 or > 59)
        {
            throw new ArgumentOutOfRangeException(nameof(minutes), "The duration minutes must be between 0 and 59.");
        }

        if ((hours * 60) + minutes <= 0)
        {
            throw new ArgumentException("The total duration must be greater than zero.", nameof(minutes));
        }

        return Math.Round(hours + (minutes / 60m), 2, MidpointRounding.AwayFromZero);
    }

    private void EnsureStatus(string expected, string action)
    {
        if (StatusCode != expected)
        {
            throw new InvalidOperationException($"Only a {expected} overtime record can be {action}.");
        }
    }

    private static void RequireUser(Guid userId, string paramName)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("The user id must not be empty.", paramName);
        }
    }

    private static string TruncateRequired(string value, int maxLength, string paramName)
    {
        var cleaned = PersonnelFileNormalization.Clean(value, paramName);
        if (cleaned.Length > maxLength)
        {
            throw new ArgumentException($"{paramName} must be {maxLength} characters or fewer.", paramName);
        }

        return cleaned;
    }

    private static string? TruncateOptional(string? value, int maxLength, string paramName)
    {
        if (value is null)
        {
            return null;
        }

        if (value.Length > maxLength)
        {
            throw new ArgumentException($"{paramName} must be {maxLength} characters or fewer.", paramName);
        }

        return value;
    }
}

/// <summary>
/// The single application of a <see cref="PersonnelFileOvertimeRecord"/> (RF-011/RF-013). It snapshots the
/// payroll type, the (optional) payroll-period imputation with its label, the origin (MANUAL / MOTOR /
/// LIQUIDACION), the optional settlement reference and the APLICADA → ANULADA lifecycle. At most one active
/// application may exist per record (RN-06; the filtered-unique index is the final net). Created exclusively
/// through <see cref="PersonnelFileOvertimeRecord.Apply"/>.
/// </summary>
public sealed class PersonnelFileOvertimeRecordApplication : TenantEntity
{
    public const int MaxPayrollTypeCodeLength = 80;
    public const int MaxPayrollPeriodLabelLength = 80;
    public const int MaxOriginCodeLength = 20;
    public const int MaxStatusCodeLength = 20;
    public const int MaxAnnulmentReasonLength = 500;
    public const int MaxNotesLength = 500;

    private PersonnelFileOvertimeRecordApplication()
    {
    }

    private PersonnelFileOvertimeRecordApplication(
        DateOnly appliedDate,
        string payrollTypeCode,
        long? payrollPeriodId,
        Guid? payrollPeriodPublicId,
        string? payrollPeriodLabel,
        string originCode,
        Guid appliedByUserId,
        Guid? settlementPublicId,
        string? notes)
    {
        if (payrollPeriodId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payrollPeriodId), "The payroll period id must be positive when provided.");
        }

        if (originCode is not (OvertimeApplicationOrigins.Manual
            or OvertimeApplicationOrigins.Motor
            or OvertimeApplicationOrigins.Liquidacion))
        {
            throw new ArgumentException("The application origin must be MANUAL, MOTOR or LIQUIDACION.", nameof(originCode));
        }

        if (appliedByUserId == Guid.Empty)
        {
            throw new ArgumentException("The applying user id must not be empty.", nameof(appliedByUserId));
        }

        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        IsActive = true;
        StatusCode = OvertimeApplicationStatuses.Aplicada;

        AppliedDate = appliedDate;
        PayrollTypeCode = Require(payrollTypeCode, MaxPayrollTypeCodeLength, nameof(payrollTypeCode));
        PayrollPeriodId = payrollPeriodId;
        PayrollPeriodPublicId = payrollPeriodPublicId;
        PayrollPeriodLabel = Optional(PersonnelFileNormalization.CleanOptional(payrollPeriodLabel), MaxPayrollPeriodLabelLength, nameof(payrollPeriodLabel));
        OriginCode = originCode;
        AppliedByUserId = appliedByUserId;
        SettlementPublicId = settlementPublicId;
        Notes = Optional(PersonnelFileNormalization.CleanOptional(notes), MaxNotesLength, nameof(notes));
    }

    public long OvertimeRecordId { get; private set; }

    public PersonnelFileOvertimeRecord OvertimeRecord { get; private set; } = null!;

    public DateOnly AppliedDate { get; private set; }

    public string PayrollTypeCode { get; private set; } = string.Empty;

    /// <summary>Optional imputation to a company payroll-period instance (REQ-001 master; FK, §0.14).</summary>
    public long? PayrollPeriodId { get; private set; }

    public Guid? PayrollPeriodPublicId { get; private set; }

    public string? PayrollPeriodLabel { get; private set; }

    public string OriginCode { get; private set; } = OvertimeApplicationOrigins.Manual;

    public string StatusCode { get; private set; } = OvertimeApplicationStatuses.Aplicada;

    public Guid AppliedByUserId { get; private set; }

    /// <summary>The settlement (finiquito) that produced this application, when the origin is LIQUIDACION.</summary>
    public Guid? SettlementPublicId { get; private set; }

    public string? AnnulmentReason { get; private set; }

    public Guid? AnnulledByUserId { get; private set; }

    public DateTime? AnnulledUtc { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    internal static PersonnelFileOvertimeRecordApplication Create(
        DateOnly appliedDate,
        string payrollTypeCode,
        long? payrollPeriodId,
        Guid? payrollPeriodPublicId,
        string? payrollPeriodLabel,
        string originCode,
        Guid appliedByUserId,
        Guid? settlementPublicId,
        string? notes) =>
        new(
            appliedDate,
            payrollTypeCode,
            payrollPeriodId,
            payrollPeriodPublicId,
            payrollPeriodLabel,
            originCode,
            appliedByUserId,
            settlementPublicId,
            notes);

    internal void Annul(string reason, Guid byUserId, DateTime atUtc)
    {
        if (StatusCode != OvertimeApplicationStatuses.Aplicada)
        {
            throw new InvalidOperationException("Only an APLICADA application can be annulled.");
        }

        if (byUserId == Guid.Empty)
        {
            throw new ArgumentException("The user id must not be empty.", nameof(byUserId));
        }

        AnnulmentReason = Require(reason, MaxAnnulmentReasonLength, nameof(reason));
        AnnulledByUserId = byUserId;
        AnnulledUtc = atUtc;
        StatusCode = OvertimeApplicationStatuses.Anulada;
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    private static string Require(string value, int maxLength, string paramName)
    {
        var cleaned = PersonnelFileNormalization.Clean(value, paramName);
        if (cleaned.Length > maxLength)
        {
            throw new ArgumentException($"{paramName} must be {maxLength} characters or fewer.", paramName);
        }

        return cleaned;
    }

    private static string? Optional(string? value, int maxLength, string paramName)
    {
        if (value is null)
        {
            return null;
        }

        if (value.Length > maxLength)
        {
            throw new ArgumentException($"{paramName} must be {maxLength} characters or fewer.", paramName);
        }

        return value;
    }
}
