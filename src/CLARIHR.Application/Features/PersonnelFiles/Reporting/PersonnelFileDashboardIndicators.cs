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
    Guid? WorkCenterId)
    : IQuery<DashboardHiresResponse>
{
    public DashboardDimensionFilter ToFilter() => new(
        Year: null,
        FunctionalAreaId,
        OrgUnitId,
        PositionCategoryId,
        JobProfileId,
        WorkCenterId,
        IncludeInactive: true);
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
    bool IncludeInactive)
    : IQuery<DashboardSpanOfControlResponse>
{
    public DashboardDimensionFilter ToFilter() => new(
        Year: null,
        FunctionalAreaId,
        OrgUnitId,
        PositionCategoryId,
        JobProfileId,
        WorkCenterId,
        IncludeInactive);
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

public sealed record DashboardMetadataResponse(
    IReadOnlyCollection<DashboardRangeResponse> AgeRanges,
    IReadOnlyCollection<DashboardRangeResponse> SeniorityRanges,
    int FileUpToDateThresholdMonths,
    string? HrFunctionalAreaCode);

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
            metadata.HrFunctionalAreaCode));
    }

    private static DashboardRangeResponse Map(RangeBucket range) =>
        new(range.Code, range.Label, range.LowerBound, range.UpperBound);
}
