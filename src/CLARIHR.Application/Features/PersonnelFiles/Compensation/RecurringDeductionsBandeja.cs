using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.PersonnelFiles.Compensation;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

// ── Bandeja de descuentos (RF-010) ────────────────────────────────────────────────────────────────────

/// <summary>A row of the company-wide recurring-deduction bandeja: one credit per employee with its plan header +
/// lifecycle status. Every status is represented (annulled / rejected included with their status — a status filter
/// is available). User ids are null-safe (a non-Guid principal → null).</summary>
public sealed record RecurringDeductionListItemResponse(
    Guid RecurringDeductionPublicId,
    Guid PersonnelFilePublicId,
    string EmployeeFullName,
    string? EmployeeCode,
    string Reference,
    string RecurringDeductionTypeCode,
    string ConceptTypeCode,
    string ConceptNameSnapshot,
    string? FinancialInstitution,
    Guid AssignedPositionPublicId,
    DateOnly EffectiveDate,
    string InstallmentFrequencyCode,
    string ApplicationFrequencyCode,
    string CurrencyCode,
    string PayrollTypeCode,
    bool IsIndefinite,
    bool UsesCompoundInterest,
    decimal? PrincipalAmount,
    decimal? InterestRatePercent,
    int? InstallmentCount,
    decimal? TotalAmount,
    decimal TotalCharged,
    decimal? TotalOutstanding,
    string SettlementActionCode,
    string StatusCode,
    Guid? RegisteredByUserId,
    Guid? DecidedByUserId)
{
    [JsonIgnore]
    public Guid Id => RecurringDeductionPublicId;
}

/// <summary>The bandeja page: items + paging + per-status counts (over the full non-status filter, so every status
/// is represented even when the items are narrowed to one status) + the charged/outstanding totals per currency.</summary>
public sealed record RecurringDeductionBandejaResponse(
    IReadOnlyCollection<RecurringDeductionListItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    IReadOnlyDictionary<string, int> StatusCounts,
    IReadOnlyDictionary<string, decimal> ChargedByCurrency,
    IReadOnlyDictionary<string, decimal> OutstandingByCurrency);

/// <summary>
/// An export row of the recurring-deduction bandeja. The Excel/CSV/JSON writer turns the public property names into
/// column headers (reflection), so the property names are the Spanish headers seen by HR.
/// </summary>
public sealed record DescuentoCiclicoExportRow(
    string Empleado,
    string? CodigoEmpleado,
    string Referencia,
    string Tipo,
    string Concepto,
    string? InstitucionFinanciera,
    string Plaza,
    DateOnly FechaVigencia,
    string FrecuenciaCuota,
    string FrecuenciaAplicacion,
    bool ConInteres,
    decimal? Principal,
    decimal? TasaInteres,
    int? NumeroCuotas,
    decimal? MontoTotal,
    decimal TotalCobrado,
    decimal? TotalNoCobrado,
    bool Indefinido,
    string AccionLiquidacion,
    string Estado,
    string Moneda,
    string? RegistradoPor,
    string? DecididoPor);

/// <summary>Filters shared by the recurring-deduction bandeja and export. Every filter is optional; when
/// <see cref="StatusCode"/> is omitted every status is listed. The StatusCounts are always computed over every
/// status.</summary>
public sealed record QueryRecurringDeductionsQuery(
    Guid CompanyId,
    Guid? EmployeeId,
    string? StatusCode,
    string? RecurringDeductionTypeCode,
    string? PayrollTypeCode,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    int PageNumber = 1,
    int PageSize = 25) : IQuery<RecurringDeductionBandejaResponse>;

public sealed record ExportRecurringDeductionsQuery(
    Guid CompanyId,
    Guid? EmployeeId,
    string? StatusCode,
    string? RecurringDeductionTypeCode,
    string? PayrollTypeCode,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    int? MaxRows) : IQuery<IReadOnlyCollection<DescuentoCiclicoExportRow>>;

// ── Bandeja de cobros pendientes / vencidos (RF-011) ───────────────────────────────────────────────────

/// <summary>
/// One PENDING (unapplied) theoretical CHARGE of a VIGENTE credit: a pure derivation of the plan (never persisted)
/// whose theoretical due date is on/before the consulted cutoff and is not yet applied.
/// <see cref="IsOverdue"/> marks the ones whose due date is already past (a work item for the next apply-period
/// batch). A credit whose effective date has not been reached contributes nothing.
/// </summary>
public sealed record RecurringDeductionPendingInstallmentResponse(
    Guid RecurringDeductionPublicId,
    Guid PersonnelFilePublicId,
    string EmployeeFullName,
    string? EmployeeCode,
    string Reference,
    string RecurringDeductionTypeCode,
    string ConceptNameSnapshot,
    string? FinancialInstitution,
    Guid AssignedPositionPublicId,
    string PayrollTypeCode,
    string CurrencyCode,
    int InstallmentNumber,
    DateOnly TheoreticalDueDate,
    decimal Amount,
    decimal? CapitalAmount,
    decimal? InterestAmount,
    bool IsOverdue);

/// <summary>The pending-charges page: the projected pending / overdue charges + paging.</summary>
public sealed record RecurringDeductionPendingInstallmentsResponse(
    IReadOnlyCollection<RecurringDeductionPendingInstallmentResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount);

/// <summary>An export row of the pending-charges bandeja (Spanish property names → headers).</summary>
public sealed record CuotaPendienteDescuentoExportRow(
    string Empleado,
    string? CodigoEmpleado,
    string Referencia,
    string Tipo,
    string Concepto,
    string? InstitucionFinanciera,
    string Plaza,
    string TipoPlanilla,
    int NumeroCuota,
    DateOnly FechaTeorica,
    decimal Monto,
    decimal? Capital,
    decimal? Interes,
    string Moneda,
    bool Vencida);

/// <summary>
/// Filters of the pending-charges bandeja / export (RF-011). The cutoff is the payroll-period end date (when
/// <see cref="PayrollPeriodPublicId"/> is supplied), the bare <see cref="CutoffDate"/>, or — if neither — today.
/// <see cref="StartDate"/> (optional) narrows the projection to charges due on/after it.
/// </summary>
public sealed record QueryPendingRecurringDeductionInstallmentsQuery(
    Guid CompanyId,
    string? PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    DateOnly? CutoffDate,
    DateOnly? StartDate,
    Guid? EmployeeId,
    int PageNumber = 1,
    int PageSize = 25) : IQuery<RecurringDeductionPendingInstallmentsResponse>;

public sealed record ExportPendingRecurringDeductionInstallmentsQuery(
    Guid CompanyId,
    string? PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    DateOnly? CutoffDate,
    DateOnly? StartDate,
    Guid? EmployeeId,
    int? MaxRows) : IQuery<IReadOnlyCollection<CuotaPendienteDescuentoExportRow>>;

/// <summary>
/// A VIGENTE credit considered by the pending-charges projection, enriched with the employee / plaza metadata
/// needed for the bandeja rows and its applied charge numbers. The handler projects the theoretical pending
/// charges in-memory through the pure <c>RecurringDeductionRules</c>, reusing the very scan item the apply-period
/// batch consumes, so the bandeja and the batch cuadran by construction.
/// </summary>
public sealed record RecurringDeductionPendingScanItem(
    RecurringDeductionBatchScanItem Scan,
    Guid PersonnelFilePublicId,
    string EmployeeFullName,
    string? EmployeeCode,
    string Reference,
    string RecurringDeductionTypeCode,
    string ConceptNameSnapshot,
    string? FinancialInstitution,
    Guid AssignedPositionPublicId,
    string PayrollTypeCode,
    string CurrencyCode);

// ── Insumo de planilla (RF-012) ────────────────────────────────────────────────────────────────────────

/// <summary>
/// One APPLIED charge of the payroll input for an external payroll system: a row per applied (<c>APLICADA</c>,
/// active) charge of the range with its imputed period, the CREDITOR (financial institution) and the credit
/// reference the payroll operator needs to pay it, plus the capital/interest split. The Spanish property names are
/// the export headers. This export cuadra EXACTLY against the pending charges of the same filter once applied;
/// annulled charges are excluded.
/// </summary>
public sealed record InsumoPlanillaDescuentoExportRow(
    string Empleado,
    string? CodigoEmpleado,
    string Referencia,
    string Concepto,
    string? InstitucionFinanciera,
    string TipoPlanilla,
    string? PeriodoPlanilla,
    DateOnly FechaAplicada,
    string TipoCuota,
    int? NumeroCuota,
    decimal Monto,
    decimal? Capital,
    decimal? Interes,
    string Moneda);

/// <summary>
/// The payroll-input export (RF-012): the applied charges of a payroll type over a MANDATORY date range
/// (<see cref="StartDate"/> + <see cref="EndDate"/> — a missing bound yields 422). The range is applied over the
/// charge's applied date. Extraordinary payments are INCLUDED: the payroll must discount them too.
/// </summary>
public sealed record ExportRecurringDeductionPayrollInputQuery(
    Guid CompanyId,
    string? PayrollTypeCode,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int? MaxRows) : IQuery<IReadOnlyCollection<InsumoPlanillaDescuentoExportRow>>;

// ── Validators ─────────────────────────────────────────────────────────────────────────────────────────

internal sealed class QueryRecurringDeductionsQueryValidator : AbstractValidator<QueryRecurringDeductionsQuery>
{
    public QueryRecurringDeductionsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).GreaterThan(0).LessThanOrEqualTo(200);
    }
}

internal sealed class ExportRecurringDeductionsQueryValidator : AbstractValidator<ExportRecurringDeductionsQuery>
{
    public ExportRecurringDeductionsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.MaxRows).GreaterThan(0).When(query => query.MaxRows.HasValue);
    }
}

internal sealed class QueryPendingRecurringDeductionInstallmentsQueryValidator
    : AbstractValidator<QueryPendingRecurringDeductionInstallmentsQuery>
{
    public QueryPendingRecurringDeductionInstallmentsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).GreaterThan(0).LessThanOrEqualTo(200);
    }
}

internal sealed class ExportPendingRecurringDeductionInstallmentsQueryValidator
    : AbstractValidator<ExportPendingRecurringDeductionInstallmentsQuery>
{
    public ExportPendingRecurringDeductionInstallmentsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.MaxRows).GreaterThan(0).When(query => query.MaxRows.HasValue);
    }
}

internal sealed class ExportRecurringDeductionPayrollInputQueryValidator
    : AbstractValidator<ExportRecurringDeductionPayrollInputQuery>
{
    public ExportRecurringDeductionPayrollInputQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.MaxRows).GreaterThan(0).When(query => query.MaxRows.HasValue);
    }
}
