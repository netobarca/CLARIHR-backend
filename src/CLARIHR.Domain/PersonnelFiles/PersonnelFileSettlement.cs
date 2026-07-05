using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PersonnelFiles;

/// <summary>
/// Canonical status codes for a settlement ("liquidación de personal"). The codes are validated against the
/// country-scoped <c>settlement-statuses</c> catalog (visualization / i18n), but the domain lifecycle
/// references these constants (settlement module D-15). Scenarios (<see cref="SettlementKind.Escenario"/>)
/// have no lifecycle and carry a null status.
/// </summary>
public static class SettlementStatuses
{
    public const string Borrador = "BORRADOR";
    public const string Emitida = "EMITIDA";
    public const string Anulada = "ANULADA";
}

/// <summary>
/// Mode of a settlement record (D-02): a real settlement anchored to an executed retirement, or a
/// side-effect-free simulation over an active plaza. Persisted as a string; immutable after creation
/// (pre-development clarification №8 — converting a scenario is a Fase-2 copy, never a mutation).
/// </summary>
public enum SettlementKind
{
    Liquidacion = 0,
    Escenario = 1,
}

/// <summary>
/// Settlement ("liquidación de personal") of ONE plaza of an employee (D-10, ratified: per-plaza
/// granularity). A real settlement (<see cref="SettlementKind.Liquidacion"/>) anchors to an EJECUTADA
/// retirement request and to one assignment that retirement closed; a scenario
/// (<see cref="SettlementKind.Escenario"/>) simulates over an active assignment with an estimated date and
/// produces no side effects. The record snapshots the legal parameters (RF-011 — they change every year),
/// the salary/seniority bases and the five-section totals; the detail lives in
/// <see cref="PersonnelFileSettlementLine"/> rows. Lifecycle (real only): BORRADOR → EMITIDA → ANULADA
/// (D-15); only BORRADOR is editable and EMITIDA is immutable (annul + recreate to correct).
/// </summary>
public sealed class PersonnelFileSettlement : TenantEntity
{
    private readonly List<PersonnelFileSettlementLine> _lines = [];

    private PersonnelFileSettlement()
    {
    }

    private PersonnelFileSettlement(
        SettlementKind kind,
        Guid? retirementRequestPublicId,
        Guid assignedPositionPublicId,
        string? positionNameSnapshot,
        DateTime plazaStartDate,
        Guid? costCenterPublicId,
        string? costCenterNameSnapshot,
        DateTime retirementDate,
        string retirementCategoryCode,
        string? retirementCategoryNameSnapshot,
        string retirementReasonCode,
        string? retirementReasonNameSnapshot,
        Guid requesterFilePublicId,
        string requesterNameSnapshot,
        DateTime requestDate,
        string? notes,
        Guid requestedByUserId,
        string currencyCode)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        Kind = kind;
        StatusCode = kind == SettlementKind.Liquidacion ? SettlementStatuses.Borrador : null;
        RetirementRequestPublicId = retirementRequestPublicId;
        RequestedByUserId = requestedByUserId;
        CurrencyCode = PersonnelFileNormalization.Clean(currencyCode, nameof(currencyCode)).ToUpperInvariant();
        ApplyPosition(assignedPositionPublicId, positionNameSnapshot, plazaStartDate, costCenterPublicId, costCenterNameSnapshot);
        ApplyRetirementReason(retirementDate, retirementCategoryCode, retirementCategoryNameSnapshot, retirementReasonCode, retirementReasonNameSnapshot);
        ApplyHeader(requesterFilePublicId, requesterNameSnapshot, requestDate, notes);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public SettlementKind Kind { get; private set; }

    // Anchor to the executed retirement (D-03) — required on a real settlement, null on a scenario.
    public long? RetirementRequestId { get; private set; }

    public Guid? RetirementRequestPublicId { get; private set; }

    // The plaza this settlement values (D-10): a closed assignment of the retirement (real) or an active
    // one (scenario). PlazaStartDate anchors the per-plaza seniority (P-01, ratified: "desde el StartDate").
    public Guid AssignedPositionPublicId { get; private set; }

    public string? PositionNameSnapshot { get; private set; }

    public DateTime PlazaStartDate { get; private set; }

    // Cost center of the plaza — destination of the reserve/provision (D-13); null = no cost center (warning).
    public Guid? CostCenterPublicId { get; private set; }

    public string? CostCenterNameSnapshot { get; private set; }

    // Retirement facts: inherited read-only from the executed request (real) or hypothetical (scenario).
    public DateTime RetirementDate { get; private set; }

    public string RetirementCategoryCode { get; private set; } = string.Empty;

    public string? RetirementCategoryNameSnapshot { get; private set; }

    public string RetirementReasonCode { get; private set; } = string.Empty;

    public string? RetirementReasonNameSnapshot { get; private set; }

    // Requester ("solicitante", D-06 hardened: HR only — the handlers validate it) + audit of who typed it.
    public Guid RequesterFilePublicId { get; private set; }

    public string RequesterNameSnapshot { get; private set; } = string.Empty;

    public DateTime RequestDate { get; private set; }

    public string? Notes { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    // Lifecycle (real settlements only — D-15); null on scenarios.
    public string? StatusCode { get; private set; }

    // ── Legal parameters snapshot (RF-011): captured per settlement, never rewritten by later default changes.
    public decimal MinimumMonthlyWage { get; private set; }

    public decimal IndemnityCapMultiplier { get; private set; }

    public decimal ResignationCapMultiplier { get; private set; }

    public decimal VacationDays { get; private set; }

    public decimal VacationPremiumPercent { get; private set; }

    /// <summary>Aguinaldo days per the seniority tier (15/19/21); the engine computes it and an override may fix it.</summary>
    public decimal AguinaldoDays { get; private set; }

    public decimal ResignationBenefitDays { get; private set; }

    public int ResignationMinimumServiceYears { get; private set; }

    public decimal AguinaldoExemptionMultiplier { get; private set; }

    public int MonthDivisorDays { get; private set; }

    public int YearDivisorDays { get; private set; }

    // ── Calculation bases snapshot (derived by the engine, D-09/D-10/P-01).
    public decimal MonthlyBaseSalary { get; private set; }

    public int SeniorityYears { get; private set; }

    public int SeniorityDays { get; private set; }

    public decimal CappedMonthlySalaryIndemnity { get; private set; }

    public decimal CappedMonthlySalaryResignation { get; private set; }

    // ── Five-section totals (RF-010): always computed server-side, never accepted from the client (RN-13).
    public decimal TotalIncomes { get; private set; }

    public decimal TotalDeductions { get; private set; }

    public decimal NetPay { get; private set; }

    public decimal TotalEmployerCharges { get; private set; }

    /// <summary>Reserve (accounting provision) = TotalIncomes + TotalEmployerCharges, charged to the plaza's cost center (D-13/P-02).</summary>
    public decimal ProvisionTotal { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    public Guid? IssuedByUserId { get; private set; }

    public DateTime? IssuedAtUtc { get; private set; }

    public Guid? AnnulledByUserId { get; private set; }

    public DateTime? AnnulledAtUtc { get; private set; }

    public string? AnnulmentReason { get; private set; }

    public bool IsActive { get; private set; } = true;

    public Guid ConcurrencyToken { get; private set; }

    public IReadOnlyCollection<PersonnelFileSettlementLine> Lines => _lines;

    /// <summary>Editable = scenario (always) or a real settlement still in BORRADOR (RN-04).</summary>
    public bool IsEditable => Kind == SettlementKind.Escenario || StatusCode == SettlementStatuses.Borrador;

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public void BindToRetirementRequest(long retirementRequestId)
    {
        if (Kind != SettlementKind.Liquidacion)
        {
            throw new InvalidOperationException("Only a real settlement anchors to a retirement request.");
        }

        RetirementRequestId = retirementRequestId;
    }

    /// <summary>Creates a real settlement (BORRADOR) anchored to an executed retirement and one of its closed plazas (D-03/D-10).</summary>
    public static PersonnelFileSettlement CreateSettlement(
        Guid retirementRequestPublicId,
        Guid assignedPositionPublicId,
        string? positionNameSnapshot,
        DateTime plazaStartDate,
        Guid? costCenterPublicId,
        string? costCenterNameSnapshot,
        DateTime retirementDate,
        string retirementCategoryCode,
        string? retirementCategoryNameSnapshot,
        string retirementReasonCode,
        string? retirementReasonNameSnapshot,
        Guid requesterFilePublicId,
        string requesterNameSnapshot,
        DateTime requestDate,
        string? notes,
        Guid requestedByUserId,
        string currencyCode)
    {
        if (retirementRequestPublicId == Guid.Empty)
        {
            throw new ArgumentException("The retirement request reference is required.", nameof(retirementRequestPublicId));
        }

        return new PersonnelFileSettlement(
            SettlementKind.Liquidacion,
            retirementRequestPublicId,
            assignedPositionPublicId,
            positionNameSnapshot,
            plazaStartDate,
            costCenterPublicId,
            costCenterNameSnapshot,
            retirementDate,
            retirementCategoryCode,
            retirementCategoryNameSnapshot,
            retirementReasonCode,
            retirementReasonNameSnapshot,
            requesterFilePublicId,
            requesterNameSnapshot,
            requestDate,
            notes,
            requestedByUserId,
            currencyCode);
    }

    /// <summary>Creates a side-effect-free scenario over an active plaza with an estimated date (D-05).</summary>
    public static PersonnelFileSettlement CreateScenario(
        Guid assignedPositionPublicId,
        string? positionNameSnapshot,
        DateTime plazaStartDate,
        Guid? costCenterPublicId,
        string? costCenterNameSnapshot,
        DateTime estimatedRetirementDate,
        string retirementCategoryCode,
        string? retirementCategoryNameSnapshot,
        string retirementReasonCode,
        string? retirementReasonNameSnapshot,
        Guid requesterFilePublicId,
        string requesterNameSnapshot,
        DateTime requestDate,
        string? notes,
        Guid requestedByUserId,
        string currencyCode) =>
        new(
            SettlementKind.Escenario,
            retirementRequestPublicId: null,
            assignedPositionPublicId,
            positionNameSnapshot,
            plazaStartDate,
            costCenterPublicId,
            costCenterNameSnapshot,
            estimatedRetirementDate,
            retirementCategoryCode,
            retirementCategoryNameSnapshot,
            retirementReasonCode,
            retirementReasonNameSnapshot,
            requesterFilePublicId,
            requesterNameSnapshot,
            requestDate,
            notes,
            requestedByUserId,
            currencyCode);

    /// <summary>Edits the header fields — scenario always, real settlement only in BORRADOR (RN-04).</summary>
    public void UpdateHeader(
        Guid requesterFilePublicId,
        string requesterNameSnapshot,
        DateTime requestDate,
        string? notes)
    {
        EnsureEditable();
        ApplyHeader(requesterFilePublicId, requesterNameSnapshot, requestDate, notes);
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Edits the hypothetical retirement facts of a SCENARIO (estimated date + category/reason). A real
    /// settlement inherits them read-only from the executed retirement (D-03) and never changes them here.
    /// </summary>
    public void UpdateScenarioAssumptions(
        DateTime estimatedRetirementDate,
        string retirementCategoryCode,
        string? retirementCategoryNameSnapshot,
        string retirementReasonCode,
        string? retirementReasonNameSnapshot)
    {
        if (Kind != SettlementKind.Escenario)
        {
            throw new InvalidOperationException("Only a scenario can change its retirement assumptions.");
        }

        ApplyRetirementReason(
            estimatedRetirementDate,
            retirementCategoryCode,
            retirementCategoryNameSnapshot,
            retirementReasonCode,
            retirementReasonNameSnapshot);
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Snapshots the legal parameters (RF-011). Guarded to editable records; values validated > 0 where required.</summary>
    public void UpdateParameters(
        decimal minimumMonthlyWage,
        decimal indemnityCapMultiplier,
        decimal resignationCapMultiplier,
        decimal vacationDays,
        decimal vacationPremiumPercent,
        decimal aguinaldoDays,
        decimal resignationBenefitDays,
        int resignationMinimumServiceYears,
        decimal aguinaldoExemptionMultiplier,
        int monthDivisorDays,
        int yearDivisorDays)
    {
        EnsureEditable();
        if (minimumMonthlyWage <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumMonthlyWage), "The minimum monthly wage must be greater than zero.");
        }

        if (indemnityCapMultiplier <= 0 || resignationCapMultiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(indemnityCapMultiplier), "The legal cap multipliers must be greater than zero.");
        }

        if (vacationDays < 0 || vacationPremiumPercent < 0 || aguinaldoDays < 0 || resignationBenefitDays < 0 ||
            resignationMinimumServiceYears < 0 || aguinaldoExemptionMultiplier < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(vacationDays), "The settlement parameters cannot be negative.");
        }

        if (monthDivisorDays <= 0 || yearDivisorDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monthDivisorDays), "The month/year divisors must be greater than zero.");
        }

        MinimumMonthlyWage = minimumMonthlyWage;
        IndemnityCapMultiplier = indemnityCapMultiplier;
        ResignationCapMultiplier = resignationCapMultiplier;
        VacationDays = vacationDays;
        VacationPremiumPercent = vacationPremiumPercent;
        AguinaldoDays = aguinaldoDays;
        ResignationBenefitDays = resignationBenefitDays;
        ResignationMinimumServiceYears = resignationMinimumServiceYears;
        AguinaldoExemptionMultiplier = aguinaldoExemptionMultiplier;
        MonthDivisorDays = monthDivisorDays;
        YearDivisorDays = yearDivisorDays;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Writes the engine's derived bases and five-section totals (RN-13: the ONLY way amounts reach the
    /// record — the client never sends totals). Invoked by the handler after every recalculation.
    /// </summary>
    public void ApplyCalculation(
        decimal monthlyBaseSalary,
        int seniorityYears,
        int seniorityDays,
        decimal cappedMonthlySalaryIndemnity,
        decimal cappedMonthlySalaryResignation,
        decimal aguinaldoDays,
        decimal totalIncomes,
        decimal totalDeductions,
        decimal netPay,
        decimal totalEmployerCharges,
        decimal provisionTotal)
    {
        EnsureEditable();
        MonthlyBaseSalary = monthlyBaseSalary;
        SeniorityYears = seniorityYears;
        SeniorityDays = seniorityDays;
        CappedMonthlySalaryIndemnity = cappedMonthlySalaryIndemnity;
        CappedMonthlySalaryResignation = cappedMonthlySalaryResignation;
        AguinaldoDays = aguinaldoDays;
        TotalIncomes = totalIncomes;
        TotalDeductions = totalDeductions;
        NetPay = netPay;
        TotalEmployerCharges = totalEmployerCharges;
        ProvisionTotal = provisionTotal;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Adds a detail line (initial generation, regenerate or a manual line).</summary>
    public void AddLine(PersonnelFileSettlementLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        EnsureEditable();
        _lines.Add(line);
    }

    /// <summary>Removes a detail line ("eliminar la información que no aplica" — it can be re-generated from the catalog).</summary>
    public void RemoveLine(PersonnelFileSettlementLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        EnsureEditable();
        _lines.Remove(line);
    }

    /// <summary>Clears every line (explicit regenerate — pre-development clarification №2).</summary>
    public void ClearLines()
    {
        EnsureEditable();
        _lines.Clear();
    }

    /// <summary>
    /// Issues the settlement (D-15): BORRADOR → EMITIDA, immutable afterwards. Requires at least one
    /// included income line and an explicit confirmation when the net pay is negative (RN-14).
    /// </summary>
    public void MarkIssued(Guid issuedByUserId, DateTime issuedAtUtc, bool confirmNegativeNet)
    {
        if (Kind != SettlementKind.Liquidacion)
        {
            throw new InvalidOperationException("Only a real settlement can be issued.");
        }

        if (StatusCode != SettlementStatuses.Borrador)
        {
            throw new InvalidOperationException("Only a BORRADOR settlement can be issued.");
        }

        if (!_lines.Any(line => line is { ConceptClass: SettlementConceptClass.Ingreso, IsIncluded: true }))
        {
            throw new InvalidOperationException("Issuing a settlement requires at least one included income line.");
        }

        if (NetPay < 0 && !confirmNegativeNet)
        {
            throw new InvalidOperationException("Issuing a settlement with a negative net pay requires an explicit confirmation.");
        }

        StatusCode = SettlementStatuses.Emitida;
        IssuedByUserId = issuedByUserId;
        IssuedAtUtc = issuedAtUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Annuls the settlement (D-15/RF-005): from BORRADOR (reason optional) or EMITIDA (reason mandatory).
    /// Terminal; the record and its lines are preserved. After annulling, the (retirement × plaza) slot is
    /// free for a new settlement (D-16).
    /// </summary>
    public void Annul(Guid annulledByUserId, DateTime annulledAtUtc, string? reason)
    {
        if (Kind != SettlementKind.Liquidacion)
        {
            throw new InvalidOperationException("Only a real settlement can be annulled; a scenario is deleted.");
        }

        if (StatusCode is not (SettlementStatuses.Borrador or SettlementStatuses.Emitida))
        {
            throw new InvalidOperationException("Only a BORRADOR or EMITIDA settlement can be annulled.");
        }

        var normalizedReason = PersonnelFileNormalization.CleanOptional(reason);
        if (StatusCode == SettlementStatuses.Emitida && string.IsNullOrWhiteSpace(normalizedReason))
        {
            throw new InvalidOperationException("Annulling an issued settlement requires a reason.");
        }

        StatusCode = SettlementStatuses.Anulada;
        AnnulledByUserId = annulledByUserId;
        AnnulledAtUtc = annulledAtUtc;
        AnnulmentReason = normalizedReason;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Soft-delete — scenarios only (a real settlement is never deleted, it is annulled).</summary>
    public void SetActive(bool isActive)
    {
        if (Kind != SettlementKind.Escenario)
        {
            throw new InvalidOperationException("Only a scenario can be deactivated; a real settlement is annulled.");
        }

        IsActive = isActive;
        ConcurrencyToken = Guid.NewGuid();
    }

    private void EnsureEditable()
    {
        if (!IsEditable)
        {
            throw new InvalidOperationException("Only a BORRADOR settlement (or a scenario) can be edited.");
        }
    }

    private void ApplyPosition(
        Guid assignedPositionPublicId,
        string? positionNameSnapshot,
        DateTime plazaStartDate,
        Guid? costCenterPublicId,
        string? costCenterNameSnapshot)
    {
        if (assignedPositionPublicId == Guid.Empty)
        {
            throw new ArgumentException("The assigned position (plaza) reference is required.", nameof(assignedPositionPublicId));
        }

        AssignedPositionPublicId = assignedPositionPublicId;
        PositionNameSnapshot = PersonnelFileNormalization.CleanOptional(positionNameSnapshot);
        PlazaStartDate = PersonnelFileNormalization.NormalizeDate(plazaStartDate);
        CostCenterPublicId = costCenterPublicId;
        CostCenterNameSnapshot = PersonnelFileNormalization.CleanOptional(costCenterNameSnapshot);
    }

    private void ApplyRetirementReason(
        DateTime retirementDate,
        string retirementCategoryCode,
        string? retirementCategoryNameSnapshot,
        string retirementReasonCode,
        string? retirementReasonNameSnapshot)
    {
        var normalizedRetirementDate = PersonnelFileNormalization.NormalizeDate(retirementDate);
        if (normalizedRetirementDate < PlazaStartDate)
        {
            throw new InvalidOperationException("The retirement date cannot precede the plaza start date.");
        }

        RetirementDate = normalizedRetirementDate;
        RetirementCategoryCode = PersonnelFileNormalization.Clean(retirementCategoryCode, nameof(retirementCategoryCode)).ToUpperInvariant();
        RetirementCategoryNameSnapshot = PersonnelFileNormalization.CleanOptional(retirementCategoryNameSnapshot);
        RetirementReasonCode = PersonnelFileNormalization.Clean(retirementReasonCode, nameof(retirementReasonCode)).ToUpperInvariant();
        RetirementReasonNameSnapshot = PersonnelFileNormalization.CleanOptional(retirementReasonNameSnapshot);
    }

    private void ApplyHeader(
        Guid requesterFilePublicId,
        string requesterNameSnapshot,
        DateTime requestDate,
        string? notes)
    {
        if (requesterFilePublicId == Guid.Empty)
        {
            throw new ArgumentException("The requester file reference is required.", nameof(requesterFilePublicId));
        }

        RequesterFilePublicId = requesterFilePublicId;
        RequesterNameSnapshot = PersonnelFileNormalization.Clean(requesterNameSnapshot, nameof(requesterNameSnapshot));
        RequestDate = PersonnelFileNormalization.NormalizeDate(requestDate);
        Notes = PersonnelFileNormalization.CleanOptional(notes);
    }
}

/// <summary>
/// One detail line of a settlement: a concept of the ingresos / descuentos / pagos-patronales sections with
/// its calculation trace (base, units, computed amount, exempt vs taxable-excess split — RN-009.4), the
/// audited manual override (D-14: amount + mandatory reason, the computed value stays visible), the
/// inclusion flag ("eliminar la información que no aplica") and the ratified value-0 rule (RN-008.4: a
/// legally unmet concept is recorded at 0 with a readable reason, never silently dropped).
/// </summary>
public sealed class PersonnelFileSettlementLine : TenantEntity
{
    private PersonnelFileSettlementLine()
    {
    }

    private PersonnelFileSettlementLine(
        SettlementConceptClass conceptClass,
        string conceptCode,
        string conceptNameSnapshot,
        string? description,
        bool isSystemCalculated,
        int sortOrder)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        ConceptClass = conceptClass;
        ConceptCode = PersonnelFileNormalization.Clean(conceptCode, nameof(conceptCode)).ToUpperInvariant();
        ConceptNameSnapshot = PersonnelFileNormalization.Clean(conceptNameSnapshot, nameof(conceptNameSnapshot));
        Description = PersonnelFileNormalization.CleanOptional(description);
        IsSystemCalculated = isSystemCalculated;
        SortOrder = sortOrder;
        IsIncluded = true;
    }

    public long SettlementId { get; private set; }

    public PersonnelFileSettlement Settlement { get; private set; } = null!;

    public SettlementConceptClass ConceptClass { get; private set; }

    public string ConceptCode { get; private set; } = string.Empty;

    public string ConceptNameSnapshot { get; private set; } = string.Empty;

    /// <summary>Free description — mandatory on manual lines (OTRO_*, horas extras), optional elsewhere.</summary>
    public string? Description { get; private set; }

    public bool IsSystemCalculated { get; private set; }

    /// <summary>Base amount the formula used (e.g. the capped salary) — traceability of the number.</summary>
    public decimal? CalculationBase { get; private set; }

    /// <summary>Days/factor the formula used (e.g. pending days, days since the vacation anniversary) — editable input.</summary>
    public decimal? UnitsOrDays { get; private set; }

    public decimal CalculatedAmount { get; private set; }

    /// <summary>Exempt portion of the amount for Renta purposes (RN-009.4).</summary>
    public decimal ExemptAmount { get; private set; }

    /// <summary>Taxable excess the system adds to the Renta base (ratified §17.3: the system controls it).</summary>
    public decimal TaxableExcessAmount { get; private set; }

    public decimal? OverrideAmount { get; private set; }

    public string? OverrideReason { get; private set; }

    /// <summary>The amount that joins the totals: the audited override when present, else the computed amount.</summary>
    public decimal FinalAmount { get; private set; }

    public bool IsIncluded { get; private set; }

    /// <summary>RN-008.4 (ratified): the concept's legal requirement is unmet, so the line is recorded at 0.</summary>
    public bool IsZeroByLaw { get; private set; }

    public string? ZeroReasonCode { get; private set; }

    /// <summary>Readable calculation trace ("15 × 1.30 × 143/365 × $12.17 …").</summary>
    public string? CalculationDetail { get; private set; }

    /// <summary>Counterparty of an external deduction ("descuento externo — última cuota", D-08).</summary>
    public string? CounterpartyName { get; private set; }

    public int SortOrder { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToSettlement(long settlementId) => SettlementId = settlementId;

    public static PersonnelFileSettlementLine Create(
        SettlementConceptClass conceptClass,
        string conceptCode,
        string conceptNameSnapshot,
        string? description,
        bool isSystemCalculated,
        int sortOrder) =>
        new(conceptClass, conceptCode, conceptNameSnapshot, description, isSystemCalculated, sortOrder);

    /// <summary>
    /// Writes the engine's computation for this line (base, units, amount, exempt/excess split, trace and
    /// the value-0 rule). The override — when present — survives recalculations (D-14): only
    /// <see cref="FinalAmount"/> keeps honoring it.
    /// </summary>
    public void ApplyComputation(
        decimal? calculationBase,
        decimal? unitsOrDays,
        decimal calculatedAmount,
        decimal exemptAmount,
        decimal taxableExcessAmount,
        string? calculationDetail,
        bool isZeroByLaw,
        string? zeroReasonCode,
        string? counterpartyName = null)
    {
        CalculationBase = calculationBase;
        UnitsOrDays = unitsOrDays;
        CalculatedAmount = calculatedAmount;
        ExemptAmount = exemptAmount;
        TaxableExcessAmount = taxableExcessAmount;
        CalculationDetail = PersonnelFileNormalization.CleanOptional(calculationDetail);
        IsZeroByLaw = isZeroByLaw;
        ZeroReasonCode = PersonnelFileNormalization.CleanOptional(zeroReasonCode);
        CounterpartyName = PersonnelFileNormalization.CleanOptional(counterpartyName);
        RefreshFinalAmount();
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Fixes a manual amount with its mandatory audit note (D-14); the computed value stays visible.</summary>
    public void SetOverride(decimal overrideAmount, string overrideReason)
    {
        if (string.IsNullOrWhiteSpace(overrideReason))
        {
            throw new InvalidOperationException("A manual override requires a reason.");
        }

        OverrideAmount = overrideAmount;
        OverrideReason = PersonnelFileNormalization.Clean(overrideReason, nameof(overrideReason));
        RefreshFinalAmount();
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Removes the manual override, restoring the computed amount (D-14).</summary>
    public void ClearOverride()
    {
        OverrideAmount = null;
        OverrideReason = null;
        RefreshFinalAmount();
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Includes/excludes the line from the calculation without losing it (RN-002.2).</summary>
    public void SetIncluded(bool isIncluded)
    {
        IsIncluded = isIncluded;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Edits the description and (via override-free manual amount) of a manual line.</summary>
    public void UpdateManual(string description, decimal amount)
    {
        if (IsSystemCalculated)
        {
            throw new InvalidOperationException("Only a manual line can be edited this way; adjust engine lines via inputs or overrides.");
        }

        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "A manual amount cannot be negative.");
        }

        Description = PersonnelFileNormalization.Clean(description, nameof(description));
        CalculatedAmount = amount;
        RefreshFinalAmount();
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Edits the formula input (days/base) of an engine line — the engine recomputes right after.</summary>
    public void SetUnitsOrDays(decimal unitsOrDays)
    {
        if (unitsOrDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitsOrDays), "Days/units cannot be negative.");
        }

        UnitsOrDays = unitsOrDays;
        ConcurrencyToken = Guid.NewGuid();
    }

    private void RefreshFinalAmount() => FinalAmount = OverrideAmount ?? CalculatedAmount;
}
