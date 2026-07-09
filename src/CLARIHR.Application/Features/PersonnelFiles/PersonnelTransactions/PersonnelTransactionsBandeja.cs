using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles.PersonnelTransactions;

/// <summary>
/// The two payroll effects a company can extract from an applied disciplinary action (REQ-003 RF-012): a
/// one-off deduction (<c>DESCUENTO</c>) and an unpaid suspension (<c>SUSPENSION_SIN_GOCE</c>). One applied
/// disciplinary action can carry BOTH — the payroll input then emits one row per effect.
/// </summary>
public static class PersonnelTransactionPayrollEffects
{
    public const string Deduction = "DESCUENTO";
    public const string UnpaidSuspension = "SUSPENSION_SIN_GOCE";
}

// ── Recognitions bandeja (§3.9) ───────────────────────────────────────────────────────────────────

/// <summary>A row of the company-wide recognitions bandeja (RF-012): one recognition per employee.</summary>
public sealed record RecognitionListItemResponse(
    Guid RecognitionPublicId,
    Guid PersonnelFilePublicId,
    string EmployeeFullName,
    string? EmployeeCode,
    Guid RecognitionTypePublicId,
    string RecognitionTypeCode,
    string TypeNameSnapshot,
    DateOnly EventDate,
    string Detail,
    decimal? Amount,
    string? CurrencyCode,
    string StatusCode,
    string RegisteredByUserId,
    string? DecidedByUserId,
    DateTime? DecidedUtc,
    DateTime RegisteredUtc)
{
    [JsonIgnore]
    public Guid Id => RecognitionPublicId;
}

/// <summary>The recognitions bandeja page: items + paging + per-status counts. The StatusCounts cover every
/// status; the items EXCLUDE the ANULADA records by default (opt in with <c>includeAnnulled</c> or an explicit
/// <c>statusCode</c>).</summary>
public sealed record RecognitionBandejaResponse(
    IReadOnlyCollection<RecognitionListItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    IReadOnlyDictionary<string, int> StatusCounts);

/// <summary>
/// A recognition export row. The Excel/CSV/JSON writer turns the public property names into column headers
/// (reflection), so the property names ARE the Spanish headers seen by HR. Annulled records are excluded by
/// default (the caller passes <c>includeAnnulled=false</c>).
/// </summary>
public sealed record ReconocimientoExportRow(
    string Empleado,
    string? CodigoEmpleado,
    string Tipo,
    DateOnly FechaHecho,
    string Detalle,
    decimal? Monto,
    string? Moneda,
    string Estado,
    string RegistradoPor,
    string? DecididoPor,
    DateTime? FechaDecision,
    DateTime FechaRegistro);

/// <summary>
/// Filters shared by the recognitions bandeja and export. When <see cref="StatusCode"/> is supplied the items
/// are filtered to exactly that status; otherwise <see cref="IncludeAnnulled"/> (default false) decides whether
/// the ANULADA records are shown — the DEFAULT excludes them. The StatusCounts are computed over EVERY status
/// (respecting the other filters). <see cref="FromDate"/>/<see cref="ToDate"/> filter the event date (the fact).
/// </summary>
public sealed record QueryRecognitionsQuery(
    Guid CompanyId,
    Guid? EmployeeId,
    string? RecognitionTypeCode,
    string? StatusCode,
    DateOnly? FromDate,
    DateOnly? ToDate,
    bool IncludeAnnulled = false,
    int PageNumber = 1,
    int PageSize = 25) : IQuery<RecognitionBandejaResponse>;

public sealed record ExportRecognitionsQuery(
    Guid CompanyId,
    Guid? EmployeeId,
    string? RecognitionTypeCode,
    string? StatusCode,
    DateOnly? FromDate,
    DateOnly? ToDate,
    bool IncludeAnnulled,
    int? MaxRows) : IQuery<IReadOnlyCollection<ReconocimientoExportRow>>;

internal sealed class QueryRecognitionsQueryValidator : AbstractValidator<QueryRecognitionsQuery>
{
    public QueryRecognitionsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.ToDate)
            .GreaterThanOrEqualTo(query => query.FromDate!.Value)
            .When(query => query is { FromDate: not null, ToDate: not null });
    }
}

internal sealed class ExportRecognitionsQueryValidator : AbstractValidator<ExportRecognitionsQuery>
{
    public ExportRecognitionsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.ToDate)
            .GreaterThanOrEqualTo(query => query.FromDate!.Value)
            .When(query => query is { FromDate: not null, ToDate: not null });
    }
}

// ── Disciplinary-actions bandeja (§3.9) ───────────────────────────────────────────────────────────

/// <summary>A row of the company-wide disciplinary-actions bandeja (RF-012): one amonestación per employee, with
/// its deduction and suspension blocks.</summary>
public sealed record DisciplinaryActionListItemResponse(
    Guid DisciplinaryActionPublicId,
    Guid PersonnelFilePublicId,
    string EmployeeFullName,
    string? EmployeeCode,
    Guid DisciplinaryActionTypePublicId,
    string DisciplinaryActionTypeCode,
    string TypeNameSnapshot,
    Guid DisciplinaryActionCausePublicId,
    string CauseNameSnapshot,
    DateOnly IncidentDate,
    string FactsDetail,
    bool HasPayrollDeduction,
    decimal? DeductionAmount,
    string? CurrencyCode,
    string? DeductionConceptTypeCode,
    string? DeductionConceptNameSnapshot,
    DateOnly? SuspensionStartDate,
    DateOnly? SuspensionEndDate,
    int? SuspensionDays,
    string StatusCode,
    string RegisteredByUserId,
    string? DecidedByUserId,
    DateTime? DecidedUtc,
    DateTime RegisteredUtc)
{
    [JsonIgnore]
    public Guid Id => DisciplinaryActionPublicId;
}

/// <summary>The disciplinary-actions bandeja page: items + paging + per-status counts. The StatusCounts cover
/// every status; the items EXCLUDE the ANULADA records by default (opt in with <c>includeAnnulled</c> or an
/// explicit <c>statusCode</c>).</summary>
public sealed record DisciplinaryActionBandejaResponse(
    IReadOnlyCollection<DisciplinaryActionListItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    IReadOnlyDictionary<string, int> StatusCounts);

/// <summary>
/// A disciplinary-action export row (property names ARE the Spanish headers). The concept snapshot
/// (<see cref="ConceptoDescuento"/>) is populated only once the record is APLICADA (frozen at Apply). Annulled
/// records are excluded by default (the caller passes <c>includeAnnulled=false</c>).
/// </summary>
public sealed record AmonestacionExportRow(
    string Empleado,
    string? CodigoEmpleado,
    string Tipo,
    string Causa,
    DateOnly FechaFalta,
    string Detalle,
    bool TieneDescuento,
    decimal? MontoDescuento,
    string? ConceptoDescuento,
    string? Moneda,
    DateOnly? SuspensionDesde,
    DateOnly? SuspensionHasta,
    int? SuspensionDias,
    string Estado,
    string RegistradoPor,
    string? DecididoPor,
    DateTime? FechaDecision,
    DateTime FechaRegistro);

/// <summary>
/// Filters shared by the disciplinary-actions bandeja and export. Same status semantics as the recognitions
/// bandeja (default excludes ANULADA; StatusCounts over every status). <see cref="FromDate"/>/<see cref="ToDate"/>
/// filter the incident date (the fault).
/// </summary>
public sealed record QueryDisciplinaryActionsQuery(
    Guid CompanyId,
    Guid? EmployeeId,
    string? DisciplinaryActionTypeCode,
    string? CauseCode,
    string? StatusCode,
    DateOnly? FromDate,
    DateOnly? ToDate,
    bool IncludeAnnulled = false,
    int PageNumber = 1,
    int PageSize = 25) : IQuery<DisciplinaryActionBandejaResponse>;

public sealed record ExportDisciplinaryActionsQuery(
    Guid CompanyId,
    Guid? EmployeeId,
    string? DisciplinaryActionTypeCode,
    string? CauseCode,
    string? StatusCode,
    DateOnly? FromDate,
    DateOnly? ToDate,
    bool IncludeAnnulled,
    int? MaxRows) : IQuery<IReadOnlyCollection<AmonestacionExportRow>>;

internal sealed class QueryDisciplinaryActionsQueryValidator : AbstractValidator<QueryDisciplinaryActionsQuery>
{
    public QueryDisciplinaryActionsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.ToDate)
            .GreaterThanOrEqualTo(query => query.FromDate!.Value)
            .When(query => query is { FromDate: not null, ToDate: not null });
    }
}

internal sealed class ExportDisciplinaryActionsQueryValidator : AbstractValidator<ExportDisciplinaryActionsQuery>
{
    public ExportDisciplinaryActionsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.ToDate)
            .GreaterThanOrEqualTo(query => query.FromDate!.Value)
            .When(query => query is { FromDate: not null, ToDate: not null });
    }
}

// ── Payroll input (RF-012) ────────────────────────────────────────────────────────────────────────

/// <summary>
/// A payroll-input row (property names ARE the Spanish headers). One row per effect of an APLICADA disciplinary
/// action of the mandatory range: <see cref="Efecto"/> is <c>DESCUENTO</c> (with the concept/amount/currency) or
/// <c>SUSPENSION_SIN_GOCE</c> (with the suspension range + days). Revoked (ANULADA) records never travel
/// (RN-14/RN-15).
/// </summary>
public sealed record InsumoPlanillaExportRow(
    string Empleado,
    string? CodigoEmpleado,
    string Efecto,
    string Causa,
    string? ConceptoDescuento,
    decimal? Monto,
    string? Moneda,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    int? Dias);

/// <summary>
/// The payroll-input export query (RF-012): the date range is MANDATORY and filters the incident date (the
/// fault) of the applied disciplinary actions. A missing or incoherent range is rejected by the handler with 422
/// <c>PERSONNEL_TRANSACTION_RANGE_REQUIRED</c> (not a 400 model-binding error).
/// </summary>
public sealed record ExportPayrollInputQuery(
    Guid CompanyId,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int? MaxRows) : IQuery<IReadOnlyCollection<InsumoPlanillaExportRow>>;

internal sealed class ExportPayrollInputQueryValidator : AbstractValidator<ExportPayrollInputQuery>
{
    public ExportPayrollInputQueryValidator()
    {
        // The range required/invalid check is enforced in the handler as a 422 (business rule), NOT here — a
        // FluentValidation failure would surface as a 400. Only the company id is a structural precondition.
        RuleFor(query => query.CompanyId).NotEmpty();
    }
}

/// <summary>
/// Reporting-only error for the company bandejas / payroll input. The export-format error reuses
/// <c>PersonnelFileErrors.ExportFormatInvalid</c> (<c>PERSONNEL_FILE_EXPORT_FORMAT_INVALID</c>); the family view
/// gates surface the standard authorization errors.
/// </summary>
internal static class PersonnelTransactionReportingErrors
{
    public static readonly Error RangeRequired = new(
        "PERSONNEL_TRANSACTION_RANGE_REQUIRED",
        "A valid start-date and end-date range is required for the payroll input export.",
        ErrorType.UnprocessableEntity);
}
