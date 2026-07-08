using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>A row of the company-wide vacation-requests bandeja (leave module §3.9): one request per employee.</summary>
public sealed record VacationRequestListItemResponse(
    Guid VacationRequestPublicId,
    Guid PersonnelFilePublicId,
    string EmployeeFullName,
    string? EmployeeCode,
    Guid? RequesterFilePublicId,
    string? RequesterNameSnapshot,
    DateOnly StartDate,
    DateOnly EndDate,
    int RequestedDays,
    int ConsumedDays,
    int ReturnedDays,
    int NetConsumedDays,
    string StatusCode,
    DateTime? DecisionDateUtc,
    DateTime CreatedAtUtc)
{
    [JsonIgnore]
    public Guid Id => VacationRequestPublicId;
}

/// <summary>The bandeja page: items + paging + per-status counts (over the full non-status filter).</summary>
public sealed record VacationRequestBandejaResponse(
    IReadOnlyCollection<VacationRequestListItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    IReadOnlyDictionary<string, int> StatusCounts);

/// <summary>
/// A "goce de vacaciones" export row (leave module §3.9): an approved / partially-returned / returned request
/// with the enjoyment window, the requested / consumed / returned / net days and the periods of origin
/// (year: days). Property names are the Spanish headers (reflection-driven export writer).
/// </summary>
public sealed record GoceVacacionesExportRow(
    string Empleado,
    string? Codigo,
    string Estado,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    int DiasSolicitados,
    int DiasConsumidos,
    int DiasDevueltos,
    int DiasNetos,
    string PeriodosOrigen,
    DateTime? FechaDecision);

/// <summary>
/// Filters shared by the vacation-requests bandeja and export. When <see cref="StatusCode"/> is omitted the
/// bandeja shows every status; the StatusCounts are always computed over every status.
/// </summary>
public sealed record QueryVacationRequestsQuery(
    Guid CompanyId,
    Guid? EmployeeId,
    string? StatusCode,
    DateTime? StartFromUtc,
    DateTime? StartToUtc,
    int PageNumber = 1,
    int PageSize = 25) : IQuery<VacationRequestBandejaResponse>;

/// <summary>
/// The "goces" export filters: the export always restricts to the enjoyed set (APROBADA / DEVUELTA_PARCIAL /
/// DEVUELTA), optionally narrowed by employee and start-date range.
/// </summary>
public sealed record ExportVacationRequestsQuery(
    Guid CompanyId,
    Guid? EmployeeId,
    DateTime? StartFromUtc,
    DateTime? StartToUtc,
    int? MaxRows) : IQuery<IReadOnlyCollection<GoceVacacionesExportRow>>;

internal sealed class QueryVacationRequestsQueryValidator : AbstractValidator<QueryVacationRequestsQuery>
{
    public QueryVacationRequestsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.StartToUtc)
            .GreaterThanOrEqualTo(query => query.StartFromUtc!.Value)
            .When(query => query.StartFromUtc.HasValue && query.StartToUtc.HasValue);
    }
}

internal sealed class ExportVacationRequestsQueryValidator : AbstractValidator<ExportVacationRequestsQuery>
{
    public ExportVacationRequestsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.StartToUtc)
            .GreaterThanOrEqualTo(query => query.StartFromUtc!.Value)
            .When(query => query.StartFromUtc.HasValue && query.StartToUtc.HasValue);
    }
}
