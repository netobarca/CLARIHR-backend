using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using FluentValidation;

namespace CLARIHR.Application.Features.Payroll;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// REQ-012 §3.7 (PR-7) — the reporting face of the motor: the corporate bandeja over the PERSISTED
// header (REQ-013 P-03 — totals are never recomputed here), the exports (bandeja + payroll print
// with per-concept/per-cost-center totals + bank reconciliation) and the EMPLOYEE axis (REQ-015):
// one corporate trans-run history endpoint (default CERRADA+AUTORIZADA; with GENERADA the SAME
// endpoint is the open-period actions/events view) and the employee's own self-service history
// (self-or-view gate, FIXED states, own file only).
// ─────────────────────────────────────────────────────────────────────────────────────────────

public sealed record PayrollRunListItemResponse(
    Guid PayrollRunPublicId,
    Guid PayrollDefinitionPublicId,
    Guid PayrollPeriodPublicId,
    string PayrollDefinitionCode,
    string PayrollDefinitionName,
    string PayrollTypeCode,
    string PeriodLabel,
    DateOnly PeriodStartDate,
    DateOnly PeriodEndDate,
    DateOnly? PaymentDate,
    string CurrencyCode,
    string StatusCode,
    int EmployeeCount,
    decimal TotalIncome,
    decimal TotalDeductions,
    decimal TotalEmployerCost,
    decimal TotalNet,
    DateTime GeneratedUtc,
    int RegeneratedCount)
{
    [JsonIgnore]
    public Guid Id => PayrollRunPublicId;
}

/// <summary>The bandeja page. <c>StatusCounts</c> ALWAYS spans every status — they are the tab numbers.</summary>
public sealed record PayrollRunBandejaResponse(
    IReadOnlyCollection<PayrollRunListItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    IReadOnlyDictionary<string, int> StatusCounts);

public sealed record QueryPayrollRunsQuery(
    Guid CompanyId,
    Guid? PayrollDefinitionPublicId,
    Guid? PayrollPeriodPublicId,
    string? StatusCode,
    int? Year,
    int PageNumber = 1,
    int PageSize = 25) : IQuery<PayrollRunBandejaResponse>;

/// <summary>A bandeja export row (the Spanish PascalCase property names ARE the column headers).</summary>
public sealed record CorridaPlanillaExportRow(
    string Nomina,
    string CodigoNomina,
    string TipoPlanilla,
    string Periodo,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    DateOnly? FechaPago,
    string Estado,
    int Empleados,
    decimal TotalIngresos,
    decimal TotalDescuentos,
    decimal CostoPatronal,
    decimal TotalNeto,
    string Moneda,
    int Regeneraciones);

public sealed record ExportPayrollRunsQuery(
    Guid CompanyId,
    Guid? PayrollDefinitionPublicId,
    Guid? PayrollPeriodPublicId,
    string? StatusCode,
    int? Year,
    int? MaxRows) : IQuery<IReadOnlyCollection<CorridaPlanillaExportRow>>;

/// <summary>
/// The payroll print (REQ-013 RF-003): DETALLE rows (every line with its audited final amount) followed
/// by TOTAL_POR_CONCEPTO and TOTAL_POR_CENTRO_COSTO summary rows computed over the INCLUDED lines.
/// </summary>
public sealed record ImpresionPlanillaExportRow(
    string TipoFila,
    string? Empleado,
    string? CodigoEmpleado,
    string? CentroCosto,
    string? Concepto,
    string? CodigoConcepto,
    string? Clase,
    decimal? Unidades,
    decimal? Base,
    decimal? Calculado,
    decimal? ValorManual,
    decimal Final,
    string? Incluida,
    string? Fuente,
    string Moneda);

public sealed record ExportPayrollRunLinesQuery(
    Guid CompanyId,
    Guid PayrollRunId,
    int? MaxRows) : IQuery<IReadOnlyCollection<ImpresionPlanillaExportRow>>;

/// <summary>
/// One bank-reconciliation row per employee of the run: payment method → bank → PRIMARY account (or the
/// profile's designated payment account) with the employee's net. A missing account never blocks — the
/// row travels with the <c>PAYROLL_WARNING_NO_BANK_ACCOUNT</c> warning (advertir-nunca-bloquear).
/// </summary>
public sealed record ConciliacionBancariaExportRow(
    string Empleado,
    string? CodigoEmpleado,
    string? FormaPago,
    string? Banco,
    string? TipoCuenta,
    string? NumeroCuenta,
    decimal Neto,
    string Moneda,
    string? Advertencia);

public sealed record ExportPayrollRunBankReconciliationQuery(
    Guid CompanyId,
    Guid PayrollRunId,
    int? MaxRows) : IQuery<IReadOnlyCollection<ConciliacionBancariaExportRow>>;

/// <summary>
/// Planilla Patronal (REQ-016 RF-003): one row per employee of the run with the employer cost — salario
/// base + cargas patronales (ISSS/AFP patronal + otras, p. ej. INCAF). Control interno (P-05) para validar
/// lo que se paga al gobierno — el total del reporte cuadra contra <c>PayrollRun.TotalEmployerCost</c>.
/// </summary>
public sealed record PlanillaPatronalExportRow(
    string Empleado,
    string? CodigoEmpleado,
    string? CentroCosto,
    decimal SalarioBase,
    decimal IsssPatronal,
    decimal AfpPatronal,
    decimal OtrasCargasPatronales,
    decimal CostoPatronalTotal,
    string Moneda);

public sealed record ExportEmployerCostReportQuery(
    Guid CompanyId,
    Guid PayrollRunId,
    int? MaxRows) : IQuery<IReadOnlyCollection<PlanillaPatronalExportRow>>;

public static class PayrollRunReportingConstants
{
    /// <summary>Row-type discriminators of the payroll print export.</summary>
    public const string DetailRow = "DETALLE";
    public const string ConceptTotalRow = "TOTAL_POR_CONCEPTO";
    public const string CostCenterTotalRow = "TOTAL_POR_CENTRO_COSTO";

    /// <summary>Stable warning of the bank reconciliation (plan §5 — never blocks the export).</summary>
    public const string NoBankAccountWarning = "PAYROLL_WARNING_NO_BANK_ACCOUNT";

    /// <summary>The REQ-015 default (its P-01): history = closed/authorized runs unless explicitly filtered.</summary>
    public static readonly IReadOnlyCollection<string> DefaultHistoryStatuses =
        [Domain.Payroll.PayrollRunStatuses.Cerrada, Domain.Payroll.PayrollRunStatuses.Autorizada];
}

// ── Employee axis (REQ-015) ───────────────────────────────────────────────────────────────────

/// <summary>One run where the employee has INCLUDED lines, with THEIR sums (not the run's totals).</summary>
public sealed record PayrollRunEmployeeHistoryItemResponse(
    Guid PayrollRunPublicId,
    string PayrollDefinitionCode,
    string PayrollDefinitionName,
    string PayrollTypeCode,
    string PeriodLabel,
    DateOnly PeriodStartDate,
    DateOnly PeriodEndDate,
    DateOnly? PaymentDate,
    string StatusCode,
    string CurrencyCode,
    decimal TotalIncome,
    decimal TotalDeductions,
    decimal TotalNet)
{
    [JsonIgnore]
    public Guid Id => PayrollRunPublicId;
}

public sealed record PayrollRunEmployeeHistoryResponse(
    Guid PersonnelFilePublicId,
    IReadOnlyCollection<PayrollRunEmployeeHistoryItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount);

/// <summary>
/// The corporate trans-run query (REQ-015 RF-001): default statuses CERRADA+AUTORIZADA (P-01);
/// passing GENERADA turns the SAME endpoint into the open-period actions/events view.
/// </summary>
public sealed record QueryPayrollRunEmployeeHistoryQuery(
    Guid CompanyId,
    Guid PersonnelFilePublicId,
    int? Year,
    Guid? PayrollDefinitionPublicId,
    string? PayrollTypeCode,
    IReadOnlyCollection<string>? StatusCodes,
    DateOnly? From,
    DateOnly? To,
    int PageNumber = 1,
    int PageSize = 25) : IQuery<PayrollRunEmployeeHistoryResponse>;

/// <summary>Self-service history (REQ-015 RF-005): own file only, FIXED states CERRADA/AUTORIZADA.</summary>
public sealed record GetMyPayrollHistoryQuery(
    Guid PersonnelFilePublicId,
    int? Year,
    int PageNumber = 1,
    int PageSize = 25) : IQuery<PayrollRunEmployeeHistoryResponse>;

/// <summary>Self-service drill: the employee's own lines of ONE closed/authorized run.</summary>
public sealed record GetMyPayrollHistoryRunLinesQuery(
    Guid PersonnelFilePublicId,
    Guid PayrollRunId) : IQuery<PayrollRunEmployeeLinesResponse>;

// ── Validators ────────────────────────────────────────────────────────────────────────────────

internal sealed class QueryPayrollRunsQueryValidator : AbstractValidator<QueryPayrollRunsQuery>
{
    public QueryPayrollRunsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).GreaterThan(0).LessThanOrEqualTo(200);
    }
}

internal sealed class ExportPayrollRunsQueryValidator : AbstractValidator<ExportPayrollRunsQuery>
{
    public ExportPayrollRunsQueryValidator() => RuleFor(query => query.CompanyId).NotEmpty();
}

internal sealed class ExportPayrollRunLinesQueryValidator : AbstractValidator<ExportPayrollRunLinesQuery>
{
    public ExportPayrollRunLinesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PayrollRunId).NotEmpty();
    }
}

internal sealed class ExportPayrollRunBankReconciliationQueryValidator
    : AbstractValidator<ExportPayrollRunBankReconciliationQuery>
{
    public ExportPayrollRunBankReconciliationQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PayrollRunId).NotEmpty();
    }
}

internal sealed class QueryPayrollRunEmployeeHistoryQueryValidator
    : AbstractValidator<QueryPayrollRunEmployeeHistoryQuery>
{
    public QueryPayrollRunEmployeeHistoryQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PersonnelFilePublicId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).GreaterThan(0).LessThanOrEqualTo(200);
    }
}

internal sealed class GetMyPayrollHistoryQueryValidator : AbstractValidator<GetMyPayrollHistoryQuery>
{
    public GetMyPayrollHistoryQueryValidator()
    {
        RuleFor(query => query.PersonnelFilePublicId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).GreaterThan(0).LessThanOrEqualTo(200);
    }
}

internal sealed class GetMyPayrollHistoryRunLinesQueryValidator : AbstractValidator<GetMyPayrollHistoryRunLinesQuery>
{
    public GetMyPayrollHistoryRunLinesQueryValidator()
    {
        RuleFor(query => query.PersonnelFilePublicId).NotEmpty();
        RuleFor(query => query.PayrollRunId).NotEmpty();
    }
}
