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

/// <summary>Derived interview state of a tray row (RF-008).</summary>
public static class RetirementInterviewStatuses
{
    /// <summary>No published form is active for the employee's retirement reason.</summary>
    public const string SinFormulario = "SIN_FORMULARIO";

    /// <summary>A form applies but the employee has no (non-archived) submission yet.</summary>
    public const string Pendiente = "PENDIENTE";

    /// <summary>The employee has a draft submission.</summary>
    public const string Borrador = "BORRADOR";

    /// <summary>The employee's submission was submitted (immutable).</summary>
    public const string Enviada = "ENVIADA";
}

/// <summary>
/// A row of the interview tray (RF-008): an employee whose retirement is AUTORIZADA/EJECUTADA (D-07) with the
/// derived state of their exit interview. REVERTIDA/ANULADA/RECHAZADA never appear (RN-008.2).
/// </summary>
public sealed record RetirementInterviewTrayItemResponse(
    Guid RetirementRequestPublicId,
    Guid PersonnelFilePublicId,
    string EmployeeFullName,
    string RetirementCategoryCode,
    string? RetirementCategoryName,
    string RetirementReasonCode,
    string? RetirementReasonName,
    DateTime RetirementDate,
    string RequestStatusCode,
    string InterviewStatus,
    Guid? SubmissionPublicId);

public sealed record GetRetirementInterviewTrayQuery(
    Guid CompanyId,
    string? InterviewStatus,
    string? CategoryCode,
    string? ReasonCode,
    DateTime? RetirementFromUtc,
    DateTime? RetirementToUtc) : IQuery<IReadOnlyCollection<RetirementInterviewTrayItemResponse>>;

internal sealed class GetRetirementInterviewTrayQueryValidator : AbstractValidator<GetRetirementInterviewTrayQuery>
{
    public GetRetirementInterviewTrayQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.RetirementToUtc)
            .GreaterThanOrEqualTo(query => query.RetirementFromUtc!.Value)
            .When(query => query.RetirementFromUtc.HasValue && query.RetirementToUtc.HasValue);
    }
}
