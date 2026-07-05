using CLARIHR.Application.Common.CQRS;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>A row of the company-wide settlements bandeja (RF-006): one settlement or scenario per plaza.</summary>
public sealed record SettlementListItemResponse(
    Guid SettlementPublicId,
    Guid PersonnelFilePublicId,
    string EmployeeFullName,
    SettlementKind Kind,
    string? StatusCode,
    Guid AssignedPositionPublicId,
    string? PositionName,
    DateTime RequestDate,
    DateTime RetirementDate,
    string RetirementCategoryCode,
    string? RetirementCategoryName,
    string RetirementReasonCode,
    string? RetirementReasonName,
    string RequesterName,
    decimal TotalIncomes,
    decimal TotalDeductions,
    decimal NetPay,
    decimal TotalEmployerCharges,
    decimal ProvisionTotal,
    string CurrencyCode,
    DateTime? IssuedAtUtc,
    DateTime? AnnulledAtUtc);

/// <summary>The bandeja page: items + paging + per-status counts (scenarios count under the `ESCENARIO` key).</summary>
public sealed record SettlementBandejaResponse(
    IReadOnlyCollection<SettlementListItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    IReadOnlyDictionary<string, int> StatusCounts);

/// <summary>
/// An export row. The Excel/CSV writer turns the public property names into column headers (reflection), so
/// the property names are the Spanish headers seen by HR. Scenarios export with Tipo = ESCENARIO and the
/// SIMULACIÓN mark (R-10).
/// </summary>
public sealed record SettlementExportRow(
    string Empleado,
    string Plaza,
    string Tipo,
    string Estado,
    string Solicitante,
    DateTime FechaSolicitud,
    DateTime FechaRetiro,
    string Categoria,
    string Motivo,
    decimal TotalIngresos,
    decimal TotalDescuentos,
    decimal NetoAPagar,
    decimal PagosPatronales,
    decimal ReservaProvision,
    string Moneda,
    string? Observacion);

public sealed record QuerySettlementsQuery(
    Guid CompanyId,
    string? Kind,
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
    int PageSize = 25) : IQuery<SettlementBandejaResponse>;

public sealed record ExportSettlementsQuery(
    Guid CompanyId,
    string? Kind,
    string? StatusCode,
    string? CategoryCode,
    string? ReasonCode,
    Guid? EmployeeId,
    DateTime? RequestFromUtc,
    DateTime? RequestToUtc,
    DateTime? RetirementFromUtc,
    DateTime? RetirementToUtc,
    string? Search,
    int? MaxRows) : IQuery<IReadOnlyCollection<SettlementExportRow>>;

internal sealed class QuerySettlementsQueryValidator : AbstractValidator<QuerySettlementsQuery>
{
    public QuerySettlementsQueryValidator()
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

internal sealed class ExportSettlementsQueryValidator : AbstractValidator<ExportSettlementsQuery>
{
    public ExportSettlementsQueryValidator()
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
