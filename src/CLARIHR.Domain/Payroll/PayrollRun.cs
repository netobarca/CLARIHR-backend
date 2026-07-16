using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Payroll;

/// <summary>
/// Class of a payroll-run line. EXPLICIT column with a domain CHECK and NO default — the inherited
/// lesson of the settlement engine's <c>ResolveClass</c> default→Ingreso trap (REQ-012 §0.12): a payroll
/// line that does not declare its class is a bug, never an income.
/// </summary>
public static class PayrollLineClasses
{
    public const string Ingreso = "Ingreso";
    public const string Descuento = "Descuento";
    public const string PagoPatronal = "PagoPatronal";

    public static readonly IReadOnlyCollection<string> All = new[] { Ingreso, Descuento, PagoPatronal };
}

/// <summary>
/// One payroll run ("corrida", REQ-012 §1.4 — D-07): a Nómina × period calculation with its lines,
/// persisted totals and lifecycle (GENERADA → AUTORIZADA → CERRADA, return-with-reason, pre-closure
/// annulment — <see cref="PayrollRunStatuses"/>). Snapshots pin what was true at generation (definition
/// code/name/type, period label/dates, currency). ONE ACTIVE run per (definition, period) — filtered
/// unique index; annulment releases the slot. Custody: validate-first mutators; the handlers pre-check
/// and the engine (PayrollCalculationRules) produces the lines.
/// </summary>
public sealed class PayrollRun : TenantEntity
{
    public const int MaxCodeLength = 80;
    public const int MaxNameLength = 200;
    public const int MaxLabelLength = 80;
    public const int MaxReasonLength = 500;
    public const int CurrencyCodeLength = 3;

    private readonly List<PayrollRunLine> _lines = [];

    private PayrollRun()
    {
    }

    private PayrollRun(
        Guid publicId,
        long payrollDefinitionId,
        long payrollPeriodId,
        string payrollDefinitionCode,
        string payrollDefinitionName,
        string payrollTypeCode,
        string periodLabel,
        DateOnly periodStartDate,
        DateOnly periodEndDate,
        DateOnly? paymentDate,
        string currencyCode,
        Guid generatedByUserId,
        DateTime generatedUtc)
    {
        PublicId = publicId;
        PayrollDefinitionId = payrollDefinitionId;
        PayrollPeriodId = payrollPeriodId;
        PayrollDefinitionCode = Require(payrollDefinitionCode, MaxCodeLength, nameof(payrollDefinitionCode));
        PayrollDefinitionName = Require(payrollDefinitionName, MaxNameLength, nameof(payrollDefinitionName));
        PayrollTypeCode = Require(payrollTypeCode, MaxCodeLength, nameof(payrollTypeCode));
        PeriodLabel = Require(periodLabel, MaxLabelLength, nameof(periodLabel));
        PeriodStartDate = periodStartDate;
        PeriodEndDate = periodEndDate;
        PaymentDate = paymentDate;
        CurrencyCode = Require(currencyCode, CurrencyCodeLength, nameof(currencyCode));
        StatusCode = PayrollRunStatuses.Generada;
        GeneratedByUserId = generatedByUserId;
        GeneratedUtc = generatedUtc;
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public long PayrollDefinitionId { get; private set; }

    public long PayrollPeriodId { get; private set; }

    public string PayrollDefinitionCode { get; private set; } = string.Empty;

    public string PayrollDefinitionName { get; private set; } = string.Empty;

    public string PayrollTypeCode { get; private set; } = string.Empty;

    public string PeriodLabel { get; private set; } = string.Empty;

    public DateOnly PeriodStartDate { get; private set; }

    public DateOnly PeriodEndDate { get; private set; }

    public DateOnly? PaymentDate { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    public string StatusCode { get; private set; } = PayrollRunStatuses.Generada;

    public Guid GeneratedByUserId { get; private set; }

    public DateTime GeneratedUtc { get; private set; }

    public int RegeneratedCount { get; private set; }

    public Guid? AuthorizedByUserId { get; private set; }

    public DateTime? AuthorizedUtc { get; private set; }

    public string? ReturnReason { get; private set; }

    public Guid? ClosedByUserId { get; private set; }

    public DateTime? ClosedUtc { get; private set; }

    public Guid? AnnulledByUserId { get; private set; }

    public DateTime? AnnulledUtc { get; private set; }

    public string? AnnulmentReason { get; private set; }

    public int EmployeeCount { get; private set; }

    public decimal TotalIncome { get; private set; }

    public decimal TotalDeductions { get; private set; }

    public decimal TotalEmployerCost { get; private set; }

    public decimal TotalNet { get; private set; }

    /// <summary>Stable warning codes of the generation (jsonb — codes + context, never free prose).</summary>
    public string? WarningsJson { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public IReadOnlyCollection<PayrollRunLine> Lines => _lines.AsReadOnly();

    /// <summary>Line overrides/inclusion and re-generation are only possible while GENERADA.</summary>
    public bool IsEditable => StatusCode == PayrollRunStatuses.Generada;

    public static PayrollRun Create(
        long payrollDefinitionId,
        long payrollPeriodId,
        string payrollDefinitionCode,
        string payrollDefinitionName,
        string payrollTypeCode,
        string periodLabel,
        DateOnly periodStartDate,
        DateOnly periodEndDate,
        DateOnly? paymentDate,
        string currencyCode,
        Guid generatedByUserId,
        DateTime generatedUtc) =>
        new(
            Guid.NewGuid(),
            payrollDefinitionId,
            payrollPeriodId,
            payrollDefinitionCode,
            payrollDefinitionName,
            payrollTypeCode,
            periodLabel,
            periodStartDate,
            periodEndDate,
            paymentDate,
            currencyCode,
            generatedByUserId,
            generatedUtc);

    /// <summary>Replaces the full line set and the persisted totals (generation / regeneration / recalc).</summary>
    public void ReplaceLines(
        IReadOnlyCollection<PayrollRunLine> lines,
        int employeeCount,
        decimal totalIncome,
        decimal totalDeductions,
        decimal totalEmployerCost,
        decimal totalNet,
        string? warningsJson)
    {
        ArgumentNullException.ThrowIfNull(lines);
        EnsureEditable();

        _lines.Clear();
        foreach (var line in lines)
        {
            _lines.Add(line);
        }

        EmployeeCount = employeeCount;
        TotalIncome = totalIncome;
        TotalDeductions = totalDeductions;
        TotalEmployerCost = totalEmployerCost;
        TotalNet = totalNet;
        WarningsJson = warningsJson;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Refreshes the persisted totals after a line-level adjustment (override / inclusion).</summary>
    public void RefreshTotals(
        decimal totalIncome,
        decimal totalDeductions,
        decimal totalEmployerCost,
        decimal totalNet)
    {
        EnsureEditable();
        TotalIncome = totalIncome;
        TotalDeductions = totalDeductions;
        TotalEmployerCost = totalEmployerCost;
        TotalNet = totalNet;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void MarkRegenerated()
    {
        EnsureEditable();
        RegeneratedCount++;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>GENERADA → AUTORIZADA (anti-self is the handler's double check — §3.6).</summary>
    public void Authorize(Guid authorizedByUserId, DateTime atUtc)
    {
        if (!PayrollRunStatuses.Authorizable.Contains(StatusCode))
        {
            throw new InvalidOperationException("Only a GENERADA payroll run can be authorized.");
        }

        StatusCode = PayrollRunStatuses.Autorizada;
        AuthorizedByUserId = authorizedByUserId;
        AuthorizedUtc = atUtc;
        ReturnReason = null;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>AUTORIZADA → GENERADA with a mandatory reason (the ONLY pre-closure reopening — REQ-013 P-02).</summary>
    public void Return(string reason)
    {
        var normalized = RequireReason(reason);
        if (!PayrollRunStatuses.Returnable.Contains(StatusCode))
        {
            throw new InvalidOperationException("Only an AUTORIZADA payroll run can be returned.");
        }

        StatusCode = PayrollRunStatuses.Generada;
        AuthorizedByUserId = null;
        AuthorizedUtc = null;
        ReturnReason = normalized;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>AUTORIZADA → CERRADA (terminal; the period closes in the SAME transaction — §3.6).</summary>
    public void Close(Guid closedByUserId, DateTime atUtc)
    {
        if (!PayrollRunStatuses.Closable.Contains(StatusCode))
        {
            throw new InvalidOperationException("Only an AUTORIZADA payroll run can be closed.");
        }

        StatusCode = PayrollRunStatuses.Cerrada;
        ClosedByUserId = closedByUserId;
        ClosedUtc = atUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// GENERADA|AUTORIZADA → ANULADA (pre-closure only). Releases the one-active-run slot (IsActive=false
    /// under the filtered unique index) — the pool reversal is the handler's job (§3.5).
    /// </summary>
    public void Annul(Guid annulledByUserId, string reason, DateTime atUtc)
    {
        var normalized = RequireReason(reason);
        if (PayrollRunStatuses.Terminal.Contains(StatusCode))
        {
            throw new InvalidOperationException("A closed or annulled payroll run cannot be annulled.");
        }

        StatusCode = PayrollRunStatuses.Anulada;
        AnnulledByUserId = annulledByUserId;
        AnnulledUtc = atUtc;
        AnnulmentReason = normalized;
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    private void EnsureEditable()
    {
        if (!IsEditable)
        {
            throw new InvalidOperationException("Only a GENERADA payroll run can be modified.");
        }
    }

    private static string Require(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty.", parameterName);
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value must be {maxLength} characters or fewer.", parameterName);
        }

        return trimmed;
    }

    private static string RequireReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required.", nameof(reason));
        }

        var trimmed = reason.Trim();
        if (trimmed.Length > MaxReasonLength)
        {
            throw new ArgumentException($"Reason must be {MaxReasonLength} characters or fewer.", nameof(reason));
        }

        return trimmed;
    }
}

/// <summary>
/// One line of a payroll run (§1.4): employee/plaza snapshots, the concept, the EXPLICIT
/// <see cref="PayrollLineClasses"/> class (CHECK, no default — the ResolveClass lesson), the calculated
/// amount with its optional audited override, the inclusion flag (REQ-014: excluding a pool line reverts
/// its application and re-exposes the source record) and the line→source traceability
/// (<see cref="SourceModule"/> + <see cref="SourceReferencePublicId"/> — REQ-013/014/015).
/// Immutable except for the review adjustments (override / inclusion), which rotate the parent's token.
/// </summary>
public sealed class PayrollRunLine : TenantEntity
{
    public const int MaxConceptCodeLength = 80;
    public const int MaxConceptNameLength = 200;
    public const int MaxSourceModuleLength = 40;
    public const int MaxOverrideNoteLength = 500;
    public const int MaxEmployeeNameLength = 200;
    public const int MaxEmployeeCodeLength = 80;
    public const int MaxCostCenterNameLength = 200;
    public const int CurrencyCodeLength = 3;

    private PayrollRunLine()
    {
    }

    private PayrollRunLine(
        Guid publicId,
        long personnelFileId,
        Guid employeePublicId,
        string employeeName,
        string? employeeCode,
        Guid? assignedPositionPublicId,
        string? costCenterName,
        string conceptCode,
        string conceptName,
        string lineClass,
        decimal? units,
        decimal? baseAmount,
        decimal calculatedAmount,
        bool isIncluded,
        string? sourceModule,
        Guid? sourceReferencePublicId,
        string currencyCode,
        string? warningCodesJson,
        int sortOrder)
    {
        if (!PayrollLineClasses.All.Contains(lineClass))
        {
            throw new ArgumentException("Line class must be Ingreso, Descuento or PagoPatronal.", nameof(lineClass));
        }

        PublicId = publicId;
        PersonnelFileId = personnelFileId;
        EmployeePublicId = employeePublicId;
        EmployeeName = employeeName;
        EmployeeCode = employeeCode;
        AssignedPositionPublicId = assignedPositionPublicId;
        CostCenterName = costCenterName;
        ConceptCode = conceptCode;
        ConceptName = conceptName;
        LineClass = lineClass;
        Units = units;
        BaseAmount = baseAmount;
        CalculatedAmount = calculatedAmount;
        IsIncluded = isIncluded;
        SourceModule = sourceModule;
        SourceReferencePublicId = sourceReferencePublicId;
        CurrencyCode = currencyCode;
        WarningCodesJson = warningCodesJson;
        SortOrder = sortOrder;
    }

    public long PayrollRunId { get; private set; }

    public long PersonnelFileId { get; private set; }

    public Guid EmployeePublicId { get; private set; }

    public string EmployeeName { get; private set; } = string.Empty;

    public string? EmployeeCode { get; private set; }

    public Guid? AssignedPositionPublicId { get; private set; }

    public string? CostCenterName { get; private set; }

    public string ConceptCode { get; private set; } = string.Empty;

    public string ConceptName { get; private set; } = string.Empty;

    /// <summary>Ingreso | Descuento | PagoPatronal — explicit, CHECKed, no default (§0.12).</summary>
    public string LineClass { get; private set; } = string.Empty;

    public decimal? Units { get; private set; }

    public decimal? BaseAmount { get; private set; }

    public decimal CalculatedAmount { get; private set; }

    /// <summary>Audited review override (only while the run is GENERADA); null ⇒ the calculated amount rules.</summary>
    public decimal? OverrideAmount { get; private set; }

    public string? OverrideNote { get; private set; }

    public Guid? AdjustedByUserId { get; private set; }

    /// <summary>Excluded lines do not sum, do not apply pools and free their source record (REQ-014).</summary>
    public bool IsIncluded { get; private set; }

    public string? SourceModule { get; private set; }

    public Guid? SourceReferencePublicId { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    public string? WarningCodesJson { get; private set; }

    public int SortOrder { get; private set; }

    /// <summary>The effective amount: the audited override when present, else the calculated one.</summary>
    public decimal FinalAmount => OverrideAmount ?? CalculatedAmount;

    public static PayrollRunLine Create(
        long personnelFileId,
        Guid employeePublicId,
        string employeeName,
        string? employeeCode,
        Guid? assignedPositionPublicId,
        string? costCenterName,
        string conceptCode,
        string conceptName,
        string lineClass,
        decimal? units,
        decimal? baseAmount,
        decimal calculatedAmount,
        bool isIncluded,
        string? sourceModule,
        Guid? sourceReferencePublicId,
        string currencyCode,
        string? warningCodesJson,
        int sortOrder) =>
        new(
            Guid.NewGuid(),
            personnelFileId,
            employeePublicId,
            employeeName,
            employeeCode,
            assignedPositionPublicId,
            costCenterName,
            conceptCode,
            conceptName,
            lineClass,
            units,
            baseAmount,
            calculatedAmount,
            isIncluded,
            sourceModule,
            sourceReferencePublicId,
            currencyCode,
            warningCodesJson,
            sortOrder);

    /// <summary>Review override with its mandatory audit note (null amount clears the override).</summary>
    public void SetOverride(decimal? overrideAmount, string? note, Guid adjustedByUserId)
    {
        if (overrideAmount.HasValue && string.IsNullOrWhiteSpace(note))
        {
            throw new ArgumentException("An override requires a note.", nameof(note));
        }

        OverrideAmount = overrideAmount;
        OverrideNote = overrideAmount.HasValue ? note!.Trim() : null;
        AdjustedByUserId = adjustedByUserId;
    }

    public void SetIncluded(bool isIncluded, Guid adjustedByUserId)
    {
        IsIncluded = isIncluded;
        AdjustedByUserId = adjustedByUserId;
    }

    /// <summary>
    /// Re-binds the line's source reference to the CREATED installment/application public id once the pool
    /// is applied (§3.5) — the reversal annuls exactly those children. Registro lines (TNT/disciplinary/
    /// incapacity) keep their source record's id (the REQ-014 derived-consumption key).
    /// </summary>
    public void BindApplicationReference(Guid applicationPublicId) =>
        SourceReferencePublicId = applicationPublicId;
}
