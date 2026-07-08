using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>A row of the company-wide incapacities bandeja (RF-013): one incapacity per employee, with days + amounts.</summary>
public sealed record IncapacityListItemResponse(
    Guid IncapacityPublicId,
    Guid PersonnelFilePublicId,
    string EmployeeFullName,
    string? EmployeeCode,
    Guid? AssignedPositionPublicId,
    string RiskCode,
    Guid IncapacityTypePublicId,
    string IncapacityTypeCode,
    string? IncapacityTypeName,
    string? MedicalClinicName,
    string StatusCode,
    string OriginCode,
    DateOnly StartDate,
    DateOnly? EndDate,
    int CalendarDays,
    int ComputableDays,
    int SubsidizedDays,
    int DiscountDays,
    int EmployerDays,
    decimal SubsidyAmount,
    decimal DiscountAmount,
    decimal EmployerAmount,
    string? PayrollTypeCode,
    string? PayrollPeriodLabel,
    bool UsesFund)
{
    [JsonIgnore]
    public Guid Id => IncapacityPublicId;
}

/// <summary>The bandeja page: items + paging + per-status counts (over the full non-status filter, so every
/// status is represented even though the items default to REGISTRADA).</summary>
public sealed record IncapacityBandejaResponse(
    IReadOnlyCollection<IncapacityListItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    IReadOnlyDictionary<string, int> StatusCounts);

/// <summary>
/// An export row. The Excel/CSV/JSON writer turns the public property names into column headers (reflection),
/// so the property names are the Spanish headers seen by HR. The per-tranche breakdown is flattened from the
/// entity's <c>TrancheDetailJson</c> (jsonb) into <see cref="PorcentajesPorTramo"/>. The "/30" daily-salary
/// convention (D-21) travels documented in the base-salary columns.
/// </summary>
public sealed record IncapacidadExportRow(
    string Empleado,
    string? Codigo,
    string Plaza,
    string Riesgo,
    string Tipo,
    string? Clinica,
    string Estado,
    string Origen,
    DateOnly FechaInicio,
    DateOnly? FechaFin,
    int DiasNaturales,
    int DiasComputables,
    int DiasSubsidiados,
    int DiasDescuento,
    int DiasPatrono,
    decimal MontoSubsidiado,
    decimal MontoDescuento,
    decimal MontoPatrono,
    string PorcentajesPorTramo,
    decimal BaseMensual,
    decimal BaseDiaria,
    string? TipoPlanilla,
    string? PeriodoPlanilla,
    DateOnly? PeriodoInicio,
    DateOnly? PeriodoFin,
    bool UtilizaFondo);

/// <summary>
/// Filters shared by the incapacities bandeja and export. When <see cref="StatusCode"/> is not supplied the
/// items default to REGISTRADA — this excludes the EN_REVISION self-registrations from the payroll input
/// (RN — R-T6). The StatusCounts, however, are computed over every status.
/// </summary>
public sealed record QueryIncapacitiesQuery(
    Guid CompanyId,
    Guid? EmployeeId,
    string? RiskCode,
    string? IncapacityTypeCode,
    string? StatusCode,
    string? PayrollTypeCode,
    DateTime? StartFromUtc,
    DateTime? StartToUtc,
    int PageNumber = 1,
    int PageSize = 25) : IQuery<IncapacityBandejaResponse>;

public sealed record ExportIncapacitiesQuery(
    Guid CompanyId,
    Guid? EmployeeId,
    string? RiskCode,
    string? IncapacityTypeCode,
    string? StatusCode,
    string? PayrollTypeCode,
    DateTime? StartFromUtc,
    DateTime? StartToUtc,
    int? MaxRows) : IQuery<IReadOnlyCollection<IncapacidadExportRow>>;

internal sealed class QueryIncapacitiesQueryValidator : AbstractValidator<QueryIncapacitiesQuery>
{
    public QueryIncapacitiesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.StartToUtc)
            .GreaterThanOrEqualTo(query => query.StartFromUtc!.Value)
            .When(query => query.StartFromUtc.HasValue && query.StartToUtc.HasValue);
    }
}

internal sealed class ExportIncapacitiesQueryValidator : AbstractValidator<ExportIncapacitiesQuery>
{
    public ExportIncapacitiesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.StartToUtc)
            .GreaterThanOrEqualTo(query => query.StartFromUtc!.Value)
            .When(query => query.StartFromUtc.HasValue && query.StartToUtc.HasValue);
    }
}
