using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PersonnelFiles;

/// <summary>
/// Canonical status codes for a one-time-income record (REQ-006 D-01/D-02). The codes are validated against the
/// country-scoped <c>one-time-income-statuses</c> catalog (visualization / i18n), but the domain transition
/// logic references these constants. A record is born <see cref="EnRevision"/>, only an <see cref="Autorizado"/>
/// record can be applied (RN-06) and the application can be reverted, so <see cref="Aplicado"/> is NOT terminal
/// (the reversal returns it to <see cref="Autorizado"/>). The two <see cref="Terminal"/> states are closed.
/// </summary>
public static class OneTimeIncomeStatuses
{
    public const string EnRevision = "EN_REVISION";
    public const string Autorizado = "AUTORIZADO";
    public const string Rechazado = "RECHAZADO";
    public const string Aplicado = "APLICADO";
    public const string Anulado = "ANULADO";

    /// <summary>States whose declarative fields may still be edited (RN-02): only EN_REVISION.</summary>
    public static readonly IReadOnlyCollection<string> Editable = new[] { EnRevision };

    /// <summary>States from which the single application may be registered (RN-06): only AUTORIZADO.</summary>
    public static readonly IReadOnlyCollection<string> Applicable = new[] { Autorizado };

    /// <summary>Closed states — no further transition (APLICADO is reversible, so it is NOT terminal).</summary>
    public static readonly IReadOnlyCollection<string> Terminal = new[] { Rechazado, Anulado };
}

/// <summary>Origin of the applied one-time income: registered by hand, by the (future) payroll engine or a settlement.</summary>
public static class OneTimeIncomeApplicationOrigins
{
    public const string Manual = "MANUAL";
    public const string Motor = "MOTOR";
    public const string Liquidacion = "LIQUIDACION";
}

/// <summary>Application lifecycle — an APLICADA application counts toward the income; an ANULADA one does not.</summary>
public static class OneTimeIncomeApplicationStatuses
{
    public const string Aplicada = "APLICADA";
    public const string Anulada = "ANULADA";
}

/// <summary>
/// How the amount of a non-fixed one-time income is derived (D-07): quantity × unit value × multiplier, or a
/// percentage over a base amount.
/// </summary>
public static class OneTimeIncomeCalculationMethods
{
    public const string QuantityTimesValue = "CANTIDAD_POR_VALOR";
    public const string PercentageOnBase = "PORCENTAJE_SOBRE_BASE";
}

/// <summary>
/// A one-off income of a personnel file ("ingreso eventual", REQ-006 D-01): a declarative record of a
/// compensation concept paid a single time. It holds the header (income date, reference, settled compensation
/// concept + name snapshot, observations), the value (either a fixed amount or a computed one — quantity × unit
/// value × multiplier, or a percentage over a base — with the resolved <see cref="Amount"/>), the mandatory
/// plaza + cost-center references with the cost-center name snapshot (P-15), the requester file reference + name
/// snapshot (the trío), the payroll destination (payroll type + optional payroll-period imputation with its
/// label + end date) and the EN_REVISION → AUTORIZADO → APLICADO lifecycle with its rejection/annulment
/// branches. At most one active application hangs off the aggregate (<see cref="Applications"/>, RN-06); the
/// value coherence is a pure rule (<c>OneTimeIncomeRules</c>) — the handler validates the value and computes the
/// amount before <see cref="Create"/>. Every mutation rotates <see cref="ConcurrencyToken"/>.
/// </summary>
public sealed class PersonnelFileOneTimeIncome : TenantEntity
{
    public const int MaxReferenceLength = 200;
    public const int MaxConceptTypeCodeLength = 80;
    public const int MaxConceptNameSnapshotLength = 200;
    public const int MaxObservationsLength = 1000;
    public const int MaxCalculationMethodLength = 30;
    public const int MaxCurrencyCodeLength = 3;
    public const int MaxCostCenterNameSnapshotLength = 200;
    public const int MaxRequesterNameSnapshotLength = 200;
    public const int MaxPayrollTypeCodeLength = 80;
    public const int MaxPayrollPeriodLabelLength = 80;
    public const int MaxStatusCodeLength = 80;
    public const int MaxDecisionNoteLength = 500;
    public const int MaxAnnulmentReasonLength = 500;

    /// <summary>The implicit multiplier of a CANTIDAD_POR_VALOR value when none is provided (D-07).</summary>
    public const decimal DefaultMultiplier = 1.00m;

    private readonly List<PersonnelFileOneTimeIncomeApplication> _applications = [];

    private PersonnelFileOneTimeIncome()
    {
    }

    private PersonnelFileOneTimeIncome(
        DateOnly incomeDate,
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
        Guid costCenterPublicId,
        string costCenterNameSnapshot,
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
        StatusCode = OneTimeIncomeStatuses.EnRevision;

        RequireUser(requestedByUserId, nameof(requestedByUserId));
        RequestedByUserId = requestedByUserId;

        ApplyHeader(
            incomeDate,
            reference,
            conceptTypeCode,
            conceptNameSnapshot,
            observations);

        ApplyValue(
            isFixedValue,
            calculationMethod,
            quantity,
            unitValue,
            multiplier,
            percentage,
            baseAmount,
            amount,
            currencyCode);

        ApplyPlaza(assignedPositionPublicId, costCenterPublicId, costCenterNameSnapshot);
        ApplyRequester(requesterFilePublicId, requesterNameSnapshot);
        ApplyDestination(payrollTypeCode, payrollPeriodId, payrollPeriodPublicId, payrollPeriodLabel, payrollPeriodEndDate);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    // ── Header ────────────────────────────────────────────────────────────────────────────────────
    public DateOnly IncomeDate { get; private set; }

    public string? Reference { get; private set; }

    public string ConceptTypeCode { get; private set; } = string.Empty;

    public string ConceptNameSnapshot { get; private set; } = string.Empty;

    public string? Observations { get; private set; }

    // ── Value (D-07) ────────────────────────────────────────────────────────────────────────────────
    public bool IsFixedValue { get; private set; }

    public string? CalculationMethod { get; private set; }

    public decimal? Quantity { get; private set; }

    public decimal? UnitValue { get; private set; }

    public decimal? Multiplier { get; private set; }

    public decimal? Percentage { get; private set; }

    public decimal? BaseAmount { get; private set; }

    /// <summary>The resolved amount (a fixed value, or the computed one; always positive).</summary>
    public decimal Amount { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    // ── Plaza / cost center (P-15) ──────────────────────────────────────────────────────────────────
    public Guid AssignedPositionPublicId { get; private set; }

    public Guid CostCenterPublicId { get; private set; }

    public string CostCenterNameSnapshot { get; private set; } = string.Empty;

    // ── Requester (trío) ────────────────────────────────────────────────────────────────────────────
    public Guid RequesterFilePublicId { get; private set; }

    public string RequesterNameSnapshot { get; private set; } = string.Empty;

    // ── Payroll destination ─────────────────────────────────────────────────────────────────────────
    public string PayrollTypeCode { get; private set; } = string.Empty;

    /// <summary>Optional imputation to a company payroll-period instance (REQ-001 master; FK, §0.13).</summary>
    public long? PayrollPeriodId { get; private set; }

    public Guid? PayrollPeriodPublicId { get; private set; }

    public string PayrollPeriodLabel { get; private set; } = string.Empty;

    public DateOnly? PayrollPeriodEndDate { get; private set; }

    // ── Flow ────────────────────────────────────────────────────────────────────────────────────────
    public string StatusCode { get; private set; } = OneTimeIncomeStatuses.EnRevision;

    public Guid RequestedByUserId { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    public DateTime? DecidedUtc { get; private set; }

    public string? DecisionNote { get; private set; }

    public Guid? AnnulledByUserId { get; private set; }

    public DateTime? AnnulledUtc { get; private set; }

    public string? AnnulmentReason { get; private set; }

    /// <summary>The settlement (finiquito) that applied this income, so the reversal reopen is symmetric (§0.11).</summary>
    public Guid? AppliedBySettlementPublicId { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public IReadOnlyCollection<PersonnelFileOneTimeIncomeApplication> Applications => _applications.AsReadOnly();

    /// <summary>True when an active (APLICADA) application already exists (RN-06 — at most one).</summary>
    public bool HasActiveApplication =>
        _applications.Exists(item => item.StatusCode == OneTimeIncomeApplicationStatuses.Aplicada);

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    /// <summary>
    /// Creates a one-time income (initial status EN_REVISION). The value must be coherent: a fixed value carries
    /// no method or components and a positive amount; a computed value carries a method with the matching
    /// positive components (the handler validates and resolves the amount through <c>OneTimeIncomeRules</c>
    /// first).
    /// </summary>
    public static PersonnelFileOneTimeIncome Create(
        DateOnly incomeDate,
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
        Guid costCenterPublicId,
        string costCenterNameSnapshot,
        Guid requesterFilePublicId,
        string requesterNameSnapshot,
        string payrollTypeCode,
        long? payrollPeriodId,
        Guid? payrollPeriodPublicId,
        string payrollPeriodLabel,
        DateOnly? payrollPeriodEndDate,
        Guid requestedByUserId) =>
        new(
            incomeDate,
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
            costCenterPublicId,
            costCenterNameSnapshot,
            requesterFilePublicId,
            requesterNameSnapshot,
            payrollTypeCode,
            payrollPeriodId,
            payrollPeriodPublicId,
            payrollPeriodLabel,
            payrollPeriodEndDate,
            requestedByUserId);

    /// <summary>Edits the header + value + plaza + requester + destination while EN_REVISION (RN-02).</summary>
    public void Update(
        DateOnly incomeDate,
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
        Guid costCenterPublicId,
        string costCenterNameSnapshot,
        Guid requesterFilePublicId,
        string requesterNameSnapshot,
        string payrollTypeCode,
        long? payrollPeriodId,
        Guid? payrollPeriodPublicId,
        string payrollPeriodLabel,
        DateOnly? payrollPeriodEndDate)
    {
        EnsureStatus(OneTimeIncomeStatuses.EnRevision, "edited");

        ApplyHeader(incomeDate, reference, conceptTypeCode, conceptNameSnapshot, observations);
        ApplyValue(isFixedValue, calculationMethod, quantity, unitValue, multiplier, percentage, baseAmount, amount, currencyCode);
        ApplyPlaza(assignedPositionPublicId, costCenterPublicId, costCenterNameSnapshot);
        ApplyRequester(requesterFilePublicId, requesterNameSnapshot);
        ApplyDestination(payrollTypeCode, payrollPeriodId, payrollPeriodPublicId, payrollPeriodLabel, payrollPeriodEndDate);

        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Authorizes the income (EN_REVISION → AUTORIZADO); records who decided and when.</summary>
    public void Approve(Guid decidedByUserId, DateTime atUtc)
    {
        EnsureStatus(OneTimeIncomeStatuses.EnRevision, "approved");
        RequireUser(decidedByUserId, nameof(decidedByUserId));

        StatusCode = OneTimeIncomeStatuses.Autorizado;
        DecidedByUserId = decidedByUserId;
        DecidedUtc = atUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Rejects the income (EN_REVISION → RECHAZADO, terminal); the decision note is mandatory.</summary>
    public void Reject(Guid decidedByUserId, DateTime atUtc, string note)
    {
        EnsureStatus(OneTimeIncomeStatuses.EnRevision, "rejected");
        RequireUser(decidedByUserId, nameof(decidedByUserId));
        var normalizedNote = TruncateRequired(note, MaxDecisionNoteLength, nameof(note));

        StatusCode = OneTimeIncomeStatuses.Rechazado;
        DecidedByUserId = decidedByUserId;
        DecidedUtc = atUtc;
        DecisionNote = normalizedNote;
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Annuls / revokes the income (EN_REVISION or AUTORIZADO → ANULADO, terminal); the reason is mandatory. An
    /// APLICADO income must have its application reverted first (RN-06).
    /// </summary>
    public void Annul(string reason, Guid byUserId, DateTime atUtc)
    {
        if (StatusCode is not (OneTimeIncomeStatuses.EnRevision or OneTimeIncomeStatuses.Autorizado))
        {
            throw new InvalidOperationException("Only an EN_REVISION or AUTORIZADO one-time income can be annulled.");
        }

        RequireUser(byUserId, nameof(byUserId));
        var normalizedReason = TruncateRequired(reason, MaxAnnulmentReasonLength, nameof(reason));

        StatusCode = OneTimeIncomeStatuses.Anulado;
        AnnulmentReason = normalizedReason;
        AnnulledByUserId = byUserId;
        AnnulledUtc = atUtc;
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
        EnsureStatus(OneTimeIncomeStatuses.Autorizado, "re-targeted");

        ApplyDestination(payrollTypeCode, payrollPeriodId, payrollPeriodPublicId, payrollPeriodLabel, payrollPeriodEndDate);
        _ = atUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Registers the single application of the income (RF-011). The income must be AUTORIZADO and must not
    /// already carry an active application (RN-06); the income becomes APLICADO. Returns the created child (the
    /// caller commits through the unit of work).
    /// </summary>
    public PersonnelFileOneTimeIncomeApplication Apply(
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
        if (StatusCode != OneTimeIncomeStatuses.Autorizado)
        {
            throw new InvalidOperationException("Only an AUTORIZADO one-time income can be applied.");
        }

        if (HasActiveApplication)
        {
            throw new InvalidOperationException("The one-time income already has an active application.");
        }

        RequireUser(appliedByUserId, nameof(appliedByUserId));

        var application = PersonnelFileOneTimeIncomeApplication.Create(
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
        StatusCode = OneTimeIncomeStatuses.Aplicado;
        ConcurrencyToken = Guid.NewGuid();
        return application;
    }

    /// <summary>
    /// Annuls the active application (RF-013); the reason is mandatory. The income returns to AUTORIZADO so a new
    /// application can be registered.
    /// </summary>
    public void AnnulApplication(Guid applicationPublicId, string reason, Guid byUserId, DateTime atUtc)
    {
        RequireUser(byUserId, nameof(byUserId));

        var application = _applications.FirstOrDefault(item =>
            item.PublicId == applicationPublicId
            && item.StatusCode == OneTimeIncomeApplicationStatuses.Aplicada);

        if (application is null)
        {
            throw new InvalidOperationException("The active application was not found on this one-time income.");
        }

        application.Annul(reason, byUserId, atUtc);

        if (StatusCode == OneTimeIncomeStatuses.Aplicado)
        {
            StatusCode = OneTimeIncomeStatuses.Autorizado;
        }

        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Marks an AUTORIZADO income as applied because the employee was settled (finiquito, §0.11). Idempotent: a
    /// no-op when the income is not AUTORIZADO (already applied / closed).
    /// </summary>
    public void MarkAppliedBySettlement(Guid settlementPublicId, DateTime atUtc)
    {
        if (settlementPublicId == Guid.Empty)
        {
            throw new ArgumentException("The settlement public id must not be empty.", nameof(settlementPublicId));
        }

        if (StatusCode != OneTimeIncomeStatuses.Autorizado)
        {
            return;
        }

        StatusCode = OneTimeIncomeStatuses.Aplicado;
        AppliedBySettlementPublicId = settlementPublicId;
        _ = atUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Reopens an income that a specific settlement applied (APLICADO → AUTORIZADO) when that settlement is
    /// annulled (§0.11). Only touches records applied by <paramref name="settlementPublicId"/>; otherwise a no-op.
    /// </summary>
    public void ReopenFromSettlement(Guid settlementPublicId, DateTime atUtc)
    {
        if (StatusCode != OneTimeIncomeStatuses.Aplicado
            || AppliedBySettlementPublicId is null
            || AppliedBySettlementPublicId != settlementPublicId)
        {
            return;
        }

        StatusCode = OneTimeIncomeStatuses.Autorizado;
        AppliedBySettlementPublicId = null;
        _ = atUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    private void ApplyHeader(
        DateOnly incomeDate,
        string? reference,
        string conceptTypeCode,
        string conceptNameSnapshot,
        string? observations)
    {
        IncomeDate = incomeDate;
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
                throw new ArgumentException("A fixed-value one-time income cannot carry a calculation method or components.", nameof(isFixedValue));
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
                case OneTimeIncomeCalculationMethods.QuantityTimesValue:
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

                case OneTimeIncomeCalculationMethods.PercentageOnBase:
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

    private void ApplyPlaza(Guid assignedPositionPublicId, Guid costCenterPublicId, string costCenterNameSnapshot)
    {
        if (assignedPositionPublicId == Guid.Empty)
        {
            throw new ArgumentException("The assigned position reference is required.", nameof(assignedPositionPublicId));
        }

        if (costCenterPublicId == Guid.Empty)
        {
            throw new ArgumentException("The cost center reference is required.", nameof(costCenterPublicId));
        }

        AssignedPositionPublicId = assignedPositionPublicId;
        CostCenterPublicId = costCenterPublicId;
        CostCenterNameSnapshot = TruncateRequired(costCenterNameSnapshot, MaxCostCenterNameSnapshotLength, nameof(costCenterNameSnapshot));
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
            throw new InvalidOperationException($"Only a {expected} one-time income can be {action}.");
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
/// The single application of a <see cref="PersonnelFileOneTimeIncome"/> (RF-011/RF-013). It snapshots the
/// payroll type, the (optional) payroll-period imputation with its label, the origin (MANUAL / MOTOR /
/// LIQUIDACION), the optional settlement reference and the APLICADA → ANULADA lifecycle. At most one active
/// application may exist per income (RN-06; the filtered-unique index is the final net). Created exclusively
/// through <see cref="PersonnelFileOneTimeIncome.Apply"/>.
/// </summary>
public sealed class PersonnelFileOneTimeIncomeApplication : TenantEntity
{
    public const int MaxPayrollTypeCodeLength = 80;
    public const int MaxPayrollPeriodLabelLength = 80;
    public const int MaxOriginCodeLength = 20;
    public const int MaxStatusCodeLength = 20;
    public const int MaxAnnulmentReasonLength = 500;
    public const int MaxNotesLength = 500;

    private PersonnelFileOneTimeIncomeApplication()
    {
    }

    private PersonnelFileOneTimeIncomeApplication(
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

        if (originCode is not (OneTimeIncomeApplicationOrigins.Manual
            or OneTimeIncomeApplicationOrigins.Motor
            or OneTimeIncomeApplicationOrigins.Liquidacion))
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
        StatusCode = OneTimeIncomeApplicationStatuses.Aplicada;

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

    public long OneTimeIncomeId { get; private set; }

    public PersonnelFileOneTimeIncome OneTimeIncome { get; private set; } = null!;

    public DateOnly AppliedDate { get; private set; }

    public string PayrollTypeCode { get; private set; } = string.Empty;

    /// <summary>Optional imputation to a company payroll-period instance (REQ-001 master; FK, §0.13).</summary>
    public long? PayrollPeriodId { get; private set; }

    public Guid? PayrollPeriodPublicId { get; private set; }

    public string? PayrollPeriodLabel { get; private set; }

    public string OriginCode { get; private set; } = OneTimeIncomeApplicationOrigins.Manual;

    public string StatusCode { get; private set; } = OneTimeIncomeApplicationStatuses.Aplicada;

    public Guid AppliedByUserId { get; private set; }

    /// <summary>The settlement (finiquito) that produced this application, when the origin is LIQUIDACION.</summary>
    public Guid? SettlementPublicId { get; private set; }

    public string? AnnulmentReason { get; private set; }

    public Guid? AnnulledByUserId { get; private set; }

    public DateTime? AnnulledUtc { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    internal static PersonnelFileOneTimeIncomeApplication Create(
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
        if (StatusCode != OneTimeIncomeApplicationStatuses.Aplicada)
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
        StatusCode = OneTimeIncomeApplicationStatuses.Anulada;
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
