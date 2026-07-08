using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// The two unified operations of a compensatory-time movement in the company bandeja: a credit
/// ("ACREDITACION", hours +) or an absence ("AUSENCIA", hours −). These label the credit/absence sub-resources
/// as one movement stream — they are NOT the type OPERATION codes (ACREDITA/DEBITA/AMBAS of the master).
/// </summary>
public static class CompensatoryTimeMovementOperations
{
    public const string Acreditacion = "ACREDITACION";
    public const string Ausencia = "AUSENCIA";
}

/// <summary>
/// A row of the company-wide compensatory-time movements bandeja (REQ-002 §3.9): one credit or absence per
/// employee, projected into a common movement shape. <see cref="SignedHours"/> is the signed magnitude
/// (+ credit / − absence). <see cref="HoursWorked"/>/<see cref="Factor"/> are the credit-only inputs (null for
/// an absence); <see cref="EndDate"/> and the payroll-period fields travel only for an absence.
/// </summary>
public sealed record CompensatoryTimeMovementListItemResponse(
    Guid MovementPublicId,
    Guid PersonnelFilePublicId,
    string EmployeeFullName,
    string? EmployeeCode,
    string OperationCode,
    Guid CompensatoryTimeTypePublicId,
    string CompensatoryTimeTypeCode,
    string TypeNameSnapshot,
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal? HoursWorked,
    decimal? Factor,
    decimal SignedHours,
    string Detail,
    string? AuthorizedByText,
    string StatusCode,
    string? PayrollPeriodLabel,
    DateOnly? PayrollPeriodStart,
    DateOnly? PayrollPeriodEnd,
    DateTime RegisteredUtc)
{
    [JsonIgnore]
    public Guid Id => MovementPublicId;
}

/// <summary>The bandeja page: items + paging + per-status counts (over the full non-status filter, so both
/// REGISTRADA and ANULADA are represented even though the items default to REGISTRADA only).</summary>
public sealed record CompensatoryTimeMovementBandejaResponse(
    IReadOnlyCollection<CompensatoryTimeMovementListItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    IReadOnlyDictionary<string, int> StatusCounts);

/// <summary>
/// A movement export row. The Excel/CSV/JSON writer turns the public property names into column headers
/// (reflection), so the property names ARE the Spanish headers seen by HR. <see cref="Horas"/> is the signed
/// magnitude (+ credit / − absence); <see cref="FechaInicio"/> is the worked date for a credit / the start date
/// for an absence; <see cref="FechaFin"/> and the periodo columns travel only for an absence. Annulled
/// movements are excluded by default (the caller passes <c>includeAnnulled=false</c>).
/// </summary>
public sealed record MovimientoTiempoCompensatorioExportRow(
    string Empleado,
    string? CodigoEmpleado,
    string Operacion,
    string Tipo,
    DateOnly FechaInicio,
    DateOnly? FechaFin,
    decimal? HorasTrabajadas,
    decimal? Factor,
    decimal Horas,
    string Detalle,
    string? AutorizadoPor,
    string Estado,
    string? PeriodoPlanilla,
    DateOnly? PeriodoInicio,
    DateOnly? PeriodoFin,
    DateTime FechaRegistro);

/// <summary>
/// A balance export row: the fund totals of one employee with vigente movements (REQ-002 §3.9).
/// <see cref="SaldoDisponible"/> = <see cref="TotalAcreditado"/> − <see cref="TotalDebitado"/> (over REGISTRADA
/// movements only — matches <c>GetBalanceAsync</c>/<c>CompensatoryTimeRules.Balance</c> by construction).
/// </summary>
public sealed record SaldoTiempoCompensatorioExportRow(
    string Empleado,
    string? CodigoEmpleado,
    decimal TotalAcreditado,
    decimal TotalDebitado,
    decimal SaldoDisponible,
    DateOnly? UltimoMovimiento);

/// <summary>
/// Filters shared by the movements bandeja and export. When <see cref="StatusCode"/> is supplied the items are
/// filtered to exactly that status (so ANULADA can be queried explicitly); otherwise <see cref="IncludeAnnulled"/>
/// (default false) decides whether the ANULADA movements are shown — the DEFAULT excludes them. The StatusCounts
/// are computed over EVERY status (respecting the other filters). <see cref="OperationCode"/> restricts to
/// credits (ACREDITACION) or absences (AUSENCIA); <see cref="FromDate"/>/<see cref="ToDate"/> filter the worked
/// date (credit) / start date (absence).
/// </summary>
public sealed record QueryCompensatoryTimeMovementsQuery(
    Guid CompanyId,
    Guid? EmployeeId,
    Guid? CompensatoryTimeTypePublicId,
    string? OperationCode,
    string? StatusCode,
    DateOnly? FromDate,
    DateOnly? ToDate,
    bool IncludeAnnulled = false,
    int PageNumber = 1,
    int PageSize = 25) : IQuery<CompensatoryTimeMovementBandejaResponse>;

public sealed record ExportCompensatoryTimeMovementsQuery(
    Guid CompanyId,
    Guid? EmployeeId,
    Guid? CompensatoryTimeTypePublicId,
    string? OperationCode,
    string? StatusCode,
    DateOnly? FromDate,
    DateOnly? ToDate,
    bool IncludeAnnulled,
    int? MaxRows) : IQuery<IReadOnlyCollection<MovimientoTiempoCompensatorioExportRow>>;

public sealed record ExportCompensatoryTimeBalancesQuery(
    Guid CompanyId,
    Guid? EmployeeId,
    int? MaxRows) : IQuery<IReadOnlyCollection<SaldoTiempoCompensatorioExportRow>>;

internal sealed class QueryCompensatoryTimeMovementsQueryValidator : AbstractValidator<QueryCompensatoryTimeMovementsQuery>
{
    public QueryCompensatoryTimeMovementsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.ToDate)
            .GreaterThanOrEqualTo(query => query.FromDate!.Value)
            .When(query => query is { FromDate: not null, ToDate: not null });
    }
}

internal sealed class ExportCompensatoryTimeMovementsQueryValidator : AbstractValidator<ExportCompensatoryTimeMovementsQuery>
{
    public ExportCompensatoryTimeMovementsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.ToDate)
            .GreaterThanOrEqualTo(query => query.FromDate!.Value)
            .When(query => query is { FromDate: not null, ToDate: not null });
    }
}

internal sealed class ExportCompensatoryTimeBalancesQueryValidator : AbstractValidator<ExportCompensatoryTimeBalancesQuery>
{
    public ExportCompensatoryTimeBalancesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
    }
}
