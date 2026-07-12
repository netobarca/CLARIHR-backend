using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles.Absences;

// ── Bandeja corporativa ───────────────────────────────────────────────────────────────────────────────

public sealed record NotWorkedTimeListItemResponse(
    Guid NotWorkedTimePublicId,
    Guid PersonnelFilePublicId,
    string EmployeeFullName,
    string? EmployeeCode,
    string TypeCode,
    string TypeName,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal? Hours,
    int ComputableDays,
    int SeventhDayPenaltyDays,
    decimal DiscountedDays,
    decimal DiscountAmount,
    string CurrencyCode,
    string StatusCode)
{
    [JsonIgnore]
    public Guid Id => NotWorkedTimePublicId;
}

/// <summary>The bandeja page. <c>StatusCounts</c> and <c>AmountByCurrency</c> ALWAYS span every status, even when the
/// items are filtered to one — they are the numbers of the tabs.</summary>
public sealed record NotWorkedTimeBandejaResponse(
    IReadOnlyCollection<NotWorkedTimeListItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    IReadOnlyDictionary<string, int> StatusCounts,
    IReadOnlyDictionary<string, decimal> AmountByCurrency);

/// <summary>An export row (the Spanish PascalCase property names ARE the column headers).</summary>
public sealed record TiempoNoTrabajadoExportRow(
    string Empleado,
    string? CodigoEmpleado,
    string Tipo,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    decimal? Horas,
    int DiasComputables,
    int DiasSeptimo,
    decimal DiasDescontados,
    decimal Monto,
    string Moneda,
    string Estado);

/// <summary>
/// One row of the payroll input: a REGISTERED (never annulled) record, with the deduction concept the payroll
/// operator must charge it to. An annulled record is NOT here — the payroll must never discount money the company
/// gave back.
/// </summary>
public sealed record InsumoPlanillaTiempoNoTrabajadoExportRow(
    string Empleado,
    string? CodigoEmpleado,
    string Tipo,
    string? ConceptoEgreso,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    decimal DiasDescontados,
    decimal Monto,
    string Moneda);

public sealed record QueryNotWorkedTimesQuery(
    Guid CompanyId,
    Guid? EmployeeId,
    string? StatusCode,
    string? TypeCode,
    DateOnly? From,
    DateOnly? To,
    int PageNumber = 1,
    int PageSize = 25) : IQuery<NotWorkedTimeBandejaResponse>;

public sealed record ExportNotWorkedTimesQuery(
    Guid CompanyId,
    Guid? EmployeeId,
    string? StatusCode,
    string? TypeCode,
    DateOnly? From,
    DateOnly? To,
    int? MaxRows) : IQuery<IReadOnlyCollection<TiempoNoTrabajadoExportRow>>;

/// <summary>The payroll input over a MANDATORY date range (a missing bound → 422).</summary>
public sealed record ExportNotWorkedTimePayrollInputQuery(
    Guid CompanyId,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int? MaxRows) : IQuery<IReadOnlyCollection<InsumoPlanillaTiempoNoTrabajadoExportRow>>;

// ── Validators ────────────────────────────────────────────────────────────────────────────────────────

internal sealed class QueryNotWorkedTimesQueryValidator : AbstractValidator<QueryNotWorkedTimesQuery>
{
    public QueryNotWorkedTimesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).GreaterThan(0).LessThanOrEqualTo(200);
    }
}

internal sealed class ExportNotWorkedTimesQueryValidator : AbstractValidator<ExportNotWorkedTimesQuery>
{
    public ExportNotWorkedTimesQueryValidator() => RuleFor(query => query.CompanyId).NotEmpty();
}

internal sealed class ExportNotWorkedTimePayrollInputQueryValidator
    : AbstractValidator<ExportNotWorkedTimePayrollInputQuery>
{
    public ExportNotWorkedTimePayrollInputQueryValidator() => RuleFor(query => query.CompanyId).NotEmpty();
}
