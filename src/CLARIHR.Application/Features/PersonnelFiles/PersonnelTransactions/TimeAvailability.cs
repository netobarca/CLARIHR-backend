using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles.PersonnelTransactions;

/// <summary>
/// The availability categories of the time-availability query (REQ-003 §3.11 / RF-013). F1 wires two families:
/// unpaid <c>SUSPENSION</c> blocks (source module EMPLOYEE_RELATIONS) and <c>FIN_CONTRATO_TEMPORAL</c> — the end
/// of temporary contracts (source module EMPLOYMENT). <see cref="ActiveSources"/> advertises those two so the
/// frontend shows which families are connected WITHOUT heuristics (aclaración №6). Connecting REQ-001/REQ-002
/// (VACACION, INCAPACIDAD, PERMISO) is additive: a new repository source method + a new category here — the wire
/// contract does not change.
/// </summary>
public static class TimeAvailabilityCategories
{
    public const string Suspension = "SUSPENSION";
    public const string TemporaryContractEnd = "FIN_CONTRATO_TEMPORAL";

    /// <summary>The families connected in F1 — always advertised in the response's <c>activeSources[]</c>.</summary>
    public static readonly IReadOnlyList<string> ActiveSources = [Suspension, TemporaryContractEnd];
}

/// <summary>Source-module tags that tell the frontend which subsystem produced each availability row.</summary>
public static class TimeAvailabilitySourceModules
{
    public const string EmployeeRelations = "EMPLOYEE_RELATIONS";
    public const string Employment = "EMPLOYMENT";
}

/// <summary>
/// A single availability row — the stable per-source shape of the planning view (aclaración №6). It carries the
/// MINIMAL payload (P-10): no cause, no facts, no amounts — only who is unavailable, when, why (category) and a
/// back-reference to the producing record (<see cref="ReferencePublicId"/> = the disciplinary action for a
/// suspension, the assignment/plaza for a temporary-contract end).
/// </summary>
public sealed record TimeAvailabilityRowResponse(
    Guid PersonnelFilePublicId,
    string EmployeeName,
    string? EmployeeCode,
    Guid? PositionPublicId,
    string? PositionName,
    string CategoryCode,
    DateOnly StartDate,
    DateOnly EndDate,
    int Days,
    string StatusCode,
    string SourceModule,
    Guid ReferencePublicId);

/// <summary>
/// The time-availability query page: the ordered rows (startDate asc, employee as tie-break), the total count,
/// the per-category counts and the <c>activeSources[]</c> (the two F1 families). PageNumber/PageSize echo the
/// request paging.
/// </summary>
public sealed record TimeAvailabilityQueryResponse(
    IReadOnlyCollection<TimeAvailabilityRowResponse> Rows,
    int PageNumber,
    int PageSize,
    int TotalCount,
    IReadOnlyDictionary<string, int> CategoryCounts,
    IReadOnlyCollection<string> ActiveSources);

/// <summary>
/// A time-availability export row (property names ARE the Spanish headers the reflection-based writer emits).
/// Mirrors the query rows: <see cref="Categoria"/> is the category code, <see cref="Fuente"/> the source module.
/// </summary>
public sealed record DisponibilidadTiempoExportRow(
    string Empleado,
    string? CodigoEmpleado,
    string? Plaza,
    string Categoria,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    int Dias,
    string Estado,
    string Fuente);

/// <summary>
/// The per-source filters the repository applies in SQL: the employee (personnel-file public id) and the org
/// unit. The category filter is applied by the handler (it decides which source methods to call). The
/// mandatory range travels as an <see cref="AvailabilityWindow"/> validated by the handler.
/// </summary>
public sealed record TimeAvailabilityFilters(Guid? PersonnelFilePublicId, Guid? OrgUnitPublicId);

/// <summary>
/// The time-availability query (REQ-003 §3.11): the date range is MANDATORY (a null range → 422
/// <c>TIME_AVAILABILITY_RANGE_REQUIRED</c>; start &gt; end → 422 <c>TIME_AVAILABILITY_RANGE_INVALID</c>, both
/// enforced by the handler, NOT FluentValidation, so they surface as 422 not 400). Optional filters: employee,
/// category codes and org unit. Paged 1..100.
/// </summary>
public sealed record TimeAvailabilityQuery(
    Guid CompanyId,
    DateOnly? StartDate,
    DateOnly? EndDate,
    Guid? PersonnelFilePublicId,
    IReadOnlyCollection<string>? CategoryCodes,
    Guid? OrgUnitPublicId,
    int PageNumber = 1,
    int PageSize = 50) : IQuery<TimeAvailabilityQueryResponse>;

/// <summary>The time-availability export query (same sources/filters; the handler validates the mandatory range).</summary>
public sealed record ExportTimeAvailabilityQuery(
    Guid CompanyId,
    DateOnly? StartDate,
    DateOnly? EndDate,
    Guid? PersonnelFilePublicId,
    IReadOnlyCollection<string>? CategoryCodes,
    Guid? OrgUnitPublicId,
    int? MaxRows) : IQuery<IReadOnlyCollection<DisponibilidadTiempoExportRow>>;

internal sealed class TimeAvailabilityQueryValidator : AbstractValidator<TimeAvailabilityQuery>
{
    public TimeAvailabilityQueryValidator()
    {
        // The range required/invalid check is enforced in the handler as a 422 (business rule), NOT here — a
        // FluentValidation failure would surface as a 400. Only the structural preconditions live here.
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

internal sealed class ExportTimeAvailabilityQueryValidator : AbstractValidator<ExportTimeAvailabilityQuery>
{
    public ExportTimeAvailabilityQueryValidator() => RuleFor(query => query.CompanyId).NotEmpty();
}

/// <summary>
/// Range errors for the time-availability query/export (RF-013). Both are 422: a missing bound
/// (<see cref="RangeRequired"/>) and an incoherent range with start &gt; end (<see cref="RangeInvalid"/>).
/// </summary>
internal static class TimeAvailabilityErrors
{
    public static readonly Error RangeRequired = new(
        "TIME_AVAILABILITY_RANGE_REQUIRED",
        "A start-date and end-date range is required for the time-availability query.",
        ErrorType.UnprocessableEntity);

    public static readonly Error RangeInvalid = new(
        "TIME_AVAILABILITY_RANGE_INVALID",
        "The time-availability range is invalid: the start date must be on or before the end date.",
        ErrorType.UnprocessableEntity);
}
