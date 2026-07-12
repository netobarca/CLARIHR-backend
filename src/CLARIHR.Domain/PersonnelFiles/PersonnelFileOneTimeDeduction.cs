using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PersonnelFiles;

/// <summary>
/// Canonical status codes for a one-time-deduction record (REQ-009). A record is born <see cref="EnRevision"/>,
/// is decided once, and only an <see cref="Autorizado"/> one can be charged. <see cref="Aplicado"/> is
/// REVERSIBLE: annulling the application returns the deduction to AUTORIZADO so it can be charged again.
/// Exact mirror of <see cref="OneTimeIncomeStatuses"/>.
/// </summary>
public static class OneTimeDeductionStatuses
{
    public const string EnRevision = "EN_REVISION";
    public const string Autorizado = "AUTORIZADO";
    public const string Rechazado = "RECHAZADO";
    public const string Aplicado = "APLICADO";
    public const string Anulado = "ANULADO";

    /// <summary>States whose declarative fields may still be edited: only EN_REVISION.</summary>
    public static readonly IReadOnlyCollection<string> Editable = new[] { EnRevision };

    /// <summary>States from which the single application may be registered: only AUTORIZADO.</summary>
    public static readonly IReadOnlyCollection<string> Applicable = new[] { Autorizado };

    /// <summary>Closed states — no further transition.</summary>
    public static readonly IReadOnlyCollection<string> Terminal = new[] { Rechazado, Anulado };
}

/// <summary>Origin of the applied one-time deduction: registered by hand, by the (future) payroll engine or a settlement.</summary>
public static class OneTimeDeductionApplicationOrigins
{
    public const string Manual = "MANUAL";
    public const string Motor = "MOTOR";
    public const string Liquidacion = "LIQUIDACION";
}

/// <summary>Application lifecycle — an APLICADA application counts toward the deduction; an ANULADA one does not.</summary>
public static class OneTimeDeductionApplicationStatuses
{
    public const string Aplicada = "APLICADA";
    public const string Anulada = "ANULADA";
}

/// <summary>
/// How the amount of a non-fixed one-time deduction is derived: quantity × unit value × multiplier, or a
/// percentage over a base amount. The components are PERSISTED and the server recomputes the amount from them —
/// a client-supplied amount that does not match is rejected.
/// </summary>
public static class OneTimeDeductionCalculationMethods
{
    public const string QuantityTimesValue = "CANTIDAD_POR_VALOR";
    public const string PercentageOnBase = "PORCENTAJE_SOBRE_BASE";
}

/// <summary>
/// A one-off deduction of a personnel file ("descuento eventual", REQ-009): a compensation concept the company
/// charges the employee a single time (a fine, a damaged asset, an advance…). It holds the header (target date,
/// reference, the settled compensation concept — an ACTIVE, NON-STATUTORY <c>Egreso</c> concept, RN-04 — and its
/// name snapshot), the value (either a fixed amount or a computed one, with its components persisted), the
/// mandatory plaza (NO cost center — P-08, unlike the one-time income), the requester trío (file + name snapshot
/// + user), the payroll destination (payroll type + optional payroll-period imputation, re-targetable while
/// AUTORIZADO) and the EN_REVISION → AUTORIZADO → APLICADO lifecycle with its rejection/annulment branches.
/// At most ONE active application hangs off the aggregate; the value coherence is a pure rule
/// (<c>OneTimeDeductionRules</c>). Every mutation rotates <see cref="ConcurrencyToken"/>.
/// </summary>
public sealed class PersonnelFileOneTimeDeduction : TenantEntity
{
    public const int MaxReferenceLength = 200;
    public const int MaxConceptTypeCodeLength = 80;
    public const int MaxConceptNameSnapshotLength = 200;
    public const int MaxObservationsLength = 1000;
    public const int MaxCalculationMethodLength = 30;
    public const int MaxCurrencyCodeLength = 3;
    public const int MaxRequesterNameSnapshotLength = 200;
    public const int MaxPayrollTypeCodeLength = 80;
    public const int MaxPayrollPeriodLabelLength = 80;
    public const int MaxStatusCodeLength = 80;
    public const int MaxDecisionNoteLength = 500;
    public const int MaxAnnulmentReasonLength = 500;

    /// <summary>The implicit multiplier of a CANTIDAD_POR_VALOR value when none is provided.</summary>
    public const decimal DefaultMultiplier = 1.00m;

    private readonly List<PersonnelFileOneTimeDeductionApplication> _applications = [];

    private PersonnelFileOneTimeDeduction()
    {
    }

    private PersonnelFileOneTimeDeduction(
        DateOnly deductionDate,
        string? reference,
        string conceptTypeCode,
        string conceptNameSnapshot,
        string? observations,
        bool isFixedValue,
        string? calculationMethod,
        decimal? quantity,
        decimal? unitValue,
        decimal? multiplier,
        decimal? percentage,
        decimal? baseAmount,
        decimal amount,
        string currencyCode,
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
        StatusCode = OneTimeDeductionStatuses.EnRevision;

        RequireUser(requestedByUserId, nameof(requestedByUserId));
        RequestedByUserId = requestedByUserId;

        ApplyHeader(deductionDate, reference, conceptTypeCode, conceptNameSnapshot, observations);
        ApplyValue(isFixedValue, calculationMethod, quantity, unitValue, multiplier, percentage, baseAmount, amount, currencyCode);
        ApplyPlaza(assignedPositionPublicId);
        ApplyRequester(requesterFilePublicId, requesterNameSnapshot);
        ApplyDestination(payrollTypeCode, payrollPeriodId, payrollPeriodPublicId, payrollPeriodLabel, payrollPeriodEndDate);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    // ── Header ────────────────────────────────────────────────────────────────────────────────────
    /// <summary>The date the deduction targets ("fecha que será aplicado").</summary>
    public DateOnly DeductionDate { get; private set; }

    public string? Reference { get; private set; }

    public string ConceptTypeCode { get; private set; } = string.Empty;

    public string ConceptNameSnapshot { get; private set; } = string.Empty;

    public string? Observations { get; private set; }

    // ── Value ───────────────────────────────────────────────────────────────────────────────────────
    public bool IsFixedValue { get; private set; }

    public string? CalculationMethod { get; private set; }

    public decimal? Quantity { get; private set; }

    public decimal? UnitValue { get; private set; }

    public decimal? Multiplier { get; private set; }

    public decimal? Percentage { get; private set; }

    public decimal? BaseAmount { get; private set; }

    public decimal Amount { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    // ── Plaza (P-08 — NO cost center, unlike the one-time income) ────────────────────────────────────
    public Guid AssignedPositionPublicId { get; private set; }

    // ── Requester (the trío) ────────────────────────────────────────────────────────────────────────
    public Guid RequesterFilePublicId { get; private set; }

    public string RequesterNameSnapshot { get; private set; } = string.Empty;

    // ── Payroll destination (re-targetable while AUTORIZADO) ─────────────────────────────────────────
    public string PayrollTypeCode { get; private set; } = string.Empty;

    /// <summary>Optional imputation to a company payroll-period instance (REQ-001 master; FK real).</summary>
    public long? PayrollPeriodId { get; private set; }

    public Guid? PayrollPeriodPublicId { get; private set; }

    public string PayrollPeriodLabel { get; private set; } = string.Empty;

    public DateOnly? PayrollPeriodEndDate { get; private set; }

    // ── Flow ────────────────────────────────────────────────────────────────────────────────────────
    public string StatusCode { get; private set; } = OneTimeDeductionStatuses.EnRevision;

    /// <summary>The third leg of the anti-self TRIPLE: whoever REQUESTED the deduction cannot decide it.</summary>
    public Guid RequestedByUserId { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    public DateTime? DecidedUtc { get; private set; }

    public string? DecisionNote { get; private set; }

    public Guid? AnnulledByUserId { get; private set; }

    public DateTime? AnnulledUtc { get; private set; }

    public string? AnnulmentReason { get; private set; }

    /// <summary>The settlement (finiquito) that charged this deduction, so the anti-annul reopen is symmetric.</summary>
    public Guid? AppliedBySettlementPublicId { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public IReadOnlyCollection<PersonnelFileOneTimeDeductionApplication> Applications => _applications.AsReadOnly();

    /// <summary>True when an APLICADA application already exists — at most one may be active.</summary>
    public bool HasActiveApplication =>
        _applications.Any(item => item.StatusCode == OneTimeDeductionApplicationStatuses.Aplicada);

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    /// <summary>
    /// Creates a one-time deduction (initial status EN_REVISION). The value must be coherent: a fixed value carries
    /// no method or components and a positive amount; a computed value carries a method with the matching positive
    /// components (the handler validates and RECOMPUTES the amount through <c>OneTimeDeductionRules</c> first, so a
    /// client cannot smuggle an amount that does not follow from its components).
    /// </summary>
    public static PersonnelFileOneTimeDeduction Create(
        DateOnly deductionDate,
        string? reference,
        string conceptTypeCode,
        string conceptNameSnapshot,
        string? observations,
        bool isFixedValue,
        string? calculationMethod,
        decimal? quantity,
        decimal? unitValue,
        decimal? multiplier,
        decimal? percentage,
        decimal? baseAmount,
        decimal amount,
        string currencyCode,
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
            deductionDate,
            reference,
            conceptTypeCode,
            conceptNameSnapshot,
            observations,
            isFixedValue,
            calculationMethod,
            quantity,
            unitValue,
            multiplier,
            percentage,
            baseAmount,
            amount,
            currencyCode,
            assignedPositionPublicId,
            requesterFilePublicId,
            requesterNameSnapshot,
            payrollTypeCode,
            payrollPeriodId,
            payrollPeriodPublicId,
            payrollPeriodLabel,
            payrollPeriodEndDate,
            requestedByUserId);

    /// <summary>Edits the header + value + plaza + requester + destination while EN_REVISION.</summary>
    public void Update(
        DateOnly deductionDate,
        string? reference,
        string conceptTypeCode,
        string conceptNameSnapshot,
        string? observations,
        bool isFixedValue,
        string? calculationMethod,
        decimal? quantity,
        decimal? unitValue,
        decimal? multiplier,
        decimal? percentage,
        decimal? baseAmount,
        decimal amount,
        string currencyCode,
        Guid assignedPositionPublicId,
        Guid requesterFilePublicId,
        string requesterNameSnapshot,
        string payrollTypeCode,
        long? payrollPeriodId,
        Guid? payrollPeriodPublicId,
        string payrollPeriodLabel,
        DateOnly? payrollPeriodEndDate)
    {
        EnsureStatus(OneTimeDeductionStatuses.EnRevision, "edited");

        ApplyHeader(deductionDate, reference, conceptTypeCode, conceptNameSnapshot, observations);
        ApplyValue(isFixedValue, calculationMethod, quantity, unitValue, multiplier, percentage, baseAmount, amount, currencyCode);
        ApplyPlaza(assignedPositionPublicId);
        ApplyRequester(requesterFilePublicId, requesterNameSnapshot);
        ApplyDestination(payrollTypeCode, payrollPeriodId, payrollPeriodPublicId, payrollPeriodLabel, payrollPeriodEndDate);

        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Authorizes the deduction (EN_REVISION → AUTORIZADO); records who decided and when.</summary>
    public void Approve(Guid decidedByUserId, DateTime atUtc)
    {
        EnsureStatus(OneTimeDeductionStatuses.EnRevision, "approved");
        RequireUser(decidedByUserId, nameof(decidedByUserId));

        StatusCode = OneTimeDeductionStatuses.Autorizado;
        DecidedByUserId = decidedByUserId;
        DecidedUtc = atUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Rejects the deduction (EN_REVISION → RECHAZADO, terminal); the decision note is mandatory.</summary>
    public void Reject(Guid decidedByUserId, DateTime atUtc, string note)
    {
        EnsureStatus(OneTimeDeductionStatuses.EnRevision, "rejected");
        RequireUser(decidedByUserId, nameof(decidedByUserId));
        var normalizedNote = TruncateRequired(note, MaxDecisionNoteLength, nameof(note));

        StatusCode = OneTimeDeductionStatuses.Rechazado;
        DecidedByUserId = decidedByUserId;
        DecidedUtc = atUtc;
        DecisionNote = normalizedNote;
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Annuls / revokes the deduction (EN_REVISION or AUTORIZADO → ANULADO, terminal); the reason is mandatory. An
    /// APLICADO deduction must have its application reverted first.
    /// </summary>
    public void Annul(string reason, Guid byUserId, DateTime atUtc)
    {
        if (StatusCode is not (OneTimeDeductionStatuses.EnRevision or OneTimeDeductionStatuses.Autorizado))
        {
            throw new InvalidOperationException("Only an EN_REVISION or AUTORIZADO one-time deduction can be annulled.");
        }

        RequireUser(byUserId, nameof(byUserId));
        var normalizedReason = TruncateRequired(reason, MaxAnnulmentReasonLength, nameof(reason));

        StatusCode = OneTimeDeductionStatuses.Anulado;
        AnnulmentReason = normalizedReason;
        AnnulledByUserId = byUserId;
        AnnulledUtc = atUtc;
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Soft-deletes an EN_REVISION draft; an authorized deduction is revoked, never soft-deleted.</summary>
    public void Deactivate()
    {
        EnsureStatus(OneTimeDeductionStatuses.EnRevision, "deleted");

        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Re-targets the payroll destination (payroll type + period + label + end date) while AUTORIZADO.</summary>
    public void RetargetPeriod(
        string payrollTypeCode,
        long? payrollPeriodId,
        Guid? payrollPeriodPublicId,
        string payrollPeriodLabel,
        DateOnly? payrollPeriodEndDate,
        DateTime atUtc)
    {
        EnsureStatus(OneTimeDeductionStatuses.Autorizado, "re-targeted");

        ApplyDestination(payrollTypeCode, payrollPeriodId, payrollPeriodPublicId, payrollPeriodLabel, payrollPeriodEndDate);
        _ = atUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Registers the single application of the deduction. It must be AUTORIZADO and must not already carry an
    /// active application; the deduction becomes APLICADO.
    /// </summary>
    public PersonnelFileOneTimeDeductionApplication Apply(
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
        if (StatusCode != OneTimeDeductionStatuses.Autorizado)
        {
            throw new InvalidOperationException("Only an AUTORIZADO one-time deduction can be applied.");
        }

        if (HasActiveApplication)
        {
            throw new InvalidOperationException("The one-time deduction already has an active application.");
        }

        RequireUser(appliedByUserId, nameof(appliedByUserId));

        var application = PersonnelFileOneTimeDeductionApplication.Create(
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
        StatusCode = OneTimeDeductionStatuses.Aplicado;
        ConcurrencyToken = Guid.NewGuid();
        return application;
    }

    /// <summary>
    /// Annuls the active application (the REVERSAL); the reason is mandatory. The deduction returns to AUTORIZADO
    /// so a new application can be registered.
    /// </summary>
    public void AnnulApplication(Guid applicationPublicId, string reason, Guid byUserId, DateTime atUtc)
    {
        RequireUser(byUserId, nameof(byUserId));

        var application = _applications.FirstOrDefault(item =>
            item.PublicId == applicationPublicId
            && item.StatusCode == OneTimeDeductionApplicationStatuses.Aplicada);

        if (application is null)
        {
            throw new InvalidOperationException("The active application was not found on this one-time deduction.");
        }

        application.Annul(reason, byUserId, atUtc);

        if (StatusCode == OneTimeDeductionStatuses.Aplicado)
        {
            StatusCode = OneTimeDeductionStatuses.Autorizado;
        }

        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Marks an AUTORIZADO deduction as charged because the employee was settled (finiquito). Idempotent: a no-op
    /// when the deduction is not AUTORIZADO.
    /// </summary>
    public void MarkAppliedBySettlement(Guid settlementPublicId, DateTime atUtc)
    {
        if (settlementPublicId == Guid.Empty)
        {
            throw new ArgumentException("The settlement public id must not be empty.", nameof(settlementPublicId));
        }

        if (StatusCode != OneTimeDeductionStatuses.Autorizado)
        {
            return;
        }

        StatusCode = OneTimeDeductionStatuses.Aplicado;
        AppliedBySettlementPublicId = settlementPublicId;
        _ = atUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Reopens a deduction that a specific settlement charged (APLICADO → AUTORIZADO) when that settlement is
    /// annulled. Only touches records applied by <paramref name="settlementPublicId"/>; otherwise a no-op.
    /// </summary>
    public void ReopenFromSettlement(Guid settlementPublicId, DateTime atUtc)
    {
        if (StatusCode != OneTimeDeductionStatuses.Aplicado
            || AppliedBySettlementPublicId is null
            || AppliedBySettlementPublicId != settlementPublicId)
        {
            return;
        }

        StatusCode = OneTimeDeductionStatuses.Autorizado;
        AppliedBySettlementPublicId = null;
        _ = atUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    private void ApplyHeader(
        DateOnly deductionDate,
        string? reference,
        string conceptTypeCode,
        string conceptNameSnapshot,
        string? observations)
    {
        DeductionDate = deductionDate;
        Reference = TruncateOptional(PersonnelFileNormalization.CleanOptional(reference), MaxReferenceLength, nameof(reference));
        ConceptTypeCode = TruncateRequired(conceptTypeCode, MaxConceptTypeCodeLength, nameof(conceptTypeCode));
        ConceptNameSnapshot = TruncateRequired(conceptNameSnapshot, MaxConceptNameSnapshotLength, nameof(conceptNameSnapshot));
        Observations = TruncateOptional(PersonnelFileNormalization.CleanOptional(observations), MaxObservationsLength, nameof(observations));
    }

    private void ApplyValue(
        bool isFixedValue,
        string? calculationMethod,
        decimal? quantity,
        decimal? unitValue,
        decimal? multiplier,
        decimal? percentage,
        decimal? baseAmount,
        decimal amount,
        string currencyCode)
    {
        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "The amount must be greater than zero.");
        }

        if (isFixedValue)
        {
            if (calculationMethod is not null
                || quantity is not null || unitValue is not null || multiplier is not null
                || percentage is not null || baseAmount is not null)
            {
                throw new ArgumentException("A fixed-value one-time deduction cannot carry a calculation method or components.", nameof(isFixedValue));
            }

            IsFixedValue = true;
            CalculationMethod = null;
            Quantity = null;
            UnitValue = null;
            Multiplier = null;
            Percentage = null;
            BaseAmount = null;
        }
        else
        {
            var normalizedMethod = TruncateRequired(calculationMethod ?? string.Empty, MaxCalculationMethodLength, nameof(calculationMethod));

            switch (normalizedMethod)
            {
                case OneTimeDeductionCalculationMethods.QuantityTimesValue:
                {
                    if (quantity is not { } q || q <= 0m || unitValue is not { } u || u <= 0m)
                    {
                        throw new ArgumentException("A CANTIDAD_POR_VALOR value requires a positive quantity and unit value.", nameof(calculationMethod));
                    }

                    var resolvedMultiplier = multiplier ?? DefaultMultiplier;
                    if (resolvedMultiplier <= 0m)
                    {
                        throw new ArgumentException("The multiplier must be greater than zero.", nameof(multiplier));
                    }

                    if (percentage is not null || baseAmount is not null)
                    {
                        throw new ArgumentException("A CANTIDAD_POR_VALOR value cannot carry a percentage or base amount.", nameof(calculationMethod));
                    }

                    IsFixedValue = false;
                    CalculationMethod = normalizedMethod;
                    Quantity = q;
                    UnitValue = u;
                    Multiplier = resolvedMultiplier;
                    Percentage = null;
                    BaseAmount = null;
                    break;
                }

                case OneTimeDeductionCalculationMethods.PercentageOnBase:
                {
                    if (percentage is not { } p || p <= 0m || baseAmount is not { } b || b <= 0m)
                    {
                        throw new ArgumentException("A PORCENTAJE_SOBRE_BASE value requires a positive percentage and base amount.", nameof(calculationMethod));
                    }

                    if (quantity is not null || unitValue is not null || multiplier is not null)
                    {
                        throw new ArgumentException("A PORCENTAJE_SOBRE_BASE value cannot carry a quantity, unit value or multiplier.", nameof(calculationMethod));
                    }

                    IsFixedValue = false;
                    CalculationMethod = normalizedMethod;
                    Quantity = null;
                    UnitValue = null;
                    Multiplier = null;
                    Percentage = p;
                    BaseAmount = b;
                    break;
                }

                default:
                    throw new ArgumentException("The calculation method must be CANTIDAD_POR_VALOR or PORCENTAJE_SOBRE_BASE.", nameof(calculationMethod));
            }
        }

        Amount = amount;
        CurrencyCode = TruncateRequired(currencyCode, MaxCurrencyCodeLength, nameof(currencyCode));
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

    private void EnsureStatus(string expected, string action)
    {
        if (StatusCode != expected)
        {
            throw new InvalidOperationException($"Only a {expected} one-time deduction can be {action}.");
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
/// The single application of a <see cref="PersonnelFileOneTimeDeduction"/>. It snapshots the payroll type, the
/// (optional) payroll-period imputation with its label, the origin (MANUAL / MOTOR / LIQUIDACION), the optional
/// settlement reference and the APLICADA → ANULADA lifecycle. At most one active application may exist per
/// deduction (the filtered-unique index is the final net). Created exclusively through the aggregate.
/// </summary>
public sealed class PersonnelFileOneTimeDeductionApplication : TenantEntity
{
    public const int MaxPayrollTypeCodeLength = 80;
    public const int MaxPayrollPeriodLabelLength = 80;
    public const int MaxOriginCodeLength = 20;
    public const int MaxStatusCodeLength = 20;
    public const int MaxAnnulmentReasonLength = 500;
    public const int MaxNotesLength = 500;

    private PersonnelFileOneTimeDeductionApplication()
    {
    }

    private PersonnelFileOneTimeDeductionApplication(
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

        if (originCode is not (OneTimeDeductionApplicationOrigins.Manual
            or OneTimeDeductionApplicationOrigins.Motor
            or OneTimeDeductionApplicationOrigins.Liquidacion))
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
        StatusCode = OneTimeDeductionApplicationStatuses.Aplicada;

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

    public long OneTimeDeductionId { get; private set; }

    public PersonnelFileOneTimeDeduction OneTimeDeduction { get; private set; } = null!;

    public DateOnly AppliedDate { get; private set; }

    public string PayrollTypeCode { get; private set; } = string.Empty;

    /// <summary>Optional imputation to a company payroll-period instance (REQ-001 master; FK real).</summary>
    public long? PayrollPeriodId { get; private set; }

    public Guid? PayrollPeriodPublicId { get; private set; }

    public string? PayrollPeriodLabel { get; private set; }

    public string OriginCode { get; private set; } = OneTimeDeductionApplicationOrigins.Manual;

    public string StatusCode { get; private set; } = OneTimeDeductionApplicationStatuses.Aplicada;

    public Guid AppliedByUserId { get; private set; }

    /// <summary>The settlement (finiquito) that produced this application, when the origin is LIQUIDACION.</summary>
    public Guid? SettlementPublicId { get; private set; }

    public string? AnnulmentReason { get; private set; }

    public Guid? AnnulledByUserId { get; private set; }

    public DateTime? AnnulledUtc { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    internal static PersonnelFileOneTimeDeductionApplication Create(
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
        if (StatusCode != OneTimeDeductionApplicationStatuses.Aplicada)
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
        StatusCode = OneTimeDeductionApplicationStatuses.Anulada;
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
