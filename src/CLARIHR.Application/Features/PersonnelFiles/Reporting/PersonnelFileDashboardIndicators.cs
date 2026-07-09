using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles.Reporting;

// ----------------------------------------------------------------------------------------------------
// Altas (hires) — time series by month for a year (D-02). Bajas/rotación are DEFERRED (module Baja de
// Personal). Derived from HireDate; note a rehire overwrites HireDate, so a rehired employee appears as a
// hire of the rehire year (R-03).
// ----------------------------------------------------------------------------------------------------

public sealed record DashboardHiresMonthResponse(int Month, int Count);

public sealed record DashboardHiresResponse(int Year, IReadOnlyCollection<DashboardHiresMonthResponse> ByMonth, int Total);

public sealed record GetDashboardHiresQuery(
    Guid CompanyId,
    int Year,
    Guid? FunctionalAreaId,
    Guid? OrgUnitId,
    Guid? PositionCategoryId,
    Guid? JobProfileId,
    Guid? WorkCenterId,
    string? PayrollTypeCode = null,
    Guid? CostCenterId = null,
    int? Month = null)
    : IQuery<DashboardHiresResponse>
{
    public DashboardDimensionFilter ToFilter() => new(
        Year: null,
        FunctionalAreaId,
        OrgUnitId,
        PositionCategoryId,
        JobProfileId,
        WorkCenterId,
        IncludeInactive: true,
        PayrollTypeCode,
        CostCenterId,
        Month);
}

internal sealed class GetDashboardHiresQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileDashboardRepository repository)
    : IQueryHandler<GetDashboardHiresQuery, DashboardHiresResponse>
{
    public async Task<Result<DashboardHiresResponse>> Handle(GetDashboardHiresQuery query, CancellationToken cancellationToken)
    {
        var authorization = await authorizationService.EnsureCanViewReportsAsync(query.CompanyId, cancellationToken);
        if (authorization.IsFailure)
        {
            return Result<DashboardHiresResponse>.Failure(authorization.Error);
        }

        var filter = query.ToFilter();
        var dataSet = await repository.GetDashboardDataSetAsync(query.CompanyId, cancellationToken);

        var hired = dataSet.Rows
            .Where(row => PersonnelFileDashboardRules.MatchesDimensions(row, filter)
                && row.HireDate.HasValue
                && row.HireDate.Value.Year == query.Year)
            .ToArray();

        var byMonth = Enumerable.Range(1, 12)
            .Select(month => new DashboardHiresMonthResponse(month, hired.Count(row => row.HireDate!.Value.Month == month)))
            .ToArray();

        return Result<DashboardHiresResponse>.Success(new DashboardHiresResponse(query.Year, byMonth, hired.Length));
    }
}

// ----------------------------------------------------------------------------------------------------
// Colaboradores por jefe (span of control, D-05) — jefe = occupant of the slot referenced by the
// report's DirectDependencyPositionSlotId.
// ----------------------------------------------------------------------------------------------------

public sealed record DashboardManagerSpanResponse(Guid ManagerEmployeeId, string ManagerName, string? PositionTitle, int DirectReports);

public sealed record DashboardSpanOfControlResponse(
    IReadOnlyCollection<DashboardManagerSpanResponse> Managers,
    int WithoutManagerCount,
    int TotalEmployees);

public sealed record GetDashboardSpanOfControlQuery(
    Guid CompanyId,
    Guid? FunctionalAreaId,
    Guid? OrgUnitId,
    Guid? PositionCategoryId,
    Guid? JobProfileId,
    Guid? WorkCenterId,
    bool IncludeInactive,
    string? PayrollTypeCode = null,
    Guid? CostCenterId = null,
    int? Month = null)
    : IQuery<DashboardSpanOfControlResponse>
{
    public DashboardDimensionFilter ToFilter() => new(
        Year: null,
        FunctionalAreaId,
        OrgUnitId,
        PositionCategoryId,
        JobProfileId,
        WorkCenterId,
        IncludeInactive,
        PayrollTypeCode,
        CostCenterId,
        Month);
}

internal sealed class GetDashboardSpanOfControlQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileDashboardRepository repository)
    : IQueryHandler<GetDashboardSpanOfControlQuery, DashboardSpanOfControlResponse>
{
    public async Task<Result<DashboardSpanOfControlResponse>> Handle(GetDashboardSpanOfControlQuery query, CancellationToken cancellationToken)
    {
        var authorization = await authorizationService.EnsureCanViewReportsAsync(query.CompanyId, cancellationToken);
        if (authorization.IsFailure)
        {
            return Result<DashboardSpanOfControlResponse>.Failure(authorization.Error);
        }

        var filter = query.ToFilter();
        var spans = await repository.GetSpanOfControlAsync(query.CompanyId, filter, cancellationToken);
        var dataSet = await repository.GetDashboardDataSetAsync(query.CompanyId, cancellationToken);

        var referenceDate = PersonnelFileDashboardRules.ResolveReferenceDate(filter.Year, DateTime.UtcNow);
        var totalEmployees = dataSet.Rows.Count(row => PersonnelFileDashboardRules.MatchesFilter(row, filter, referenceDate));
        var reportsWithManager = spans.Sum(span => span.DirectReports);

        var managers = spans
            .Select(span => new DashboardManagerSpanResponse(span.ManagerEmployeeId, span.ManagerName, span.PositionTitle, span.DirectReports))
            .ToArray();

        return Result<DashboardSpanOfControlResponse>.Success(new DashboardSpanOfControlResponse(
            managers,
            Math.Max(0, totalEmployees - reportsWithManager),
            totalEmployees));
    }
}

// ----------------------------------------------------------------------------------------------------
// Metadata — parametrizable ranges (with bounds) + resolved company settings for the dashboard UI.
// ----------------------------------------------------------------------------------------------------

public sealed record DashboardRangeResponse(string Code, string Label, int LowerBound, int? UpperBound);

/// <summary>
/// REQ-004 PR-2 (additive): a connectable analytical section (RF-018 — mirror of <c>activeSources[]</c>). PR-2
/// declares the base registry with every section <c>active:false</c>; PR-3 activates <c>PERSONNEL_ACTIONS</c> and
/// PR-4 activates <c>MOVEMENTS</c> as each is built. <see cref="AcceptsMonth"/> tells the frontend whether the
/// section honors the <c>month</c> flow filter.
/// </summary>
public sealed record DashboardMetadataSectionResponse(string Key, bool Active, bool AcceptsMonth);

/// <summary>REQ-004 PR-2 (additive): a common dashboard filter the frontend may apply, with its enabled flag.</summary>
public sealed record DashboardMetadataFilterResponse(string Key, bool Enabled);

public sealed record DashboardMetadataResponse(
    IReadOnlyCollection<DashboardRangeResponse> AgeRanges,
    IReadOnlyCollection<DashboardRangeResponse> SeniorityRanges,
    int FileUpToDateThresholdMonths,
    string? HrFunctionalAreaCode,
    // REQ-004 PR-2 (additive) — declares the connectable sections, the common filters (incl. the 3 new ones)
    // and the rotation formula the frontend renders (populated by the movements section in PR-4).
    IReadOnlyCollection<DashboardMetadataSectionResponse> Sections,
    IReadOnlyCollection<DashboardMetadataFilterResponse> Filters,
    string RotationFormula);

public sealed record GetDashboardMetadataQuery(Guid CompanyId) : IQuery<DashboardMetadataResponse>;

internal sealed class GetDashboardMetadataQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileDashboardRepository repository)
    : IQueryHandler<GetDashboardMetadataQuery, DashboardMetadataResponse>
{
    public async Task<Result<DashboardMetadataResponse>> Handle(GetDashboardMetadataQuery query, CancellationToken cancellationToken)
    {
        var authorization = await authorizationService.EnsureCanViewReportsAsync(query.CompanyId, cancellationToken);
        if (authorization.IsFailure)
        {
            return Result<DashboardMetadataResponse>.Failure(authorization.Error);
        }

        var metadata = await repository.GetDashboardMetadataAsync(query.CompanyId, cancellationToken);

        return Result<DashboardMetadataResponse>.Success(new DashboardMetadataResponse(
            metadata.AgeRanges.Select(Map).ToArray(),
            metadata.SeniorityRanges.Select(Map).ToArray(),
            metadata.FileUpToDateThresholdMonths ?? PersonnelFileDashboardRules.DefaultFileUpToDateThresholdMonths,
            metadata.HrFunctionalAreaCode,
            BaseSections,
            Filters,
            PersonnelFileDashboardRules.RotationFormula));
    }

    // Connectable-section registry (RF-018). PR-3 activated PERSONNEL_ACTIONS; PR-4 activates MOVEMENTS; the rest
    // stay inactive until their module connects. All sections are flow-based → AcceptsMonth = true.
    private static readonly IReadOnlyCollection<DashboardMetadataSectionResponse> BaseSections =
    [
        new("PERSONNEL_ACTIONS", Active: true, AcceptsMonth: true),    // PR-3: documentary actions section is live
        new("MOVEMENTS", Active: true, AcceptsMonth: true),            // PR-4: movements section is live
        new("INCAPACIDADES", Active: false, AcceptsMonth: true),       // REQ-001 connects
        new("VACACIONES", Active: false, AcceptsMonth: true),          // REQ-001 connects
        new("RECONOCIMIENTOS", Active: false, AcceptsMonth: true),     // REQ-003 connects
        new("AMONESTACIONES", Active: false, AcceptsMonth: true),      // REQ-003 connects
        new("TIEMPO_COMPENSATORIO", Active: false, AcceptsMonth: true) // REQ-002 connects
    ];

    // Common dimension filters (keys = the WIRE query-parameter names the frontend sends — Guid `xxxId` params are
    // rewritten to `xxxPublicId` by PublicContractBindingMetadataProvider). The three new REQ-004 filters are
    // payrollTypeCode, costCenterPublicId and month; month is enabled but only the flow sections consume it.
    private static readonly IReadOnlyCollection<DashboardMetadataFilterResponse> Filters =
    [
        new("year", Enabled: true),
        new("functionalAreaPublicId", Enabled: true),
        new("orgUnitPublicId", Enabled: true),
        new("positionCategoryPublicId", Enabled: true),
        new("jobProfilePublicId", Enabled: true),
        new("workCenterPublicId", Enabled: true),
        new("includeInactive", Enabled: true),
        new("payrollTypeCode", Enabled: true),
        new("costCenterPublicId", Enabled: true),
        new("month", Enabled: true)
    ];

    private static DashboardRangeResponse Map(RangeBucket range) =>
        new(range.Code, range.Label, range.LowerBound, range.UpperBound);
}
