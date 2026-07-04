using CLARIHR.Application.Common.CQRS;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>A row of the company-wide retirement-request bandeja (RF-002).</summary>
public sealed record RetirementRequestListItemResponse(
    Guid RetirementRequestPublicId,
    Guid PersonnelFilePublicId,
    string EmployeeFullName,
    string RequesterName,
    DateTime RequestDate,
    DateTime RetirementDate,
    string RetirementCategoryCode,
    string? RetirementCategoryName,
    string RetirementReasonCode,
    string? RetirementReasonName,
    string RequestStatusCode,
    DateTime? ResolutionDateUtc,
    DateTime? ExecutionDateUtc,
    DateTime? ReversalDateUtc);

/// <summary>The company-wide bandeja page: items + paging + per-status counts (RF-002).</summary>
public sealed record RetirementRequestBandejaResponse(
    IReadOnlyCollection<RetirementRequestListItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    IReadOnlyDictionary<string, int> StatusCounts);

/// <summary>
/// An export row. The Excel/CSV writer turns the public property names into column headers (reflection), so
/// the property names are the Spanish headers seen by HR.
/// </summary>
public sealed record RetirementRequestExportRow(
    string Empleado,
    string Solicitante,
    DateTime FechaSolicitud,
    DateTime FechaRetiro,
    string Categoria,
    string Motivo,
    string Estado,
    DateTime? FechaAutorizacion,
    DateTime? FechaEjecucion,
    DateTime? FechaReversion,
    string? Observacion);

public sealed record QueryRetirementRequestsQuery(
    Guid CompanyId,
    string? StatusCode,
    string? CategoryCode,
    string? ReasonCode,
    Guid? EmployeeId,
    DateTime? RequestFromUtc,
    DateTime? RequestToUtc,
    DateTime? RetirementFromUtc,
    DateTime? RetirementToUtc,
    string? Search,
    int PageNumber = 1,
    int PageSize = 25) : IQuery<RetirementRequestBandejaResponse>;

public sealed record ExportRetirementRequestsQuery(
    Guid CompanyId,
    string? StatusCode,
    string? CategoryCode,
    string? ReasonCode,
    Guid? EmployeeId,
    DateTime? RequestFromUtc,
    DateTime? RequestToUtc,
    DateTime? RetirementFromUtc,
    DateTime? RetirementToUtc,
    string? Search,
    int? MaxRows) : IQuery<IReadOnlyCollection<RetirementRequestExportRow>>;

internal sealed class QueryRetirementRequestsQueryValidator : AbstractValidator<QueryRetirementRequestsQuery>
{
    public QueryRetirementRequestsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.RequestToUtc)
            .GreaterThanOrEqualTo(query => query.RequestFromUtc!.Value)
            .When(query => query.RequestFromUtc.HasValue && query.RequestToUtc.HasValue);
        RuleFor(query => query.RetirementToUtc)
            .GreaterThanOrEqualTo(query => query.RetirementFromUtc!.Value)
            .When(query => query.RetirementFromUtc.HasValue && query.RetirementToUtc.HasValue);
    }
}

internal sealed class ExportRetirementRequestsQueryValidator : AbstractValidator<ExportRetirementRequestsQuery>
{
    public ExportRetirementRequestsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.RequestToUtc)
            .GreaterThanOrEqualTo(query => query.RequestFromUtc!.Value)
            .When(query => query.RequestFromUtc.HasValue && query.RequestToUtc.HasValue);
        RuleFor(query => query.RetirementToUtc)
            .GreaterThanOrEqualTo(query => query.RetirementFromUtc!.Value)
            .When(query => query.RetirementFromUtc.HasValue && query.RetirementToUtc.HasValue);
    }
}
