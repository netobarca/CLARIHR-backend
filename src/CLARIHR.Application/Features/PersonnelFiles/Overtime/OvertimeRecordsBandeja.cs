using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

// ── Bandeja de horas extras (RF-011) ─────────────────────────────────────────────────────────────────

/// <summary>
/// A row of the company-wide overtime-record bandeja (RF-011): one overtime shift with its employee identity, the
/// shift (work date, overtime type + snapshot, the type factor snapshot + the applied factor, the h:m duration
/// with the derived decimal hours), the motive, the origin channel (RRHH / PORTAL — the dual channel of P-01), the
/// plaza, the requester, the payroll destination and its lifecycle status. Every status is represented (annulled /
/// rejected included with their status — a status filter is available). User ids are null-safe (a non-Guid
/// principal → null — lesson REQ-003). NO money travels — the module is hours × factor only (§0.1/§0.16).
/// </summary>
public sealed record OvertimeRecordListItemResponse(
    Guid OvertimeRecordPublicId,
    Guid PersonnelFilePublicId,
    string EmployeeFullName,
    string? EmployeeCode,
    DateOnly WorkDate,
    Guid OvertimeTypePublicId,
    string OvertimeTypeCodeSnapshot,
    string OvertimeTypeNameSnapshot,
    decimal TypeFactorSnapshot,
    decimal FactorApplied,
    int DurationHours,
    int DurationMinutes,
    decimal DurationDecimalHours,
    Guid JustificationTypePublicId,
    string JustificationCodeSnapshot,
    string JustificationNameSnapshot,
    string OriginChannel,
    Guid AssignedPositionPublicId,
    Guid RequesterFilePublicId,
    string RequesterNameSnapshot,
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    string PayrollPeriodLabel,
    DateOnly? PayrollPeriodEndDate,
    string StatusCode,
    Guid? RequestedByUserId,
    Guid? DecidedByUserId)
{
    [JsonIgnore]
    public Guid Id => OvertimeRecordPublicId;
}

/// <summary>
/// One totals-by-type bucket (§0.16): the overtime type code + name, the row count and the total decimal HOURS of
/// the type (a <c>GroupBy</c> in the DB over <c>duration_decimal_hours</c>). Totals are EN HORAS, never money — the
/// module carries no currency (unlike the REQ-006 groupBy, which sums by currency).
/// </summary>
public sealed record OvertimeTotalsByTypeResponse(
    string OvertimeTypeCode,
    string OvertimeTypeName,
    int Count,
    decimal TotalHours);

/// <summary>
/// The bandeja page: items + paging + per-status counts (over the full non-status filter, so every status is
/// represented even when the items are narrowed to a status), the global total decimal HOURS of the filtered set
/// and the totals-by-type buckets (§0.16). The totals CUADRAN by construction: Σ <c>TotalsByType.TotalHours</c> ==
/// <c>TotalHours</c> (both aggregate the same <c>duration_decimal_hours</c> over the same filtered set). There is
/// NO dimensional <c>groupBy</c> here (the overtime levantamiento does not ask for it — §0.16).
/// </summary>
public sealed record OvertimeRecordBandejaResponse(
    IReadOnlyCollection<OvertimeRecordListItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    IReadOnlyDictionary<string, int> StatusCounts,
    decimal TotalHours,
    IReadOnlyCollection<OvertimeTotalsByTypeResponse> TotalsByType);

// ── Export rows (Spanish property names → column headers) ───────────────────────────────────────────────

/// <summary>An export row of the overtime bandeja. The Excel/CSV/JSON writer turns the public property names into
/// column headers (reflection), so they are the Spanish headers seen by HR. <c>DuracionHoras</c> is the derived
/// decimal hours (2 h 30 m = 2.50).</summary>
public sealed record HoraExtraExportRow(
    string Empleado,
    string? CodigoEmpleado,
    DateOnly FechaJornada,
    string TipoHoraExtra,
    string TipoHoraExtraNombre,
    string Justificacion,
    decimal Factor,
    decimal DuracionHoras,
    string Canal,
    string Plaza,
    string TipoPlanilla,
    string Periodo,
    string Solicitante,
    string Estado,
    string? RegistradoPor,
    string? DecididoPor);

/// <summary>An export row of the pending / overdue tray (Spanish property names → headers). <c>DuracionHoras</c> is
/// the derived decimal hours.</summary>
public sealed record HoraExtraPendienteExportRow(
    string Empleado,
    string? CodigoEmpleado,
    DateOnly FechaJornada,
    string TipoHoraExtra,
    string TipoHoraExtraNombre,
    decimal Factor,
    decimal DuracionHoras,
    string TipoPlanilla,
    string Periodo,
    DateOnly? FinPeriodo,
    bool Vencido);

/// <summary>
/// An export row of the PAYROLL INPUT for an external payroll system: the pending (AUTORIZADA, elapsed, not
/// compensated) overtime records of a mandatory payroll type + period. Cuadra against the pending tray of the same
/// filter (excludes annulled + applied + compensated + future). The cost center is derived from the plaza at export
/// time (join to the employment assignment, D-12 — no monetary snapshot lives on the record). The Spanish property
/// names are the export headers.
/// </summary>
public sealed record InsumoPlanillaHoraExtraExportRow(
    string Empleado,
    string? CodigoEmpleado,
    string TipoHoraExtra,
    string TipoHoraExtraNombre,
    decimal Factor,
    decimal DuracionHoras,
    string TipoPlanilla,
    string Periodo,
    string CentroCosto);

// ── Filters ────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>The shared (non-status) filter surface of the overtime bandeja query + export, so the repository
/// composes ONE filter helper over both (the status codes are applied separately — the StatusCounts always span
/// every status).</summary>
public interface IOvertimeRecordFilters
{
    Guid? EmployeeId { get; }

    Guid? OvertimeTypePublicId { get; }

    Guid? JustificationTypePublicId { get; }

    DateOnly? FromWorkDate { get; }

    DateOnly? ToWorkDate { get; }

    string? PayrollTypeCode { get; }

    string? PayrollPeriod { get; }

    Guid? RequesterFilePublicId { get; }

    string? OriginChannel { get; }

    Guid? AssignedPositionPublicId { get; }

    string? Search { get; }
}

// ── Queries ────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Filters of the overtime advanced search (RF-011). Every filter is optional; when <see cref="StatusCodes"/> is
/// empty every status is listed (the StatusCounts always span every status). The response carries the paginated
/// items, the per-status counts, the global total HOURS and the totals-by-type buckets (§0.16). There is NO
/// dimensional groupBy.
/// </summary>
public sealed record QueryOvertimeRecordsQuery(
    Guid CompanyId,
    IReadOnlyCollection<string>? StatusCodes,
    Guid? EmployeeId,
    Guid? OvertimeTypePublicId,
    Guid? JustificationTypePublicId,
    DateOnly? FromWorkDate,
    DateOnly? ToWorkDate,
    string? PayrollTypeCode,
    string? PayrollPeriod,
    Guid? RequesterFilePublicId,
    string? OriginChannel,
    Guid? AssignedPositionPublicId,
    string? Search,
    int PageNumber = 1,
    int PageSize = 25) : IQuery<OvertimeRecordBandejaResponse>, IOvertimeRecordFilters;

public sealed record ExportOvertimeRecordsQuery(
    Guid CompanyId,
    IReadOnlyCollection<string>? StatusCodes,
    Guid? EmployeeId,
    Guid? OvertimeTypePublicId,
    Guid? JustificationTypePublicId,
    DateOnly? FromWorkDate,
    DateOnly? ToWorkDate,
    string? PayrollTypeCode,
    string? PayrollPeriod,
    Guid? RequesterFilePublicId,
    string? OriginChannel,
    Guid? AssignedPositionPublicId,
    string? Search,
    int? MaxRows) : IQuery<IReadOnlyCollection<HoraExtraExportRow>>, IOvertimeRecordFilters;

public sealed record ExportOvertimeRecordPendingQuery(
    Guid CompanyId,
    string? PayrollTypeCode,
    bool OnlyOverdue,
    int? MaxRows) : IQuery<IReadOnlyCollection<HoraExtraPendienteExportRow>>;

/// <summary>The payroll-input export (§0.16): the pending overtime records of a MANDATORY payroll type + period
/// (a missing bound yields 400). Cuadra against the pending tray of the same filter (excludes annulled + applied +
/// compensated + future).</summary>
public sealed record ExportOvertimeRecordPayrollInputQuery(
    Guid CompanyId,
    string? PayrollTypeCode,
    string? PayrollPeriod,
    int? MaxRows) : IQuery<IReadOnlyCollection<InsumoPlanillaHoraExtraExportRow>>;

// ── Validators ─────────────────────────────────────────────────────────────────────────────────────────

internal sealed class QueryOvertimeRecordsQueryValidator : AbstractValidator<QueryOvertimeRecordsQuery>
{
    public QueryOvertimeRecordsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.ToWorkDate)
            .GreaterThanOrEqualTo(query => query.FromWorkDate!.Value)
            .When(query => query.FromWorkDate.HasValue && query.ToWorkDate.HasValue);
    }
}

internal sealed class ExportOvertimeRecordsQueryValidator : AbstractValidator<ExportOvertimeRecordsQuery>
{
    public ExportOvertimeRecordsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.ToWorkDate)
            .GreaterThanOrEqualTo(query => query.FromWorkDate!.Value)
            .When(query => query.FromWorkDate.HasValue && query.ToWorkDate.HasValue);
    }
}

internal sealed class ExportOvertimeRecordPendingQueryValidator : AbstractValidator<ExportOvertimeRecordPendingQuery>
{
    public ExportOvertimeRecordPendingQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
    }
}

internal sealed class ExportOvertimeRecordPayrollInputQueryValidator : AbstractValidator<ExportOvertimeRecordPayrollInputQuery>
{
    public ExportOvertimeRecordPayrollInputQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();

        // The mandatory payroll type + period are enforced in the handler as a 400
        // (OVERTIME_PAYROLL_INPUT_FILTER_REQUIRED) so the operator gets an actionable code.
    }
}

// ── Handler-level errors (each needs an EN + ES resource entry — localization parity) ────────────────

/// <summary>Dedicated errors for the overtime bandeja + exports slice (REQ-007 PR-5): the mandatory payroll-input
/// filter (400 with an actionable code).</summary>
internal static class OvertimeRecordBandejaErrors
{
    public static readonly Error PayrollInputFilterRequired = new(
        "OVERTIME_PAYROLL_INPUT_FILTER_REQUIRED",
        "A payroll type and a payroll period are required to export the overtime payroll input.",
        ErrorType.Validation);
}
