using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PersonnelFiles;

/// <summary>
/// Canonical status codes for a recurring-income record (REQ-005 D-01/D-02). The codes are validated against
/// the country-scoped <c>recurring-income-statuses</c> catalog (visualization / i18n), but the domain
/// transition logic references these constants. A record is born <see cref="EnRevision"/> and only a
/// <see cref="Vigente"/> record can apply installments (RN-08). The three <see cref="Terminal"/> states are
/// closed.
/// </summary>
public static class RecurringIncomeStatuses
{
    public const string EnRevision = "EN_REVISION";
    public const string Vigente = "VIGENTE";
    public const string Rechazado = "RECHAZADO";
    public const string Suspendido = "SUSPENDIDO";
    public const string Finalizado = "FINALIZADO";
    public const string Anulado = "ANULADO";

    /// <summary>States whose declarative fields may still be edited (RN-02): only EN_REVISION.</summary>
    public static readonly IReadOnlyCollection<string> Editable = new[] { EnRevision };

    /// <summary>States from which installments may be applied (RN-08): only VIGENTE.</summary>
    public static readonly IReadOnlyCollection<string> Applicable = new[] { Vigente };

    /// <summary>Closed states — no further transition (except the settlement reopen of a FINALIZADO).</summary>
    public static readonly IReadOnlyCollection<string> Terminal = new[] { Rechazado, Anulado, Finalizado };
}

/// <summary>What happens to the outstanding plan balance when the employee is settled (finiquito, P-06).</summary>
public static class RecurringIncomeSettlementActions
{
    public const string PagarSaldo = "PAGAR_SALDO";
    public const string Cancelar = "CANCELAR";
}

/// <summary>Origin of an applied installment: registered by hand or produced by the (future) payroll engine.</summary>
public static class RecurringIncomeInstallmentOrigins
{
    public const string Manual = "MANUAL";
    public const string Motor = "MOTOR";
}

/// <summary>Installment lifecycle — an APLICADA installment counts toward the plan; an ANULADA one does not.</summary>
public static class RecurringIncomeInstallmentStatuses
{
    public const string Aplicada = "APLICADA";
    public const string Anulada = "ANULADA";
}

/// <summary>
/// Frequency codes of the installment plan (subset of the country-scoped <c>PAY_PERIOD_CATALOG</c>) that the
/// pure projection understands. Any other code degrades to a monthly cadence in the projection.
/// </summary>
public static class RecurringIncomeFrequencies
{
    public const string Mensual = "MENSUAL";
    public const string Quincenal = "QUINCENAL";
    public const string Semanal = "SEMANAL";
    public const string Unica = "UNICA";
}

/// <summary>
/// A recurring-income agreement of a personnel file ("ingreso cíclico", REQ-005 D-01): a declarative record of
/// a compensation concept that is paid in installments (a fixed plan or an open-ended one). It holds the
/// header (registration date, reference, income type, the settled compensation concept + name snapshot), the
/// mandatory plaza + cost-center references with the cost-center name snapshot (P-15), the installment plan
/// (start date, currency, payroll type, frequency, whether it is indefinite, the per-installment value and —
/// for a finite plan — the resolved installment count + total amount, plus the settlement action) and the
/// EN_REVISION → VIGENTE → (SUSPENDIDO) → FINALIZADO lifecycle with its rejection/annulment branches. The
/// applied installments hang off the aggregate (<see cref="Installments"/>); the plan-coherence derivation and
/// the projection are pure rules (<c>RecurringIncomeRules</c>) — the handler normalizes the plan before
/// <see cref="Create"/> and computes each installment amount before <see cref="ApplyInstallment"/>. Every
/// mutation rotates <see cref="ConcurrencyToken"/>.
/// </summary>
public sealed class PersonnelFileRecurringIncome : TenantEntity
{
    public const int MaxReferenceLength = 200;
    public const int MaxRecurringIncomeTypeCodeLength = 80;
    public const int MaxConceptTypeCodeLength = 80;
    public const int MaxConceptNameSnapshotLength = 200;
    public const int MaxObservationsLength = 1000;
    public const int MaxCostCenterNameSnapshotLength = 200;
    public const int MaxCurrencyCodeLength = 3;
    public const int MaxPayrollTypeCodeLength = 80;
    public const int MaxInstallmentFrequencyCodeLength = 80;
    public const int MaxSettlementActionCodeLength = 80;
    public const int MaxStatusCodeLength = 80;
    public const int MaxDecisionNoteLength = 500;
    public const int MaxSuspensionNoteLength = 500;
    public const int MaxClosureReasonLength = 500;

    private readonly List<PersonnelFileRecurringIncomeInstallment> _installments = [];

    private PersonnelFileRecurringIncome()
    {
    }

    private PersonnelFileRecurringIncome(
        DateOnly registrationDate,
        string? reference,
        string recurringIncomeTypeCode,
        string conceptTypeCode,
        string conceptNameSnapshot,
        string? observations,
        Guid assignedPositionPublicId,
        Guid costCenterPublicId,
        string costCenterNameSnapshot,
        DateOnly installmentStartDate,
        string currencyCode,
        string payrollTypeCode,
        string installmentFrequencyCode,
        bool isIndefinite,
        decimal installmentValue,
        int? installmentCount,
        decimal? totalAmount,
        string settlementActionCode,
        Guid registeredByUserId)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        IsActive = true;
        StatusCode = RecurringIncomeStatuses.EnRevision;

        if (registeredByUserId == Guid.Empty)
        {
            throw new ArgumentException("The registering user id must not be empty.", nameof(registeredByUserId));
        }

        RegisteredByUserId = registeredByUserId;

        ApplyHeader(
            registrationDate,
            reference,
            recurringIncomeTypeCode,
            conceptTypeCode,
            conceptNameSnapshot,
            observations,
            assignedPositionPublicId,
            costCenterPublicId,
            costCenterNameSnapshot);

        ApplyPlan(
            installmentStartDate,
            currencyCode,
            payrollTypeCode,
            installmentFrequencyCode,
            isIndefinite,
            installmentValue,
            installmentCount,
            totalAmount,
            settlementActionCode);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    // ── Header ────────────────────────────────────────────────────────────────────────────────────
    public DateOnly RegistrationDate { get; private set; }

    public string? Reference { get; private set; }

    public string RecurringIncomeTypeCode { get; private set; } = string.Empty;

    public string ConceptTypeCode { get; private set; } = string.Empty;

    public string ConceptNameSnapshot { get; private set; } = string.Empty;

    public string? Observations { get; private set; }

    // ── Plaza / cost center (P-15) ──────────────────────────────────────────────────────────────────
    public Guid AssignedPositionPublicId { get; private set; }

    public Guid CostCenterPublicId { get; private set; }

    public string CostCenterNameSnapshot { get; private set; } = string.Empty;

    // ── Installment plan ────────────────────────────────────────────────────────────────────────────
    public DateOnly InstallmentStartDate { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    public string PayrollTypeCode { get; private set; } = string.Empty;

    public string InstallmentFrequencyCode { get; private set; } = string.Empty;

    public bool IsIndefinite { get; private set; }

    public decimal InstallmentValue { get; private set; }

    /// <summary>Resolved installment count (always present for a finite plan; null for an indefinite one).</summary>
    public int? InstallmentCount { get; private set; }

    /// <summary>Resolved plan total (always present for a finite plan; null for an indefinite one).</summary>
    public decimal? TotalAmount { get; private set; }

    public string SettlementActionCode { get; private set; } = string.Empty;

    // ── Flow ────────────────────────────────────────────────────────────────────────────────────────
    public string StatusCode { get; private set; } = RecurringIncomeStatuses.EnRevision;

    public Guid RegisteredByUserId { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    public DateTime? DecidedUtc { get; private set; }

    public string? DecisionNote { get; private set; }

    public DateTime? SuspendedUtc { get; private set; }

    public string? SuspensionNote { get; private set; }

    public DateTime? ClosedUtc { get; private set; }

    public string? ClosureReason { get; private set; }

    public Guid? ClosedByUserId { get; private set; }

    /// <summary>The settlement (finiquito) that finalized this income, so the anti-annul reopen is symmetric (§0.11).</summary>
    public Guid? ClosedBySettlementPublicId { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public IReadOnlyCollection<PersonnelFileRecurringIncomeInstallment> Installments => _installments.AsReadOnly();

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    /// <summary>
    /// Creates a recurring income (initial status EN_REVISION). The plan must be coherent: the value is
    /// positive, an indefinite plan carries neither count nor total, and a finite plan carries BOTH a resolved
    /// count (≥1) and a positive total (the handler normalizes them through <c>RecurringIncomeRules</c> first).
    /// </summary>
    public static PersonnelFileRecurringIncome Create(
        DateOnly registrationDate,
        string? reference,
        string recurringIncomeTypeCode,
        string conceptTypeCode,
        string conceptNameSnapshot,
        string? observations,
        Guid assignedPositionPublicId,
        Guid costCenterPublicId,
        string costCenterNameSnapshot,
        DateOnly installmentStartDate,
        string currencyCode,
        string payrollTypeCode,
        string installmentFrequencyCode,
        bool isIndefinite,
        decimal installmentValue,
        int? installmentCount,
        decimal? totalAmount,
        string settlementActionCode,
        Guid registeredByUserId) =>
        new(
            registrationDate,
            reference,
            recurringIncomeTypeCode,
            conceptTypeCode,
            conceptNameSnapshot,
            observations,
            assignedPositionPublicId,
            costCenterPublicId,
            costCenterNameSnapshot,
            installmentStartDate,
            currencyCode,
            payrollTypeCode,
            installmentFrequencyCode,
            isIndefinite,
            installmentValue,
            installmentCount,
            totalAmount,
            settlementActionCode,
            registeredByUserId);

    /// <summary>Edits the header + plan while EN_REVISION (RN-02); no other state may be edited.</summary>
    public void Update(
        DateOnly registrationDate,
        string? reference,
        string recurringIncomeTypeCode,
        string conceptTypeCode,
        string conceptNameSnapshot,
        string? observations,
        Guid assignedPositionPublicId,
        Guid costCenterPublicId,
        string costCenterNameSnapshot,
        DateOnly installmentStartDate,
        string currencyCode,
        string payrollTypeCode,
        string installmentFrequencyCode,
        bool isIndefinite,
        decimal installmentValue,
        int? installmentCount,
        decimal? totalAmount,
        string settlementActionCode)
    {
        EnsureStatus(RecurringIncomeStatuses.EnRevision, "edited");

        ApplyHeader(
            registrationDate,
            reference,
            recurringIncomeTypeCode,
            conceptTypeCode,
            conceptNameSnapshot,
            observations,
            assignedPositionPublicId,
            costCenterPublicId,
            costCenterNameSnapshot);

        ApplyPlan(
            installmentStartDate,
            currencyCode,
            payrollTypeCode,
            installmentFrequencyCode,
            isIndefinite,
            installmentValue,
            installmentCount,
            totalAmount,
            settlementActionCode);

        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Authorizes the income (EN_REVISION → VIGENTE); records who decided and when.</summary>
    public void Approve(Guid decidedByUserId, DateTime atUtc)
    {
        EnsureStatus(RecurringIncomeStatuses.EnRevision, "approved");
        RequireUser(decidedByUserId, nameof(decidedByUserId));

        StatusCode = RecurringIncomeStatuses.Vigente;
        DecidedByUserId = decidedByUserId;
        DecidedUtc = atUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Rejects the income (EN_REVISION → RECHAZADO, terminal); the decision note is mandatory.</summary>
    public void Reject(Guid decidedByUserId, DateTime atUtc, string note)
    {
        EnsureStatus(RecurringIncomeStatuses.EnRevision, "rejected");
        RequireUser(decidedByUserId, nameof(decidedByUserId));
        var normalizedNote = TruncateRequired(note, MaxDecisionNoteLength, nameof(note));

        StatusCode = RecurringIncomeStatuses.Rechazado;
        DecidedByUserId = decidedByUserId;
        DecidedUtc = atUtc;
        DecisionNote = normalizedNote;
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Suspends a VIGENTE income (VIGENTE → SUSPENDIDO); the reason is optional (P-03).</summary>
    public void Suspend(string? note, DateTime atUtc)
    {
        EnsureStatus(RecurringIncomeStatuses.Vigente, "suspended");

        StatusCode = RecurringIncomeStatuses.Suspendido;
        SuspendedUtc = atUtc;
        SuspensionNote = TruncateOptional(PersonnelFileNormalization.CleanOptional(note), MaxSuspensionNoteLength, nameof(note));
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Resumes a suspended income (SUSPENDIDO → VIGENTE).</summary>
    public void Resume(DateTime atUtc)
    {
        EnsureStatus(RecurringIncomeStatuses.Suspendido, "resumed");

        StatusCode = RecurringIncomeStatuses.Vigente;
        SuspendedUtc = null;
        SuspensionNote = null;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Annuls / revokes the income (EN_REVISION or VIGENTE → ANULADO, terminal); the reason is mandatory. The
    /// closure fields carry the terminal reason and actor.
    /// </summary>
    public void Annul(string reason, Guid byUserId, DateTime atUtc)
    {
        if (StatusCode is not (RecurringIncomeStatuses.EnRevision or RecurringIncomeStatuses.Vigente))
        {
            throw new InvalidOperationException("Only an EN_REVISION or VIGENTE recurring income can be annulled.");
        }

        RequireUser(byUserId, nameof(byUserId));
        var normalizedReason = TruncateRequired(reason, MaxClosureReasonLength, nameof(reason));

        StatusCode = RecurringIncomeStatuses.Anulado;
        ClosureReason = normalizedReason;
        ClosedByUserId = byUserId;
        ClosedUtc = atUtc;
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Soft-deletes an EN_REVISION draft (RF-002/CRUD): deactivates the record without a terminal transition, so
    /// a never-authorized draft can be discarded. Only an EN_REVISION income may be deleted; an authorized income
    /// is revoked (<see cref="Annul"/>) or closed (<see cref="CloseManually"/>), never soft-deleted.
    /// </summary>
    public void Deactivate()
    {
        EnsureStatus(RecurringIncomeStatuses.EnRevision, "deleted");

        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Closes an INDEFINITE VIGENTE income by hand (VIGENTE → FINALIZADO); the reason is mandatory (P-06).</summary>
    public void CloseManually(string reason, Guid byUserId, DateTime atUtc)
    {
        EnsureStatus(RecurringIncomeStatuses.Vigente, "closed manually");
        RequireUser(byUserId, nameof(byUserId));

        if (!IsIndefinite)
        {
            throw new InvalidOperationException("Only an indefinite recurring income can be closed manually.");
        }

        var normalizedReason = TruncateRequired(reason, MaxClosureReasonLength, nameof(reason));

        StatusCode = RecurringIncomeStatuses.Finalizado;
        ClosureReason = normalizedReason;
        ClosedByUserId = byUserId;
        ClosedUtc = atUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Finalizes a finite VIGENTE income once every installment has been applied (VIGENTE → FINALIZADO). The
    /// completion is verified against the active applied installments (RN-05).
    /// </summary>
    public void FinalizeByPlanCompletion(DateTime atUtc)
    {
        EnsureStatus(RecurringIncomeStatuses.Vigente, "finalized by plan completion");

        if (IsIndefinite || !IsFinitePlanComplete())
        {
            throw new InvalidOperationException("The installment plan is not complete.");
        }

        StatusCode = RecurringIncomeStatuses.Finalizado;
        ClosedUtc = atUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Finalizes a VIGENTE income because the employee was settled (finiquito, §0.11). Idempotent: a no-op when
    /// the income is no longer VIGENTE (another settlement already closed it).
    /// </summary>
    public void FinalizeBySettlement(Guid settlementPublicId, DateTime atUtc)
    {
        if (settlementPublicId == Guid.Empty)
        {
            throw new ArgumentException("The settlement public id must not be empty.", nameof(settlementPublicId));
        }

        if (StatusCode != RecurringIncomeStatuses.Vigente)
        {
            return;
        }

        StatusCode = RecurringIncomeStatuses.Finalizado;
        ClosedBySettlementPublicId = settlementPublicId;
        ClosedUtc = atUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Reopens an income that a specific settlement finalized (FINALIZADO → VIGENTE) when that settlement is
    /// annulled (§0.11). Only touches records closed by <paramref name="settlementPublicId"/>; otherwise a no-op.
    /// </summary>
    public void ReopenFromSettlement(Guid settlementPublicId, DateTime atUtc)
    {
        if (StatusCode != RecurringIncomeStatuses.Finalizado
            || ClosedBySettlementPublicId is null
            || ClosedBySettlementPublicId != settlementPublicId)
        {
            return;
        }

        StatusCode = RecurringIncomeStatuses.Vigente;
        ClosedBySettlementPublicId = null;
        ClosedUtc = null;
        _ = atUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Applies one installment of the plan (RF-006). The income must be VIGENTE, the number must be the next
    /// expected one (strict sequence, filling any annulled gap first) and — for a finite plan — must not exceed
    /// the installment count. The amount is computed by the handler through <c>RecurringIncomeRules</c> (not
    /// editable, P-04) and must be positive. Returns the created child (the caller commits through the unit of
    /// work); the handler finalizes the income separately if the plan becomes complete.
    /// </summary>
    public PersonnelFileRecurringIncomeInstallment ApplyInstallment(
        int installmentNumber,
        DateOnly appliedDate,
        DateOnly theoreticalDueDate,
        decimal amount,
        string currencyCode,
        string payrollTypeCode,
        long? payrollPeriodId,
        string? payrollPeriodLabel,
        string originCode,
        Guid appliedByUserId,
        string? notes)
    {
        if (StatusCode != RecurringIncomeStatuses.Vigente)
        {
            throw new InvalidOperationException("Only a VIGENTE recurring income can apply installments.");
        }

        if (installmentNumber != NextInstallmentNumber())
        {
            throw new InvalidOperationException("The installment number must be the next expected one in the plan sequence.");
        }

        if (!IsIndefinite && InstallmentCount is { } count && installmentNumber > count)
        {
            throw new InvalidOperationException("The installment number cannot exceed the finite plan count.");
        }

        RequireUser(appliedByUserId, nameof(appliedByUserId));

        var installment = PersonnelFileRecurringIncomeInstallment.Create(
            installmentNumber,
            appliedDate,
            theoreticalDueDate,
            amount,
            currencyCode,
            payrollTypeCode,
            payrollPeriodId,
            payrollPeriodLabel,
            originCode,
            appliedByUserId,
            notes);

        _installments.Add(installment);
        ConcurrencyToken = Guid.NewGuid();
        return installment;
    }

    /// <summary>
    /// Annuls an applied installment (RF-008); the reason is mandatory. If the income was FINALIZADO and the
    /// plan is no longer complete after the annulment, it is reopened to VIGENTE so the number can be re-applied.
    /// </summary>
    public void AnnulInstallment(Guid installmentPublicId, string reason, Guid byUserId, DateTime atUtc)
    {
        RequireUser(byUserId, nameof(byUserId));

        var installment = _installments.FirstOrDefault(item =>
            item.PublicId == installmentPublicId
            && item.StatusCode == RecurringIncomeInstallmentStatuses.Aplicada);

        if (installment is null)
        {
            throw new InvalidOperationException("The applied installment was not found on this recurring income.");
        }

        installment.Annul(reason, byUserId, atUtc);

        if (StatusCode == RecurringIncomeStatuses.Finalizado && !IsIndefinite && !IsFinitePlanComplete())
        {
            StatusCode = RecurringIncomeStatuses.Vigente;
            ClosedUtc = null;
            ClosedByUserId = null;
        }

        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>The next expected installment number: the smallest positive integer not currently APLICADA.</summary>
    public int NextInstallmentNumber()
    {
        var active = _installments
            .Where(item => item.StatusCode == RecurringIncomeInstallmentStatuses.Aplicada)
            .Select(item => item.InstallmentNumber)
            .ToHashSet();

        var next = 1;
        while (active.Contains(next))
        {
            next++;
        }

        return next;
    }

    private bool IsFinitePlanComplete()
    {
        if (IsIndefinite || InstallmentCount is not { } count)
        {
            return false;
        }

        var appliedCount = _installments.Count(item => item.StatusCode == RecurringIncomeInstallmentStatuses.Aplicada);
        return appliedCount >= count;
    }

    private void ApplyHeader(
        DateOnly registrationDate,
        string? reference,
        string recurringIncomeTypeCode,
        string conceptTypeCode,
        string conceptNameSnapshot,
        string? observations,
        Guid assignedPositionPublicId,
        Guid costCenterPublicId,
        string costCenterNameSnapshot)
    {
        if (assignedPositionPublicId == Guid.Empty)
        {
            throw new ArgumentException("The assigned position reference is required.", nameof(assignedPositionPublicId));
        }

        if (costCenterPublicId == Guid.Empty)
        {
            throw new ArgumentException("The cost center reference is required.", nameof(costCenterPublicId));
        }

        RegistrationDate = registrationDate;
        Reference = TruncateOptional(PersonnelFileNormalization.CleanOptional(reference), MaxReferenceLength, nameof(reference));
        RecurringIncomeTypeCode = TruncateRequired(recurringIncomeTypeCode, MaxRecurringIncomeTypeCodeLength, nameof(recurringIncomeTypeCode));
        ConceptTypeCode = TruncateRequired(conceptTypeCode, MaxConceptTypeCodeLength, nameof(conceptTypeCode));
        ConceptNameSnapshot = TruncateRequired(conceptNameSnapshot, MaxConceptNameSnapshotLength, nameof(conceptNameSnapshot));
        Observations = TruncateOptional(PersonnelFileNormalization.CleanOptional(observations), MaxObservationsLength, nameof(observations));
        AssignedPositionPublicId = assignedPositionPublicId;
        CostCenterPublicId = costCenterPublicId;
        CostCenterNameSnapshot = TruncateRequired(costCenterNameSnapshot, MaxCostCenterNameSnapshotLength, nameof(costCenterNameSnapshot));
    }

    private void ApplyPlan(
        DateOnly installmentStartDate,
        string currencyCode,
        string payrollTypeCode,
        string installmentFrequencyCode,
        bool isIndefinite,
        decimal installmentValue,
        int? installmentCount,
        decimal? totalAmount,
        string settlementActionCode)
    {
        if (installmentValue <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(installmentValue), "The installment value must be greater than zero.");
        }

        if (isIndefinite)
        {
            if (installmentCount is not null || totalAmount is not null)
            {
                throw new ArgumentException("An indefinite plan cannot carry an installment count or total amount.", nameof(isIndefinite));
            }
        }
        else
        {
            if (installmentCount is not { } count || count < 1)
            {
                throw new ArgumentException("A finite plan requires a resolved installment count of at least one.", nameof(installmentCount));
            }

            if (totalAmount is not { } total || total <= 0m)
            {
                throw new ArgumentException("A finite plan requires a positive total amount.", nameof(totalAmount));
            }
        }

        InstallmentStartDate = installmentStartDate;
        CurrencyCode = TruncateRequired(currencyCode, MaxCurrencyCodeLength, nameof(currencyCode));
        PayrollTypeCode = TruncateRequired(payrollTypeCode, MaxPayrollTypeCodeLength, nameof(payrollTypeCode));
        InstallmentFrequencyCode = TruncateRequired(installmentFrequencyCode, MaxInstallmentFrequencyCodeLength, nameof(installmentFrequencyCode));
        IsIndefinite = isIndefinite;
        InstallmentValue = installmentValue;
        InstallmentCount = isIndefinite ? null : installmentCount;
        TotalAmount = isIndefinite ? null : totalAmount;
        SettlementActionCode = TruncateRequired(settlementActionCode, MaxSettlementActionCodeLength, nameof(settlementActionCode));
    }

    private void EnsureStatus(string expected, string action)
    {
        if (StatusCode != expected)
        {
            throw new InvalidOperationException($"Only a {expected} recurring income can be {action}.");
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
/// One applied installment of a <see cref="PersonnelFileRecurringIncome"/> (RF-006/RF-008). It snapshots the
/// currency and payroll type, the (optional) payroll period imputation with its label, the origin (MANUAL /
/// MOTOR) and the APLICADA → ANULADA lifecycle. The last installment of a finite plan absorbs the rounding
/// remainder (the amount is computed by the pure rules before creation). Created exclusively through
/// <see cref="PersonnelFileRecurringIncome.ApplyInstallment"/>.
/// </summary>
public sealed class PersonnelFileRecurringIncomeInstallment : TenantEntity
{
    public const int MaxCurrencyCodeLength = 3;
    public const int MaxPayrollTypeCodeLength = 80;
    public const int MaxPayrollPeriodLabelLength = 80;
    public const int MaxOriginCodeLength = 20;
    public const int MaxStatusCodeLength = 20;
    public const int MaxAnnulmentReasonLength = 500;
    public const int MaxNotesLength = 500;

    private PersonnelFileRecurringIncomeInstallment()
    {
    }

    private PersonnelFileRecurringIncomeInstallment(
        int installmentNumber,
        DateOnly appliedDate,
        DateOnly theoreticalDueDate,
        decimal amount,
        string currencyCode,
        string payrollTypeCode,
        long? payrollPeriodId,
        string? payrollPeriodLabel,
        string originCode,
        Guid appliedByUserId,
        string? notes)
    {
        if (installmentNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(installmentNumber), "The installment number must be greater than or equal to one.");
        }

        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "The installment amount must be greater than zero.");
        }

        if (payrollPeriodId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payrollPeriodId), "The payroll period id must be positive when provided.");
        }

        if (originCode is not (RecurringIncomeInstallmentOrigins.Manual or RecurringIncomeInstallmentOrigins.Motor))
        {
            throw new ArgumentException("The installment origin must be MANUAL or MOTOR.", nameof(originCode));
        }

        if (appliedByUserId == Guid.Empty)
        {
            throw new ArgumentException("The applying user id must not be empty.", nameof(appliedByUserId));
        }

        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        IsActive = true;
        StatusCode = RecurringIncomeInstallmentStatuses.Aplicada;

        InstallmentNumber = installmentNumber;
        AppliedDate = appliedDate;
        TheoreticalDueDate = theoreticalDueDate;
        Amount = amount;
        CurrencyCode = Require(currencyCode, MaxCurrencyCodeLength, nameof(currencyCode));
        PayrollTypeCode = Require(payrollTypeCode, MaxPayrollTypeCodeLength, nameof(payrollTypeCode));
        PayrollPeriodId = payrollPeriodId;
        PayrollPeriodLabel = Optional(PersonnelFileNormalization.CleanOptional(payrollPeriodLabel), MaxPayrollPeriodLabelLength, nameof(payrollPeriodLabel));
        OriginCode = originCode;
        AppliedByUserId = appliedByUserId;
        Notes = Optional(PersonnelFileNormalization.CleanOptional(notes), MaxNotesLength, nameof(notes));
    }

    public long RecurringIncomeId { get; private set; }

    public PersonnelFileRecurringIncome RecurringIncome { get; private set; } = null!;

    public int InstallmentNumber { get; private set; }

    public DateOnly AppliedDate { get; private set; }

    public DateOnly TheoreticalDueDate { get; private set; }

    public decimal Amount { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    public string PayrollTypeCode { get; private set; } = string.Empty;

    /// <summary>Optional imputation to a company payroll-period instance (REQ-001 master; FK, §0.13).</summary>
    public long? PayrollPeriodId { get; private set; }

    public string? PayrollPeriodLabel { get; private set; }

    public string OriginCode { get; private set; } = RecurringIncomeInstallmentOrigins.Manual;

    public string StatusCode { get; private set; } = RecurringIncomeInstallmentStatuses.Aplicada;

    public Guid AppliedByUserId { get; private set; }

    public string? AnnulmentReason { get; private set; }

    public Guid? AnnulledByUserId { get; private set; }

    public DateTime? AnnulledUtc { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    internal static PersonnelFileRecurringIncomeInstallment Create(
        int installmentNumber,
        DateOnly appliedDate,
        DateOnly theoreticalDueDate,
        decimal amount,
        string currencyCode,
        string payrollTypeCode,
        long? payrollPeriodId,
        string? payrollPeriodLabel,
        string originCode,
        Guid appliedByUserId,
        string? notes) =>
        new(
            installmentNumber,
            appliedDate,
            theoreticalDueDate,
            amount,
            currencyCode,
            payrollTypeCode,
            payrollPeriodId,
            payrollPeriodLabel,
            originCode,
            appliedByUserId,
            notes);

    internal void Annul(string reason, Guid byUserId, DateTime atUtc)
    {
        if (StatusCode != RecurringIncomeInstallmentStatuses.Aplicada)
        {
            throw new InvalidOperationException("Only an APLICADA installment can be annulled.");
        }

        if (byUserId == Guid.Empty)
        {
            throw new ArgumentException("The user id must not be empty.", nameof(byUserId));
        }

        AnnulmentReason = Require(reason, MaxAnnulmentReasonLength, nameof(reason));
        AnnulledByUserId = byUserId;
        AnnulledUtc = atUtc;
        StatusCode = RecurringIncomeInstallmentStatuses.Anulada;
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
