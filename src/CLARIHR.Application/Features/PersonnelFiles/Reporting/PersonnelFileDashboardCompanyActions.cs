using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles.Reporting;

// ----------------------------------------------------------------------------------------------------
// Company-wide personnel-actions bandeja (RF-017) — the drill of the documentary-actions journal to the
// row level, at the company (tenant) scope (the journal was previously queryable only per expediente).
// Paginated + StatusCounts + the same filters and dimensional scoping as the dashboard section. SIN MONTOS
// (aclaración №8): the journal carries the settlement net amount, but NEITHER `amount` NOR `currency` is
// projected in the bandeja row or its export — only the documentary facts (empleado, tipo, estado, origen,
// fecha del asiento, vigencias, descripción/referencia). Reporting family: the POST /query carries NO
// [AuthorizationPolicySet] (it would assign the Manage policy to a READ); authorization is per handler via
// EnsureCanViewReportsAsync (aclaración №11).
// ----------------------------------------------------------------------------------------------------

/// <summary>
/// The dimensional + documentary filter of the company-wide personnel-actions bandeja (RF-017). The window is
/// resolved in the repository: a from/to range if supplied, otherwise the year (+ month), otherwise the current
/// year. Dimensional fields (área/unidad/tipo-puesto/puesto/centro + tipo-planilla/centro-costo) scope by the
/// employee's CURRENT active-primary assignment (D-07 approximation), identical to the dashboard section.
/// </summary>
public sealed record PersonnelActionBandejaFilter(
    string? ActionTypeCode,
    string? ActionStatusCode,
    bool? IsSystemGenerated,
    int? Year,
    int? Month,
    DateTime? FromUtc,
    DateTime? ToUtc,
    Guid? EmployeeId,
    Guid? FunctionalAreaId,
    Guid? OrgUnitId,
    Guid? PositionCategoryId,
    Guid? JobProfileId,
    Guid? WorkCenterId,
    string? PayrollTypeCode,
    Guid? CostCenterId)
{
    public bool HasDimensionConstraint =>
        FunctionalAreaId.HasValue
        || OrgUnitId.HasValue
        || PositionCategoryId.HasValue
        || JobProfileId.HasValue
        || WorkCenterId.HasValue
        || !string.IsNullOrWhiteSpace(PayrollTypeCode)
        || CostCenterId.HasValue;

    /// <summary>Projects the dimensional part onto the shared <see cref="DashboardDimensionFilter"/> (Month/Year unused here — the window is resolved separately).</summary>
    public DashboardDimensionFilter ToDimensionFilter() => new(
        Year: null,
        FunctionalAreaId,
        OrgUnitId,
        PositionCategoryId,
        JobProfileId,
        WorkCenterId,
        IncludeInactive: true,
        PayrollTypeCode,
        CostCenterId,
        Month: null);
}

/// <summary>
/// One row of the company-wide personnel-actions bandeja (RF-017). Documentary facts only — SIN MONTOS
/// (aclaración №8): there is NO `amount`/`currency`. <see cref="OriginCode"/> is MANUAL / SYSTEM
/// (= <see cref="IsSystemGenerated"/>); the user who generated the action is NOT exposed by the journal.
/// </summary>
public sealed record PersonnelActionBandejaItemResponse(
    Guid PersonnelActionPublicId,
    Guid PersonnelFilePublicId,
    string EmployeeFullName,
    string? EmployeeCode,
    string ActionTypeCode,
    string? ActionTypeName,
    string ActionStatusCode,
    string? ActionStatusName,
    string OriginCode,
    bool IsSystemGenerated,
    DateTime ActionDateUtc,
    DateTime? EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? Description,
    string? Reference);

/// <summary>The bandeja page: items + paging + per-status counts (counts span every status regardless of the status filter).</summary>
public sealed record PersonnelActionBandejaResponse(
    IReadOnlyCollection<PersonnelActionBandejaItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    IReadOnlyDictionary<string, int> StatusCounts);

/// <summary>The repository output of the bandeja query: page items + total + per-status counts.</summary>
public sealed record PersonnelActionBandejaResult(
    IReadOnlyCollection<PersonnelActionBandejaItemResponse> Items,
    int TotalCount,
    IReadOnlyDictionary<string, int> StatusCounts);

/// <summary>
/// An export row of the company-wide personnel-actions bandeja. The Excel/CSV writer turns the public property
/// names into the (Spanish) column headers seen by HR (reflection). SIN MONTOS (aclaración №8): there is no
/// monetary column — the settlement net amount the journal carries is never exported.
/// </summary>
public sealed record AsientoPersonalExportRow(
    string Empleado,
    string? CodigoEmpleado,
    string Tipo,
    string Estado,
    string Origen,
    DateTime FechaAsiento,
    DateTime? VigenciaDesde,
    DateTime? VigenciaHasta,
    string? Descripcion,
    string? Referencia);

public sealed record QueryCompanyPersonnelActionsQuery(
    Guid CompanyId,
    string? ActionTypeCode,
    string? ActionStatusCode,
    bool? IsSystemGenerated,
    int? Year,
    int? Month,
    DateTime? FromUtc,
    DateTime? ToUtc,
    Guid? EmployeeId,
    Guid? FunctionalAreaId,
    Guid? OrgUnitId,
    Guid? PositionCategoryId,
    Guid? JobProfileId,
    Guid? WorkCenterId,
    string? PayrollTypeCode,
    Guid? CostCenterId,
    int PageNumber = 1,
    int PageSize = 25) : IQuery<PersonnelActionBandejaResponse>
{
    public PersonnelActionBandejaFilter ToFilter() => new(
        ActionTypeCode,
        ActionStatusCode,
        IsSystemGenerated,
        Year,
        Month,
        FromUtc,
        ToUtc,
        EmployeeId,
        FunctionalAreaId,
        OrgUnitId,
        PositionCategoryId,
        JobProfileId,
        WorkCenterId,
        PayrollTypeCode,
        CostCenterId);
}

public sealed record ExportCompanyPersonnelActionsQuery(
    Guid CompanyId,
    string? ActionTypeCode,
    string? ActionStatusCode,
    bool? IsSystemGenerated,
    int? Year,
    int? Month,
    DateTime? FromUtc,
    DateTime? ToUtc,
    Guid? EmployeeId,
    Guid? FunctionalAreaId,
    Guid? OrgUnitId,
    Guid? PositionCategoryId,
    Guid? JobProfileId,
    Guid? WorkCenterId,
    string? PayrollTypeCode,
    Guid? CostCenterId,
    int? MaxRows) : IQuery<IReadOnlyCollection<AsientoPersonalExportRow>>
{
    public PersonnelActionBandejaFilter ToFilter() => new(
        ActionTypeCode,
        ActionStatusCode,
        IsSystemGenerated,
        Year,
        Month,
        FromUtc,
        ToUtc,
        EmployeeId,
        FunctionalAreaId,
        OrgUnitId,
        PositionCategoryId,
        JobProfileId,
        WorkCenterId,
        PayrollTypeCode,
        CostCenterId);
}

internal sealed class QueryCompanyPersonnelActionsQueryValidator : AbstractValidator<QueryCompanyPersonnelActionsQuery>
{
    public QueryCompanyPersonnelActionsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Month).InclusiveBetween(1, 12).When(query => query.Month.HasValue);
        RuleFor(query => query.ToUtc)
            .GreaterThanOrEqualTo(query => query.FromUtc!.Value)
            .When(query => query.FromUtc.HasValue && query.ToUtc.HasValue);
    }
}

internal sealed class ExportCompanyPersonnelActionsQueryValidator : AbstractValidator<ExportCompanyPersonnelActionsQuery>
{
    public ExportCompanyPersonnelActionsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Month).InclusiveBetween(1, 12).When(query => query.Month.HasValue);
        RuleFor(query => query.ToUtc)
            .GreaterThanOrEqualTo(query => query.FromUtc!.Value)
            .When(query => query.FromUtc.HasValue && query.ToUtc.HasValue);
    }
}

internal sealed class QueryCompanyPersonnelActionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileDashboardRepository repository)
    : IQueryHandler<QueryCompanyPersonnelActionsQuery, PersonnelActionBandejaResponse>
{
    public async Task<Result<PersonnelActionBandejaResponse>> Handle(
        QueryCompanyPersonnelActionsQuery query,
        CancellationToken cancellationToken)
    {
        var authorization = await authorizationService.EnsureCanViewReportsAsync(query.CompanyId, cancellationToken);
        if (authorization.IsFailure)
        {
            return Result<PersonnelActionBandejaResponse>.Failure(authorization.Error);
        }

        // month is a flow filter over a specific year; supplying it without a year is a 400 (reuses the section error).
        if (query.Month.HasValue && !query.Year.HasValue && !query.FromUtc.HasValue && !query.ToUtc.HasValue)
        {
            return Result<PersonnelActionBandejaResponse>.Failure(DashboardPersonnelActionsErrors.MonthRequiresYear);
        }

        var result = await repository.QueryPersonnelActionsAsync(
            query.CompanyId, query.ToFilter(), query.PageNumber, query.PageSize, cancellationToken);

        return Result<PersonnelActionBandejaResponse>.Success(new PersonnelActionBandejaResponse(
            result.Items,
            query.PageNumber,
            query.PageSize,
            result.TotalCount,
            result.StatusCounts));
    }
}

internal sealed class ExportCompanyPersonnelActionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileDashboardRepository repository)
    : IQueryHandler<ExportCompanyPersonnelActionsQuery, IReadOnlyCollection<AsientoPersonalExportRow>>
{
    public async Task<Result<IReadOnlyCollection<AsientoPersonalExportRow>>> Handle(
        ExportCompanyPersonnelActionsQuery query,
        CancellationToken cancellationToken)
    {
        var authorization = await authorizationService.EnsureCanViewReportsAsync(query.CompanyId, cancellationToken);
        if (authorization.IsFailure)
        {
            return Result<IReadOnlyCollection<AsientoPersonalExportRow>>.Failure(authorization.Error);
        }

        if (query.Month.HasValue && !query.Year.HasValue && !query.FromUtc.HasValue && !query.ToUtc.HasValue)
        {
            return Result<IReadOnlyCollection<AsientoPersonalExportRow>>.Failure(DashboardPersonnelActionsErrors.MonthRequiresYear);
        }

        var rows = await repository.GetPersonnelActionExportRowsAsync(
            query.CompanyId, query.ToFilter(), query.MaxRows, cancellationToken);

        return Result<IReadOnlyCollection<AsientoPersonalExportRow>>.Success(rows);
    }
}
