using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PersonnelFiles;

/// <summary>
/// Canonical status codes for a recurring-deduction record (REQ-008 D-14). The codes are validated against the
/// country-scoped <c>recurring-deduction-statuses</c> catalog (visualization / i18n), but the domain transition
/// logic references these constants. A record is born <see cref="EnRevision"/> and only a <see cref="Vigente"/>
/// record can apply installments. The three <see cref="Terminal"/> states are closed. Exact mirror of
/// <see cref="RecurringIncomeStatuses"/>.
/// </summary>
public static class RecurringDeductionStatuses
{
    public const string EnRevision = "EN_REVISION";
    public const string Vigente = "VIGENTE";
    public const string Rechazado = "RECHAZADO";
    public const string Suspendido = "SUSPENDIDO";
    public const string Finalizado = "FINALIZADO";
    public const string Anulado = "ANULADO";

    /// <summary>States whose declarative fields (and plan segments) may still be edited: only EN_REVISION.</summary>
    public static readonly IReadOnlyCollection<string> Editable = new[] { EnRevision };

    /// <summary>States from which installments may be applied: only VIGENTE.</summary>
    public static readonly IReadOnlyCollection<string> Applicable = new[] { Vigente };

    /// <summary>Closed states — no further transition (except the settlement reopen of a FINALIZADO).</summary>
    public static readonly IReadOnlyCollection<string> Terminal = new[] { Rechazado, Anulado, Finalizado };
}

/// <summary>
/// What happens to the outstanding credit balance when the employee is settled (finiquito, D-12):
/// <see cref="DescontarSaldo"/> discounts it from the settlement, <see cref="Cancelar"/> writes it off
/// (condonación — no settlement line at all).
/// </summary>
public static class RecurringDeductionSettlementActions
{
    public const string DescontarSaldo = "DESCONTAR_SALDO";
    public const string Cancelar = "CANCELAR";
}

/// <summary>Origin of an applied installment: registered by hand or produced by the (future) payroll engine.</summary>
public static class RecurringDeductionInstallmentOrigins
{
    public const string Manual = "MANUAL";
    public const string Motor = "MOTOR";
}

/// <summary>Installment lifecycle — an APLICADA installment counts toward the plan; an ANULADA one does not.</summary>
public static class RecurringDeductionInstallmentStatuses
{
    public const string Aplicada = "APLICADA";
    public const string Anulada = "ANULADA";
}

/// <summary>
/// Kind of applied installment (D-09): a <see cref="Regular"/> one belongs to the numbered plan sequence; an
/// <see cref="Extraordinaria"/> one is an out-of-sequence payment (abono) that goes 100 % against capital and
/// shortens the plan.
/// </summary>
public static class RecurringDeductionInstallmentKinds
{
    public const string Regular = "REGULAR";
    public const string Extraordinaria = "EXTRAORDINARIA";
}

/// <summary>
/// Frequency codes of the installment plan (subset of the country-scoped <c>PAY_PERIOD_CATALOG</c>) that the
/// pure projection and the amortization calculator understand. Any other code degrades to a monthly cadence.
/// </summary>
public static class RecurringDeductionFrequencies
{
    public const string Mensual = "MENSUAL";
    public const string Quincenal = "QUINCENAL";
    public const string Semanal = "SEMANAL";
    public const string Unica = "UNICA";

    /// <summary>
    /// How many periods of <paramref name="frequencyCode"/> fit in a year: MENSUAL 12, QUINCENAL 24, SEMANAL 52,
    /// UNICA 1. Any other (unknown) code degrades to the monthly cadence. This is the basis of BOTH the interest
    /// period rate (P-03) and the installment/application split (D-10).
    /// </summary>
    public static int PeriodsPerYear(string? frequencyCode) =>
        (frequencyCode ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            Quincenal => 24,
            Semanal => 52,
            Unica => 1,
            _ => 12,
        };

    /// <summary>
    /// In how many CHARGES one installment is split when the application cadence is faster than the installment
    /// cadence (D-10: a MENSUAL quota applied QUINCENAL is charged as 2 halves). 1 when both match — and 1 as a
    /// safe fallback for an incoherent pair, which the rules reject at write time anyway.
    /// </summary>
    public static int ApplicationPartsPerInstallment(string? installmentFrequencyCode, string? applicationFrequencyCode)
    {
        var installmentPeriods = PeriodsPerYear(installmentFrequencyCode);
        var applicationPeriods = PeriodsPerYear(applicationFrequencyCode);

        return applicationPeriods < installmentPeriods || applicationPeriods % installmentPeriods != 0
            ? 1
            : applicationPeriods / installmentPeriods;
    }
}

/// <summary>
/// A recurring-deduction agreement of a personnel file ("descuento cíclico", REQ-008 D-01): a credit the company
/// discounts from the employee in installments. It holds the header (effective date — which may be in the FUTURE,
/// D-04 —, the mandatory credit reference, the deduction type, the settled compensation concept + name snapshot
/// and the financial institution for external types), the plaza reference (D-13 — no cost center, P-08), the
/// installment plan (start date, exception months, currency, payroll type, the installment frequency and the —
/// possibly different — APPLICATION frequency, whether it is indefinite, and the settlement action) and, for
/// credits with compound interest (D-08), the principal + nominal annual rate + planned installments.
///
/// The plan itself is expressed in one of two mutually exclusive ways: WITHOUT interest it is a list of ordered
/// <see cref="PlanSegments"/> (tramos "cuota inicial–cuota final → valor"); WITH interest there are ZERO segments
/// and the plan is DERIVED from principal + rate + count by the pure amortization calculator
/// (<c>RecurringDeductionRules</c>) — never materialized. The applied installments hang off the aggregate
/// (<see cref="Installments"/>), regular and extraordinary alike, each snapshotting its capital/interest split.
/// Every mutation rotates <see cref="ConcurrencyToken"/>.
/// </summary>
public sealed class PersonnelFileRecurringDeduction : TenantEntity
{
    public const int MaxReferenceLength = 200;
    public const int MaxRecurringDeductionTypeCodeLength = 80;
    public const int MaxConceptTypeCodeLength = 80;
    public const int MaxConceptNameSnapshotLength = 200;
    public const int MaxFinancialInstitutionLength = 200;
    public const int MaxObservationsLength = 1000;
    public const int MaxCurrencyCodeLength = 3;
    public const int MaxPayrollTypeCodeLength = 80;
    public const int MaxInstallmentFrequencyCodeLength = 80;
    public const int MaxApplicationFrequencyCodeLength = 80;
    public const int MaxSettlementActionCodeLength = 80;
    public const int MaxStatusCodeLength = 80;
    public const int MaxDecisionNoteLength = 500;
    public const int MaxSuspensionNoteLength = 500;
    public const int MaxClosureReasonLength = 500;

    /// <summary>
    /// The exception months travel as a normalized CSV of distinct sorted month numbers ("1,7,12" — plan decision
    /// №1). Native PostgreSQL arrays are NOT used anywhere in this repository, so the plan's fallback applies;
    /// the API contract stays <c>int[]</c> on both sides of the wire. 12 months × "12," = 36 chars, so 40 is safe.
    /// </summary>
    public const int MaxExceptionMonthsCsvLength = 40;

    private readonly List<PersonnelFileRecurringDeductionPlanSegment> _planSegments = [];
    private readonly List<PersonnelFileRecurringDeductionInstallment> _installments = [];
    private readonly List<PersonnelFileRecurringDeductionIndebtednessOverride> _indebtednessOverrides = [];

    private PersonnelFileRecurringDeduction()
    {
    }

    private PersonnelFileRecurringDeduction(
        DateOnly effectiveDate,
        string reference,
        string recurringDeductionTypeCode,
        string conceptTypeCode,
        string conceptNameSnapshot,
        string? financialInstitution,
        string? observations,
        Guid assignedPositionPublicId,
        DateOnly installmentStartDate,
        IEnumerable<int>? exceptionMonths,
        string currencyCode,
        string payrollTypeCode,
        string installmentFrequencyCode,
        string applicationFrequencyCode,
        bool isIndefinite,
        string settlementActionCode,
        bool usesCompoundInterest,
        decimal? principalAmount,
        decimal? interestRatePercent,
        int? plannedInstallments,
        Guid registeredByUserId)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        IsActive = true;
        StatusCode = RecurringDeductionStatuses.EnRevision;

        if (registeredByUserId == Guid.Empty)
        {
            throw new ArgumentException("The registering user id must not be empty.", nameof(registeredByUserId));
        }

        RegisteredByUserId = registeredByUserId;

        ApplyHeader(
            effectiveDate,
            reference,
            recurringDeductionTypeCode,
            conceptTypeCode,
            conceptNameSnapshot,
            financialInstitution,
            observations,
            assignedPositionPublicId);

        ApplyPlan(
            installmentStartDate,
            exceptionMonths,
            currencyCode,
            payrollTypeCode,
            installmentFrequencyCode,
            applicationFrequencyCode,
            isIndefinite,
            settlementActionCode,
            usesCompoundInterest,
            principalAmount,
            interestRatePercent,
            plannedInstallments);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    // ── Header ────────────────────────────────────────────────────────────────────────────────────────
    /// <summary>When the credit starts being enforceable. May be in the FUTURE (D-04): a future-dated credit
    /// can be registered and authorized, but no installment may be applied until the date is reached.</summary>
    public DateOnly EffectiveDate { get; private set; }

    /// <summary>The credit reference (mandatory — it is the anchor of the extraordinary payments).</summary>
    public string Reference { get; private set; } = string.Empty;

    public string RecurringDeductionTypeCode { get; private set; } = string.Empty;

    public string ConceptTypeCode { get; private set; } = string.Empty;

    public string ConceptNameSnapshot { get; private set; } = string.Empty;

    /// <summary>Free-text creditor (P-07 — no master in Fase 1). Mandatory by handler for EXTERNAL types.</summary>
    public string? FinancialInstitution { get; private set; }

    public string? Observations { get; private set; }

    // ── Plaza (D-13 — no cost center, P-08) ───────────────────────────────────────────────────────────
    public Guid AssignedPositionPublicId { get; private set; }

    // ── Installment plan ──────────────────────────────────────────────────────────────────────────────
    public DateOnly InstallmentStartDate { get; private set; }

    /// <summary>Normalized CSV of the months (1..12) the plan skips (P-05). Null = none. See
    /// <see cref="ExceptionMonths"/> for the typed view.</summary>
    public string? ExceptionMonthsCsv { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    public string PayrollTypeCode { get; private set; } = string.Empty;

    /// <summary>The cadence the installments are DUE at.</summary>
    public string InstallmentFrequencyCode { get; private set; } = string.Empty;

    /// <summary>The cadence the installments are APPLIED at (№14). When it is faster than the installment
    /// frequency the due value is split by integer division; the inverse is rejected by the rules.</summary>
    public string ApplicationFrequencyCode { get; private set; } = string.Empty;

    public bool IsIndefinite { get; private set; }

    public string SettlementActionCode { get; private set; } = string.Empty;

    // ── Compound interest (D-08) ──────────────────────────────────────────────────────────────────────
    public bool UsesCompoundInterest { get; private set; }

    public decimal? PrincipalAmount { get; private set; }

    /// <summary>NOMINAL ANNUAL rate in percent (P-03); the calculator divides it by the periods of the
    /// installment frequency (MENSUAL n/12, QUINCENAL n/24, SEMANAL n/52).</summary>
    public decimal? InterestRatePercent { get; private set; }

    public int? PlannedInstallments { get; private set; }

    // ── Flow ──────────────────────────────────────────────────────────────────────────────────────────
    public string StatusCode { get; private set; } = RecurringDeductionStatuses.EnRevision;

    public Guid RegisteredByUserId { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    public DateTime? DecidedUtc { get; private set; }

    public string? DecisionNote { get; private set; }

    public DateTime? SuspendedUtc { get; private set; }

    public string? SuspensionNote { get; private set; }

    public DateTime? ClosedUtc { get; private set; }

    public string? ClosureReason { get; private set; }

    public Guid? ClosedByUserId { get; private set; }

    /// <summary>The settlement (finiquito) that finalized this credit, so the anti-annul reopen is symmetric.</summary>
    public Guid? ClosedBySettlementPublicId { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public IReadOnlyCollection<PersonnelFileRecurringDeductionPlanSegment> PlanSegments => _planSegments.AsReadOnly();

    public IReadOnlyCollection<PersonnelFileRecurringDeductionInstallment> Installments => _installments.AsReadOnly();

    /// <summary>
    /// The indebtedness overrides confirmed on this credit (REQ-010 D-16). One row per EVENT, not a flag: the same
    /// credit can exceed the ceiling when it is registered and again when it is authorized, with different figures
    /// (the load moves in between). Each row snapshots the numbers AT THAT MOMENT — the parameters change, the
    /// footprint does not.
    /// </summary>
    public IReadOnlyCollection<PersonnelFileRecurringDeductionIndebtednessOverride> IndebtednessOverrides =>
        _indebtednessOverrides.AsReadOnly();

    /// <summary>Stamps the footprint of an indebtedness override that a user explicitly confirmed.</summary>
    public PersonnelFileRecurringDeductionIndebtednessOverride StampIndebtednessOverride(
        string stage,
        Guid acknowledgedByUserId,
        DateTime acknowledgedUtc,
        decimal baseIncome,
        decimal monthlyLoad,
        decimal newInstallment,
        decimal projectedPercent,
        decimal limitPercent,
        string limitSource)
    {
        var footprint = PersonnelFileRecurringDeductionIndebtednessOverride.Create(
            stage,
            acknowledgedByUserId,
            acknowledgedUtc,
            baseIncome,
            monthlyLoad,
            newInstallment,
            projectedPercent,
            limitPercent,
            limitSource);
        footprint.SetTenantId(TenantId);

        _indebtednessOverrides.Add(footprint);
        return footprint;
    }

    /// <summary>The months (1..12) the plan skips, decoded from <see cref="ExceptionMonthsCsv"/> (P-05).</summary>
    public IReadOnlyCollection<int> ExceptionMonths => DecodeExceptionMonths(ExceptionMonthsCsv);

    /// <summary>
    /// The number of installments the plan is made of: the planned count for a compound-interest credit, the
    /// last segment's closing installment otherwise. Null for an indefinite plan (which has no end).
    /// </summary>
    public int? PlannedInstallmentCount =>
        IsIndefinite
            ? null
            : UsesCompoundInterest
                ? PlannedInstallments
                : ActiveSegments()
                    .Select(segment => segment.ToInstallment)
                    .Max();

    /// <summary>
    /// In how many CHARGES each installment is split (D-10): 1 when the application cadence equals the
    /// installment cadence, 2 when a monthly quota is charged fortnightly, and so on.
    /// </summary>
    public int ApplicationPartsPerInstallment =>
        RecurringDeductionFrequencies.ApplicationPartsPerInstallment(InstallmentFrequencyCode, ApplicationFrequencyCode);

    /// <summary>
    /// How many CHARGES the plan is made of — the unit of the ledger (D-10 / RF-006): the installment count times
    /// the application parts. A 12-installment monthly plan charged fortnightly is 24 charges of half the quota.
    /// Null for an indefinite plan. The applied installments are numbered against THIS count, not the quota count.
    /// </summary>
    public int? PlannedChargeCount =>
        PlannedInstallmentCount is { } count ? count * ApplicationPartsPerInstallment : null;

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    /// <summary>
    /// Creates a recurring deduction (initial status EN_REVISION). The plan is coherent by construction: an
    /// interest-bearing credit carries principal + rate + count and NO segments; a plain one carries segments and
    /// no interest fields. The handler validates the segments through <c>RecurringDeductionRules</c> first and
    /// passes them here through <see cref="ReplacePlanSegments"/>.
    /// </summary>
    public static PersonnelFileRecurringDeduction Create(
        DateOnly effectiveDate,
        string reference,
        string recurringDeductionTypeCode,
        string conceptTypeCode,
        string conceptNameSnapshot,
        string? financialInstitution,
        string? observations,
        Guid assignedPositionPublicId,
        DateOnly installmentStartDate,
        IEnumerable<int>? exceptionMonths,
        string currencyCode,
        string payrollTypeCode,
        string installmentFrequencyCode,
        string applicationFrequencyCode,
        bool isIndefinite,
        string settlementActionCode,
        bool usesCompoundInterest,
        decimal? principalAmount,
        decimal? interestRatePercent,
        int? plannedInstallments,
        Guid registeredByUserId) =>
        new(
            effectiveDate,
            reference,
            recurringDeductionTypeCode,
            conceptTypeCode,
            conceptNameSnapshot,
            financialInstitution,
            observations,
            assignedPositionPublicId,
            installmentStartDate,
            exceptionMonths,
            currencyCode,
            payrollTypeCode,
            installmentFrequencyCode,
            applicationFrequencyCode,
            isIndefinite,
            settlementActionCode,
            usesCompoundInterest,
            principalAmount,
            interestRatePercent,
            plannedInstallments,
            registeredByUserId);

    /// <summary>Edits the header + plan while EN_REVISION; no other state may be edited.</summary>
    public void Update(
        DateOnly effectiveDate,
        string reference,
        string recurringDeductionTypeCode,
        string conceptTypeCode,
        string conceptNameSnapshot,
        string? financialInstitution,
        string? observations,
        Guid assignedPositionPublicId,
        DateOnly installmentStartDate,
        IEnumerable<int>? exceptionMonths,
        string currencyCode,
        string payrollTypeCode,
        string installmentFrequencyCode,
        string applicationFrequencyCode,
        bool isIndefinite,
        string settlementActionCode,
        bool usesCompoundInterest,
        decimal? principalAmount,
        decimal? interestRatePercent,
        int? plannedInstallments)
    {
        EnsureStatus(RecurringDeductionStatuses.EnRevision, "edited");

        ApplyHeader(
            effectiveDate,
            reference,
            recurringDeductionTypeCode,
            conceptTypeCode,
            conceptNameSnapshot,
            financialInstitution,
            observations,
            assignedPositionPublicId);

        ApplyPlan(
            installmentStartDate,
            exceptionMonths,
            currencyCode,
            payrollTypeCode,
            installmentFrequencyCode,
            applicationFrequencyCode,
            isIndefinite,
            settlementActionCode,
            usesCompoundInterest,
            principalAmount,
            interestRatePercent,
            plannedInstallments);

        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Replaces the whole segment list (replace-all semantics — №12). Only an EN_REVISION credit may be
    /// re-planned, and a compound-interest credit carries no segments at all (its plan is derived).
    /// The caller validates contiguity through <c>RecurringDeductionRules.ValidateSegments</c> first.
    /// </summary>
    public void ReplacePlanSegments(IEnumerable<(int FromInstallment, int? ToInstallment, decimal InstallmentValue)> segments)
    {
        EnsureStatus(RecurringDeductionStatuses.EnRevision, "re-planned");

        var incoming = segments?.ToList() ?? [];

        if (UsesCompoundInterest)
        {
            if (incoming.Count > 0)
            {
                throw new InvalidOperationException("A compound-interest credit derives its plan and cannot carry manual segments.");
            }

            _planSegments.Clear();
            ConcurrencyToken = Guid.NewGuid();
            return;
        }

        if (incoming.Count == 0)
        {
            throw new InvalidOperationException("A credit without compound interest requires at least one plan segment.");
        }

        _planSegments.Clear();
        foreach (var (from, to, value) in incoming)
        {
            _planSegments.Add(PersonnelFileRecurringDeductionPlanSegment.Create(from, to, value));
        }

        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Authorizes the credit (EN_REVISION → VIGENTE); records who decided and when.</summary>
    public void Approve(Guid decidedByUserId, DateTime atUtc)
    {
        EnsureStatus(RecurringDeductionStatuses.EnRevision, "approved");
        RequireUser(decidedByUserId, nameof(decidedByUserId));

        StatusCode = RecurringDeductionStatuses.Vigente;
        DecidedByUserId = decidedByUserId;
        DecidedUtc = atUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Rejects the credit (EN_REVISION → RECHAZADO, terminal); the decision note is mandatory.</summary>
    public void Reject(Guid decidedByUserId, DateTime atUtc, string note)
    {
        EnsureStatus(RecurringDeductionStatuses.EnRevision, "rejected");
        RequireUser(decidedByUserId, nameof(decidedByUserId));
        var normalizedNote = TruncateRequired(note, MaxDecisionNoteLength, nameof(note));

        StatusCode = RecurringDeductionStatuses.Rechazado;
        DecidedByUserId = decidedByUserId;
        DecidedUtc = atUtc;
        DecisionNote = normalizedNote;
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Suspends a VIGENTE credit (VIGENTE → SUSPENDIDO); the reason is optional.</summary>
    public void Suspend(string? note, DateTime atUtc)
    {
        EnsureStatus(RecurringDeductionStatuses.Vigente, "suspended");

        StatusCode = RecurringDeductionStatuses.Suspendido;
        SuspendedUtc = atUtc;
        SuspensionNote = TruncateOptional(PersonnelFileNormalization.CleanOptional(note), MaxSuspensionNoteLength, nameof(note));
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Resumes a suspended credit (SUSPENDIDO → VIGENTE).</summary>
    public void Resume(DateTime atUtc)
    {
        EnsureStatus(RecurringDeductionStatuses.Suspendido, "resumed");

        StatusCode = RecurringDeductionStatuses.Vigente;
        SuspendedUtc = null;
        SuspensionNote = null;
        _ = atUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Annuls / revokes the credit (EN_REVISION or VIGENTE → ANULADO, terminal); the reason is mandatory.
    /// </summary>
    public void Annul(string reason, Guid byUserId, DateTime atUtc)
    {
        if (StatusCode is not (RecurringDeductionStatuses.EnRevision or RecurringDeductionStatuses.Vigente))
        {
            throw new InvalidOperationException("Only an EN_REVISION or VIGENTE recurring deduction can be annulled.");
        }

        RequireUser(byUserId, nameof(byUserId));
        var normalizedReason = TruncateRequired(reason, MaxClosureReasonLength, nameof(reason));

        StatusCode = RecurringDeductionStatuses.Anulado;
        ClosureReason = normalizedReason;
        ClosedByUserId = byUserId;
        ClosedUtc = atUtc;
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Soft-deletes an EN_REVISION draft; an authorized credit is revoked or closed, never deleted.</summary>
    public void Deactivate()
    {
        EnsureStatus(RecurringDeductionStatuses.EnRevision, "deleted");

        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Closes an INDEFINITE VIGENTE credit by hand (VIGENTE → FINALIZADO); the reason is mandatory.</summary>
    public void CloseManually(string reason, Guid byUserId, DateTime atUtc)
    {
        EnsureStatus(RecurringDeductionStatuses.Vigente, "closed manually");
        RequireUser(byUserId, nameof(byUserId));

        if (!IsIndefinite)
        {
            throw new InvalidOperationException("Only an indefinite recurring deduction can be closed manually.");
        }

        var normalizedReason = TruncateRequired(reason, MaxClosureReasonLength, nameof(reason));

        StatusCode = RecurringDeductionStatuses.Finalizado;
        ClosureReason = normalizedReason;
        ClosedByUserId = byUserId;
        ClosedUtc = atUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Finalizes a finite VIGENTE credit once the plan is complete — every installment applied OR the balance
    /// paid off by extraordinary payments (VIGENTE → FINALIZADO).
    /// </summary>
    public void FinalizeByPlanCompletion(DateTime atUtc)
    {
        EnsureStatus(RecurringDeductionStatuses.Vigente, "finalized by plan completion");

        if (IsIndefinite || !IsPlanComplete())
        {
            throw new InvalidOperationException("The installment plan is not complete.");
        }

        StatusCode = RecurringDeductionStatuses.Finalizado;
        ClosedUtc = atUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Finalizes a VIGENTE credit because the employee was settled (finiquito). Idempotent: a no-op when the
    /// credit is no longer VIGENTE. With DESCONTAR_SALDO the balance travels as a settlement line; with CANCELAR
    /// it is written off (condonación).
    /// </summary>
    public void FinalizeBySettlement(Guid settlementPublicId, DateTime atUtc)
    {
        if (settlementPublicId == Guid.Empty)
        {
            throw new ArgumentException("The settlement public id must not be empty.", nameof(settlementPublicId));
        }

        if (StatusCode != RecurringDeductionStatuses.Vigente)
        {
            return;
        }

        StatusCode = RecurringDeductionStatuses.Finalizado;
        ClosedBySettlementPublicId = settlementPublicId;
        ClosedUtc = atUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Reopens a credit that a specific settlement finalized (FINALIZADO → VIGENTE) when that settlement is
    /// annulled. Only touches records closed by <paramref name="settlementPublicId"/>; otherwise a no-op.
    /// </summary>
    public void ReopenFromSettlement(Guid settlementPublicId, DateTime atUtc)
    {
        if (StatusCode != RecurringDeductionStatuses.Finalizado
            || ClosedBySettlementPublicId is null
            || ClosedBySettlementPublicId != settlementPublicId)
        {
            return;
        }

        StatusCode = RecurringDeductionStatuses.Vigente;
        ClosedBySettlementPublicId = null;
        ClosedUtc = null;
        _ = atUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Applies one REGULAR installment of the plan. The credit must be VIGENTE, its effective date must have been
    /// reached (D-04 — a future-dated credit cannot be charged yet; the caller checks it against "today"), the
    /// number must be the next expected one (filling any annulled gap first) and it must not exceed the planned
    /// count. The amount and its capital/interest split are computed by the pure rules (not editable).
    /// </summary>
    public PersonnelFileRecurringDeductionInstallment ApplyInstallment(
        int installmentNumber,
        DateOnly appliedDate,
        DateOnly theoreticalDueDate,
        decimal amount,
        decimal? capitalAmount,
        decimal? interestAmount,
        string currencyCode,
        string payrollTypeCode,
        long? payrollPeriodId,
        string? payrollPeriodLabel,
        string originCode,
        Guid appliedByUserId,
        string? notes)
    {
        if (StatusCode != RecurringDeductionStatuses.Vigente)
        {
            throw new InvalidOperationException("Only a VIGENTE recurring deduction can apply installments.");
        }

        if (installmentNumber != NextInstallmentNumber())
        {
            throw new InvalidOperationException("The installment number must be the next expected one in the plan sequence.");
        }

        // The ledger counts CHARGES, not quotas (D-10): a monthly quota charged fortnightly yields two rows.
        if (PlannedChargeCount is { } count && installmentNumber > count)
        {
            throw new InvalidOperationException("The installment number cannot exceed the finite plan count.");
        }

        RequireUser(appliedByUserId, nameof(appliedByUserId));

        var installment = PersonnelFileRecurringDeductionInstallment.CreateRegular(
            installmentNumber,
            appliedDate,
            theoreticalDueDate,
            amount,
            capitalAmount,
            interestAmount,
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
    /// Applies an EXTRAORDINARY installment — an out-of-sequence payment (abono) that goes 100 % against capital
    /// and shortens the plan (P-04: "reducir plazo"). Only a VIGENTE finite credit may take one (never a
    /// SUSPENDIDO one), and the value may not exceed the outstanding balance; paying exactly the balance is a
    /// payoff (the caller finalizes the credit in the same transaction).
    /// </summary>
    public PersonnelFileRecurringDeductionInstallment ApplyExtraordinaryInstallment(
        DateOnly appliedDate,
        decimal amount,
        string currencyCode,
        string payrollTypeCode,
        long? payrollPeriodId,
        string? payrollPeriodLabel,
        string originCode,
        Guid appliedByUserId,
        string? notes)
    {
        if (StatusCode != RecurringDeductionStatuses.Vigente)
        {
            throw new InvalidOperationException("Only a VIGENTE recurring deduction can take an extraordinary installment.");
        }

        if (IsIndefinite)
        {
            throw new InvalidOperationException("An indefinite recurring deduction has no balance to pay off ahead of time.");
        }

        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "The extraordinary payment must be greater than zero.");
        }

        if (amount > OutstandingBalance())
        {
            throw new InvalidOperationException("The extraordinary payment cannot exceed the outstanding balance.");
        }

        RequireUser(appliedByUserId, nameof(appliedByUserId));

        var installment = PersonnelFileRecurringDeductionInstallment.CreateExtraordinary(
            NextExtraordinaryNumber(),
            appliedDate,
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
    /// Annuls an applied installment (regular or extraordinary); the reason is mandatory. If the credit was
    /// FINALIZADO and the plan is no longer complete after the annulment, it is reopened to VIGENTE.
    /// </summary>
    public void AnnulInstallment(Guid installmentPublicId, string reason, Guid byUserId, DateTime atUtc)
    {
        RequireUser(byUserId, nameof(byUserId));

        var installment = _installments.FirstOrDefault(item =>
            item.PublicId == installmentPublicId
            && item.StatusCode == RecurringDeductionInstallmentStatuses.Aplicada);

        if (installment is null)
        {
            throw new InvalidOperationException("The applied installment was not found on this recurring deduction.");
        }

        installment.Annul(reason, byUserId, atUtc);

        if (StatusCode == RecurringDeductionStatuses.Finalizado && !IsIndefinite && !IsPlanComplete())
        {
            StatusCode = RecurringDeductionStatuses.Vigente;
            ClosedUtc = null;
            ClosedByUserId = null;
        }

        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>The next expected REGULAR installment number: the smallest positive integer not currently APLICADA.</summary>
    public int NextInstallmentNumber()
    {
        var active = ActiveInstallments()
            .Where(item => item.Kind == RecurringDeductionInstallmentKinds.Regular)
            .Select(item => item.InstallmentNumber!.Value)
            .ToHashSet();

        var next = 1;
        while (active.Contains(next))
        {
            next++;
        }

        return next;
    }

    /// <summary>The next extraordinary serial (E1, E2…): the smallest positive integer not currently APLICADA.</summary>
    public int NextExtraordinaryNumber()
    {
        var active = ActiveInstallments()
            .Where(item => item.Kind == RecurringDeductionInstallmentKinds.Extraordinaria)
            .Select(item => item.ExtraordinaryNumber!.Value)
            .ToHashSet();

        var next = 1;
        while (active.Contains(next))
        {
            next++;
        }

        return next;
    }

    /// <summary>
    /// What the employee still owes. For a compound-interest credit this is the outstanding CAPITAL (paying it
    /// off does not owe the future interest — §0.9); for a plain one it is the plan total minus everything
    /// already charged. Zero for an indefinite plan (which has no total).
    /// </summary>
    public decimal OutstandingBalance()
    {
        if (IsIndefinite)
        {
            return 0m;
        }

        if (UsesCompoundInterest)
        {
            var principal = PrincipalAmount ?? 0m;
            var capitalPaid = ActiveInstallments().Sum(item => item.CapitalAmount ?? item.Amount);
            return Math.Max(0m, principal - capitalPaid);
        }

        var total = TotalPlanAmount() ?? 0m;
        var charged = ActiveInstallments().Sum(item => item.Amount);
        return Math.Max(0m, total - charged);
    }

    /// <summary>
    /// The plan total of a plain (non-interest) finite credit: the sum over the segments of span × value. Null
    /// for an indefinite plan or a compound-interest one (whose total is derived by the amortization calculator).
    /// </summary>
    public decimal? TotalPlanAmount()
    {
        if (IsIndefinite || UsesCompoundInterest)
        {
            return null;
        }

        var total = 0m;
        foreach (var segment in ActiveSegments())
        {
            if (segment.ToInstallment is not { } to)
            {
                return null;
            }

            total += (to - segment.FromInstallment + 1) * segment.InstallmentValue;
        }

        return total;
    }

    /// <summary>
    /// Whether the finite plan is done: every planned installment applied, OR the balance fully paid off (a
    /// payoff by extraordinary payment closes the credit even with installments left in the sequence).
    /// </summary>
    public bool IsPlanComplete()
    {
        if (IsIndefinite)
        {
            return false;
        }

        if (OutstandingBalance() <= 0m)
        {
            return true;
        }

        if (PlannedChargeCount is not { } count)
        {
            return false;
        }

        var appliedRegular = ActiveInstallments()
            .Count(item => item.Kind == RecurringDeductionInstallmentKinds.Regular);

        return appliedRegular >= count;
    }

    private IEnumerable<PersonnelFileRecurringDeductionInstallment> ActiveInstallments() =>
        _installments.Where(item => item.StatusCode == RecurringDeductionInstallmentStatuses.Aplicada);

    private IEnumerable<PersonnelFileRecurringDeductionPlanSegment> ActiveSegments() =>
        _planSegments.Where(segment => segment.IsActive);

    private void ApplyHeader(
        DateOnly effectiveDate,
        string reference,
        string recurringDeductionTypeCode,
        string conceptTypeCode,
        string conceptNameSnapshot,
        string? financialInstitution,
        string? observations,
        Guid assignedPositionPublicId)
    {
        if (assignedPositionPublicId == Guid.Empty)
        {
            throw new ArgumentException("The assigned position reference is required.", nameof(assignedPositionPublicId));
        }

        EffectiveDate = effectiveDate;
        Reference = TruncateRequired(reference, MaxReferenceLength, nameof(reference));
        RecurringDeductionTypeCode = TruncateRequired(recurringDeductionTypeCode, MaxRecurringDeductionTypeCodeLength, nameof(recurringDeductionTypeCode));
        ConceptTypeCode = TruncateRequired(conceptTypeCode, MaxConceptTypeCodeLength, nameof(conceptTypeCode));
        ConceptNameSnapshot = TruncateRequired(conceptNameSnapshot, MaxConceptNameSnapshotLength, nameof(conceptNameSnapshot));
        FinancialInstitution = TruncateOptional(PersonnelFileNormalization.CleanOptional(financialInstitution), MaxFinancialInstitutionLength, nameof(financialInstitution));
        Observations = TruncateOptional(PersonnelFileNormalization.CleanOptional(observations), MaxObservationsLength, nameof(observations));
        AssignedPositionPublicId = assignedPositionPublicId;
    }

    private void ApplyPlan(
        DateOnly installmentStartDate,
        IEnumerable<int>? exceptionMonths,
        string currencyCode,
        string payrollTypeCode,
        string installmentFrequencyCode,
        string applicationFrequencyCode,
        bool isIndefinite,
        string settlementActionCode,
        bool usesCompoundInterest,
        decimal? principalAmount,
        decimal? interestRatePercent,
        int? plannedInstallments)
    {
        if (usesCompoundInterest)
        {
            if (isIndefinite)
            {
                throw new ArgumentException("A compound-interest credit requires a finite plan.", nameof(isIndefinite));
            }

            if (principalAmount is not { } principal || principal <= 0m)
            {
                throw new ArgumentException("A compound-interest credit requires a positive principal.", nameof(principalAmount));
            }

            if (interestRatePercent is not { } rate || rate <= 0m)
            {
                throw new ArgumentException("A compound-interest credit requires a positive interest rate.", nameof(interestRatePercent));
            }

            if (plannedInstallments is not { } count || count < 1)
            {
                throw new ArgumentException("A compound-interest credit requires at least one planned installment.", nameof(plannedInstallments));
            }
        }
        else
        {
            if (principalAmount is not null || interestRatePercent is not null || plannedInstallments is not null)
            {
                throw new ArgumentException("A credit without compound interest cannot carry principal, rate or planned installments.", nameof(usesCompoundInterest));
            }
        }

        InstallmentStartDate = installmentStartDate;
        ExceptionMonthsCsv = EncodeExceptionMonths(exceptionMonths);
        CurrencyCode = TruncateRequired(currencyCode, MaxCurrencyCodeLength, nameof(currencyCode));
        PayrollTypeCode = TruncateRequired(payrollTypeCode, MaxPayrollTypeCodeLength, nameof(payrollTypeCode));
        InstallmentFrequencyCode = TruncateRequired(installmentFrequencyCode, MaxInstallmentFrequencyCodeLength, nameof(installmentFrequencyCode));
        ApplicationFrequencyCode = TruncateRequired(applicationFrequencyCode, MaxApplicationFrequencyCodeLength, nameof(applicationFrequencyCode));
        IsIndefinite = isIndefinite;
        SettlementActionCode = TruncateRequired(settlementActionCode, MaxSettlementActionCodeLength, nameof(settlementActionCode));
        UsesCompoundInterest = usesCompoundInterest;
        PrincipalAmount = usesCompoundInterest ? principalAmount : null;
        InterestRatePercent = usesCompoundInterest ? interestRatePercent : null;
        PlannedInstallments = usesCompoundInterest ? plannedInstallments : null;
    }

    private static string? EncodeExceptionMonths(IEnumerable<int>? months)
    {
        if (months is null)
        {
            return null;
        }

        var normalized = months.Distinct().OrderBy(month => month).ToList();
        if (normalized.Count == 0)
        {
            return null;
        }

        if (normalized.Any(month => month is < 1 or > 12))
        {
            throw new ArgumentOutOfRangeException(nameof(months), "The exception months must be between 1 and 12.");
        }

        return string.Join(',', normalized);
    }

    private static IReadOnlyCollection<int> DecodeExceptionMonths(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(int.Parse)
                .ToList();

    private void EnsureStatus(string expected, string action)
    {
        if (StatusCode != expected)
        {
            throw new InvalidOperationException($"Only a {expected} recurring deduction can be {action}.");
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
/// One segment of a recurring-deduction plan (№12 — "cuota inicial, cuota final, valor"): installments
/// <see cref="FromInstallment"/>..<see cref="ToInstallment"/> are worth <see cref="InstallmentValue"/> each.
/// This is the DEFINITION of the plan, not an applied charge. Segments are contiguous from 1 with no gaps or
/// overlaps (validated by the pure rules); an indefinite plan has exactly one open segment
/// (<see cref="ToInstallment"/> null) and a compound-interest credit has none at all. Created exclusively
/// through <see cref="PersonnelFileRecurringDeduction.ReplacePlanSegments"/>.
/// </summary>
public sealed class PersonnelFileRecurringDeductionPlanSegment : TenantEntity
{
    private PersonnelFileRecurringDeductionPlanSegment()
    {
    }

    private PersonnelFileRecurringDeductionPlanSegment(int fromInstallment, int? toInstallment, decimal installmentValue)
    {
        if (fromInstallment < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(fromInstallment), "The first installment of a segment must be greater than or equal to one.");
        }

        if (toInstallment is { } to && to < fromInstallment)
        {
            throw new ArgumentOutOfRangeException(nameof(toInstallment), "The last installment of a segment cannot precede its first.");
        }

        if (installmentValue <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(installmentValue), "The installment value must be greater than zero.");
        }

        PublicId = Guid.NewGuid();
        IsActive = true;
        FromInstallment = fromInstallment;
        ToInstallment = toInstallment;
        InstallmentValue = installmentValue;
    }

    public long RecurringDeductionId { get; private set; }

    public PersonnelFileRecurringDeduction RecurringDeduction { get; private set; } = null!;

    public int FromInstallment { get; private set; }

    /// <summary>Null = an open segment (only legal as the single segment of an indefinite plan).</summary>
    public int? ToInstallment { get; private set; }

    public decimal InstallmentValue { get; private set; }

    public bool IsActive { get; private set; }

    internal static PersonnelFileRecurringDeductionPlanSegment Create(int fromInstallment, int? toInstallment, decimal installmentValue) =>
        new(fromInstallment, toInstallment, installmentValue);
}

/// <summary>
/// One applied installment of a <see cref="PersonnelFileRecurringDeduction"/> — REGULAR (a numbered installment
/// of the plan, with its theoretical due date) or EXTRAORDINARIA (an out-of-sequence payoff payment, serial E1,
/// E2…, 100 % capital). It snapshots the currency, payroll type and period, the origin (MANUAL / MOTOR), the
/// APLICADA → ANULADA lifecycle and — for credits with interest — the capital/interest split of the charge
/// (which is computed by the pure rules before creation, never recomputed afterwards). Created exclusively
/// through the aggregate.
/// </summary>
public sealed class PersonnelFileRecurringDeductionInstallment : TenantEntity
{
    public const int MaxKindLength = 20;
    public const int MaxCurrencyCodeLength = 3;
    public const int MaxPayrollTypeCodeLength = 80;
    public const int MaxPayrollPeriodLabelLength = 80;
    public const int MaxOriginCodeLength = 20;
    public const int MaxStatusCodeLength = 20;
    public const int MaxAnnulmentReasonLength = 500;
    public const int MaxNotesLength = 500;

    private PersonnelFileRecurringDeductionInstallment()
    {
    }

    private PersonnelFileRecurringDeductionInstallment(
        string kind,
        int? installmentNumber,
        int? extraordinaryNumber,
        DateOnly appliedDate,
        DateOnly? theoreticalDueDate,
        decimal amount,
        decimal? capitalAmount,
        decimal? interestAmount,
        string currencyCode,
        string payrollTypeCode,
        long? payrollPeriodId,
        string? payrollPeriodLabel,
        string originCode,
        Guid appliedByUserId,
        string? notes)
    {
        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "The installment amount must be greater than zero.");
        }

        if (capitalAmount is < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(capitalAmount), "The capital portion cannot be negative.");
        }

        if (interestAmount is < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(interestAmount), "The interest portion cannot be negative.");
        }

        if (payrollPeriodId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payrollPeriodId), "The payroll period id must be positive when provided.");
        }

        if (originCode is not (RecurringDeductionInstallmentOrigins.Manual or RecurringDeductionInstallmentOrigins.Motor))
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
        StatusCode = RecurringDeductionInstallmentStatuses.Aplicada;

        Kind = kind;
        InstallmentNumber = installmentNumber;
        ExtraordinaryNumber = extraordinaryNumber;
        AppliedDate = appliedDate;
        TheoreticalDueDate = theoreticalDueDate;
        Amount = amount;
        CapitalAmount = capitalAmount;
        InterestAmount = interestAmount;
        CurrencyCode = Require(currencyCode, MaxCurrencyCodeLength, nameof(currencyCode));
        PayrollTypeCode = Require(payrollTypeCode, MaxPayrollTypeCodeLength, nameof(payrollTypeCode));
        PayrollPeriodId = payrollPeriodId;
        PayrollPeriodLabel = Optional(PersonnelFileNormalization.CleanOptional(payrollPeriodLabel), MaxPayrollPeriodLabelLength, nameof(payrollPeriodLabel));
        OriginCode = originCode;
        AppliedByUserId = appliedByUserId;
        Notes = Optional(PersonnelFileNormalization.CleanOptional(notes), MaxNotesLength, nameof(notes));
    }

    public long RecurringDeductionId { get; private set; }

    public PersonnelFileRecurringDeduction RecurringDeduction { get; private set; } = null!;

    /// <summary>REGULAR or EXTRAORDINARIA (D-09).</summary>
    public string Kind { get; private set; } = RecurringDeductionInstallmentKinds.Regular;

    /// <summary>The plan sequence number — always present on a REGULAR installment, never on an extraordinary one.</summary>
    public int? InstallmentNumber { get; private set; }

    /// <summary>The extraordinary serial (E1, E2…) — always present on an EXTRAORDINARIA, never on a regular one.</summary>
    public int? ExtraordinaryNumber { get; private set; }

    public DateOnly AppliedDate { get; private set; }

    /// <summary>The date the plan said this installment was due (REGULAR only — an abono has no due date).</summary>
    public DateOnly? TheoreticalDueDate { get; private set; }

    public decimal Amount { get; private set; }

    /// <summary>The capital portion of the charge (interest-bearing credits only; an extraordinary payment is 100 % capital).</summary>
    public decimal? CapitalAmount { get; private set; }

    /// <summary>The interest portion of the charge (interest-bearing credits only).</summary>
    public decimal? InterestAmount { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    public string PayrollTypeCode { get; private set; } = string.Empty;

    /// <summary>Optional imputation to a company payroll-period instance (REQ-001 master; FK).</summary>
    public long? PayrollPeriodId { get; private set; }

    public string? PayrollPeriodLabel { get; private set; }

    public string OriginCode { get; private set; } = RecurringDeductionInstallmentOrigins.Manual;

    public string StatusCode { get; private set; } = RecurringDeductionInstallmentStatuses.Aplicada;

    public Guid AppliedByUserId { get; private set; }

    public string? AnnulmentReason { get; private set; }

    public Guid? AnnulledByUserId { get; private set; }

    public DateTime? AnnulledUtc { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    internal static PersonnelFileRecurringDeductionInstallment CreateRegular(
        int installmentNumber,
        DateOnly appliedDate,
        DateOnly theoreticalDueDate,
        decimal amount,
        decimal? capitalAmount,
        decimal? interestAmount,
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

        return new PersonnelFileRecurringDeductionInstallment(
            RecurringDeductionInstallmentKinds.Regular,
            installmentNumber,
            extraordinaryNumber: null,
            appliedDate,
            theoreticalDueDate,
            amount,
            capitalAmount,
            interestAmount,
            currencyCode,
            payrollTypeCode,
            payrollPeriodId,
            payrollPeriodLabel,
            originCode,
            appliedByUserId,
            notes);
    }

    internal static PersonnelFileRecurringDeductionInstallment CreateExtraordinary(
        int extraordinaryNumber,
        DateOnly appliedDate,
        decimal amount,
        string currencyCode,
        string payrollTypeCode,
        long? payrollPeriodId,
        string? payrollPeriodLabel,
        string originCode,
        Guid appliedByUserId,
        string? notes)
    {
        if (extraordinaryNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(extraordinaryNumber), "The extraordinary number must be greater than or equal to one.");
        }

        // An abono is 100 % capital: it never pays future interest (P-04 / §0.9).
        return new PersonnelFileRecurringDeductionInstallment(
            RecurringDeductionInstallmentKinds.Extraordinaria,
            installmentNumber: null,
            extraordinaryNumber,
            appliedDate,
            theoreticalDueDate: null,
            amount,
            capitalAmount: amount,
            interestAmount: 0m,
            currencyCode,
            payrollTypeCode,
            payrollPeriodId,
            payrollPeriodLabel,
            originCode,
            appliedByUserId,
            notes);
    }

    internal void Annul(string reason, Guid byUserId, DateTime atUtc)
    {
        if (StatusCode != RecurringDeductionInstallmentStatuses.Aplicada)
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
        StatusCode = RecurringDeductionInstallmentStatuses.Anulada;
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

/// <summary>
/// The audited footprint of an indebtedness override (REQ-010 D-16 / P-14): a user was told the deduction would
/// push the employee past the applicable ceiling, and confirmed it anyway. The levantamiento is literal — the
/// system must WARN, never BLOCK — so this row is the accountability trail of that decision.
/// </summary>
public sealed class PersonnelFileRecurringDeductionIndebtednessOverride : TenantEntity
{
    public const int MaxStageLength = 20;
    public const int MaxLimitSourceLength = 20;

    private PersonnelFileRecurringDeductionIndebtednessOverride()
    {
    }

    private PersonnelFileRecurringDeductionIndebtednessOverride(
        string stage,
        Guid acknowledgedByUserId,
        DateTime acknowledgedUtc,
        decimal baseIncome,
        decimal monthlyLoad,
        decimal newInstallment,
        decimal projectedPercent,
        decimal limitPercent,
        string limitSource)
    {
        PublicId = Guid.NewGuid();
        Stage = stage;
        AcknowledgedByUserId = acknowledgedByUserId;
        AcknowledgedUtc = acknowledgedUtc;
        BaseIncome = baseIncome;
        MonthlyLoad = monthlyLoad;
        NewInstallment = newInstallment;
        ProjectedPercent = projectedPercent;
        LimitPercent = limitPercent;
        LimitSource = limitSource;
    }

    public long RecurringDeductionId { get; private set; }

    public PersonnelFileRecurringDeduction? RecurringDeduction { get; private set; }

    /// <summary>CREACION or AUTORIZACION — where the ceiling was crossed and confirmed.</summary>
    public string Stage { get; private set; } = string.Empty;

    public Guid AcknowledgedByUserId { get; private set; }

    public DateTime AcknowledgedUtc { get; private set; }

    // The snapshot of the assessment at the moment of the confirmation. The parameters and the employee's other
    // credits will move; what was on screen when the user clicked "confirm" must not.
    public decimal BaseIncome { get; private set; }

    public decimal MonthlyLoad { get; private set; }

    public decimal NewInstallment { get; private set; }

    public decimal ProjectedPercent { get; private set; }

    public decimal LimitPercent { get; private set; }

    public string LimitSource { get; private set; } = string.Empty;

    public static PersonnelFileRecurringDeductionIndebtednessOverride Create(
        string stage,
        Guid acknowledgedByUserId,
        DateTime acknowledgedUtc,
        decimal baseIncome,
        decimal monthlyLoad,
        decimal newInstallment,
        decimal projectedPercent,
        decimal limitPercent,
        string limitSource) =>
        new(
            stage,
            acknowledgedByUserId,
            acknowledgedUtc,
            baseIncome,
            monthlyLoad,
            newInstallment,
            projectedPercent,
            limitPercent,
            limitSource);
}
