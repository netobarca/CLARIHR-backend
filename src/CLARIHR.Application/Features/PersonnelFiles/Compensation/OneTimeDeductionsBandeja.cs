using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

// ── Bandeja corporativa ───────────────────────────────────────────────────────────────────────────────

/// <summary>A row of the company-wide one-time-deduction bandeja: one charge per employee with its value, its
/// requester and its lifecycle status. Every status is represented (a status filter narrows the items, never the
/// counts).</summary>
public sealed record OneTimeDeductionListItemResponse(
    Guid OneTimeDeductionPublicId,
    Guid PersonnelFilePublicId,
    string EmployeeFullName,
    string? EmployeeCode,
    DateOnly DeductionDate,
    string? Reference,
    string ConceptTypeCode,
    string ConceptNameSnapshot,
    bool IsFixedValue,
    string? CalculationMethod,
    decimal Amount,
    string CurrencyCode,
    Guid AssignedPositionPublicId,
    string RequesterNameSnapshot,
    string PayrollTypeCode,
    string PayrollPeriodLabel,
    string StatusCode,
    Guid? RequestedByUserId,
    Guid? DecidedByUserId)
{
    [JsonIgnore]
    public Guid Id => OneTimeDeductionPublicId;
}

/// <summary>The bandeja page: items + paging + per-status counts (over the full non-status filter) + the totals
/// per currency of what the company is charging.</summary>
public sealed record OneTimeDeductionBandejaResponse(
    IReadOnlyCollection<OneTimeDeductionListItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    IReadOnlyDictionary<string, int> StatusCounts,
    IReadOnlyDictionary<string, decimal> AmountByCurrency);

/// <summary>An export row of the bandeja (the Spanish property names become the column headers).</summary>
public sealed record DescuentoEventualExportRow(
    string Empleado,
    string? CodigoEmpleado,
    DateOnly Fecha,
    string? Referencia,
    string Concepto,
    bool ValorFijo,
    string? MetodoCalculo,
    decimal Monto,
    string Moneda,
    string Plaza,
    string Solicitante,
    string TipoPlanilla,
    string PeriodoPlanilla,
    string Estado);

public sealed record QueryOneTimeDeductionsQuery(
    Guid CompanyId,
    Guid? EmployeeId,
    string? StatusCode,
    string? ConceptTypeCode,
    string? PayrollTypeCode,
    DateOnly? DeductionFrom,
    DateOnly? DeductionTo,
    int PageNumber = 1,
    int PageSize = 25) : IQuery<OneTimeDeductionBandejaResponse>;

public sealed record ExportOneTimeDeductionsQuery(
    Guid CompanyId,
    Guid? EmployeeId,
    string? StatusCode,
    string? ConceptTypeCode,
    string? PayrollTypeCode,
    DateOnly? DeductionFrom,
    DateOnly? DeductionTo,
    int? MaxRows) : IQuery<IReadOnlyCollection<DescuentoEventualExportRow>>;

// ── Insumo de planilla ────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// One CHARGED one-off deduction of the payroll input: a row per APLICADA (active) application whose applied date
/// falls in the range, with the concept and the imputed period the payroll operator needs to discount it.
/// Reverted applications are excluded, so this export cuadra with what was actually charged.
/// </summary>
public sealed record InsumoPlanillaDescuentoEventualExportRow(
    string Empleado,
    string? CodigoEmpleado,
    string? Referencia,
    string Concepto,
    string TipoPlanilla,
    string? PeriodoPlanilla,
    DateOnly FechaAplicada,
    decimal Monto,
    string Moneda);

/// <summary>The payroll-input export: the charged deductions over a MANDATORY date range (a missing bound → 422).</summary>
public sealed record ExportOneTimeDeductionPayrollInputQuery(
    Guid CompanyId,
    string? PayrollTypeCode,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int? MaxRows) : IQuery<IReadOnlyCollection<InsumoPlanillaDescuentoEventualExportRow>>;

// ── Validators ─────────────────────────────────────────────────────────────────────────────────────────

internal sealed class QueryOneTimeDeductionsQueryValidator : AbstractValidator<QueryOneTimeDeductionsQuery>
{
    public QueryOneTimeDeductionsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).GreaterThan(0).LessThanOrEqualTo(200);
    }
}

internal sealed class ExportOneTimeDeductionsQueryValidator : AbstractValidator<ExportOneTimeDeductionsQuery>
{
    public ExportOneTimeDeductionsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.MaxRows).GreaterThan(0).When(query => query.MaxRows.HasValue);
    }
}

internal sealed class ExportOneTimeDeductionPayrollInputQueryValidator : AbstractValidator<ExportOneTimeDeductionPayrollInputQuery>
{
    public ExportOneTimeDeductionPayrollInputQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.MaxRows).GreaterThan(0).When(query => query.MaxRows.HasValue);
    }
}
