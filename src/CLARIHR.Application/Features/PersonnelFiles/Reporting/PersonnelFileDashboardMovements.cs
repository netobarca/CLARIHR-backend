using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles.Reporting;

// ----------------------------------------------------------------------------------------------------
// Movements section (RF-010…RF-014) — activates the bajas/rotación deferred by the HR dashboard (PR #52).
// The canonical source is the employee PROFILE, NEVER the journal (aclaración №4 / D-03): separations by
// RetirementDate (+ category/reason), hires by HireDate (same criterion as dashboard/hires, recomputed
// here so that endpoint is untouched), altas−bajas net, annual turnover, exit-interview coverage and
// settlement counts by status. SIN MONTOS (aclaración №8): settlement counts carry no amount/currency.
// ----------------------------------------------------------------------------------------------------

public sealed record DashboardMovementsSeriesMonthResponse(int Month, int Count);

public sealed record DashboardMovementsSeriesResponse(
    IReadOnlyCollection<DashboardMovementsSeriesMonthResponse> ByMonth,
    int Total);

public sealed record DashboardSeparationsResponse(
    DashboardMovementsSeriesResponse Series,
    // byCategory = RetirementCategoryCode, byReason = RetirementReasonCode (labels from the retirement catalogs).
    IReadOnlyCollection<DashboardBreakdownResponse> ByCategory,
    IReadOnlyCollection<DashboardBreakdownResponse> ByReason);

/// <summary>Annual turnover — <c>ratePercent</c> is null when the average headcount is 0 ("N/D").</summary>
public sealed record DashboardRotationResponse(int Separations, decimal AverageHeadcount, decimal? RatePercent);

/// <summary>Exit-interview coverage — <c>coveragePercent</c> is null when there are 0 separations in the period.</summary>
public sealed record DashboardExitInterviewCoverageResponse(int Separations, int Completed, decimal? CoveragePercent);

public sealed record DashboardMovementsResponse(
    int Year,
    int? Month,
    DashboardMovementsSeriesResponse Hires,
    DashboardSeparationsResponse Separations,
    DashboardMovementsSeriesResponse Net,
    DashboardRotationResponse Rotation,
    DashboardExitInterviewCoverageResponse ExitInterviewCoverage,
    // Real settlements (scenarios excluded) grouped by lifecycle status in the period — COUNTS only, no amounts.
    IReadOnlyCollection<DashboardBreakdownResponse> SettlementsByStatus);

public sealed record GetDashboardMovementsQuery(
    Guid CompanyId,
    int? Year,
    int? Month,
    Guid? FunctionalAreaId,
    Guid? OrgUnitId,
    Guid? PositionCategoryId,
    Guid? JobProfileId,
    Guid? WorkCenterId,
    string? PayrollTypeCode,
    Guid? CostCenterId)
    : IQuery<DashboardMovementsResponse>
{
    // Movements are scoped by event date (HireDate/RetirementDate) within the period + the current dimensional
    // row, never by active-at-year-end; hence Year:null and IncludeInactive:true (a retired employee still counts).
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

internal sealed class GetDashboardMovementsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileDashboardRepository repository)
    : IQueryHandler<GetDashboardMovementsQuery, DashboardMovementsResponse>
{
    public async Task<Result<DashboardMovementsResponse>> Handle(
        GetDashboardMovementsQuery query,
        CancellationToken cancellationToken)
    {
        var authorization = await authorizationService.EnsureCanViewReportsAsync(query.CompanyId, cancellationToken);
        if (authorization.IsFailure)
        {
            return Result<DashboardMovementsResponse>.Failure(authorization.Error);
        }

        // month requires an explicit year (reuses the personnel-actions error); otherwise default to the current year.
        if (query.Month.HasValue && !query.Year.HasValue)
        {
            return Result<DashboardMovementsResponse>.Failure(DashboardPersonnelActionsErrors.MonthRequiresYear);
        }

        var year = query.Year ?? DateTime.UtcNow.Year;
        var month = query.Month;
        var filter = query.ToFilter();

        var dataSet = await repository.GetDashboardDataSetAsync(query.CompanyId, cancellationToken);
        var labels = await repository.GetMovementsCatalogLabelsAsync(cancellationToken);

        // The movements population = the dimensional rows honoring the common filters (dimension-only, event-date
        // scoped — NOT active-at-year-end); retired employees stay in for the separation/headcount computations.
        var rows = dataSet.Rows
            .Where(row => PersonnelFileDashboardRules.MatchesDimensions(row, filter))
            .ToArray();

        var hires = MovementsDashboardRules.BuildHires(rows, year, month);
        var separations = MovementsDashboardRules.BuildSeparations(rows, year, month, labels.CategoryLabels, labels.ReasonLabels);
        var net = MovementsDashboardRules.ComputeNet(hires, separations.Series);

        // Headcount inicio/fin (R-02 approximation, IsActiveAtYearEnd helper): active the day BEFORE the period vs
        // active on its last day — so a hire during the period is not counted in the starting headcount.
        var periodStart = new DateTime(year, month ?? 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var startReference = periodStart.AddDays(-1);
        var endReference = (month.HasValue ? periodStart.AddMonths(1) : periodStart.AddYears(1)).AddDays(-1);
        var headcountStart = rows.Count(row => PersonnelFileDashboardRules.IsActiveAtYearEnd(row.HireDate, row.RetirementDate, startReference));
        var headcountEnd = rows.Count(row => PersonnelFileDashboardRules.IsActiveAtYearEnd(row.HireDate, row.RetirementDate, endReference));
        var rotation = MovementsDashboardRules.ComputeRotation(separations.Series.Total, headcountStart, headcountEnd);

        // Coverage: the period's separation files vs those with a completed (Submitted) exit-interview submission.
        var separationFileIds = rows
            .Where(row => MovementsDashboardRules.FallsInPeriod(row.RetirementDate, year, month))
            .Select(row => row.FileId)
            .ToArray();
        var completedFileIds = await repository.GetCompletedExitInterviewFilePublicIdsAsync(
            query.CompanyId, separationFileIds, cancellationToken);
        var coverage = MovementsDashboardRules.ComputeExitInterviewCoverage(separationFileIds, completedFileIds.ToHashSet());

        // Settlements (real only — scenarios carry a null status) grouped by StatusCode in the period. SIN MONTOS.
        var settlementCounts = await repository.GetSettlementStatusCountsAsync(query.CompanyId, year, month, cancellationToken);
        var settlementsByStatus = settlementCounts
            .Select(entry => new DashboardBreakdownResponse(
                entry.Key,
                labels.SettlementStatusLabels.TryGetValue(entry.Key, out var label) ? label : entry.Key,
                entry.Value))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var response = new DashboardMovementsResponse(
            year,
            month,
            MapSeries(hires),
            new DashboardSeparationsResponse(MapSeries(separations.Series), separations.ByCategory, separations.ByReason),
            MapSeries(net),
            new DashboardRotationResponse(rotation.Separations, rotation.AverageHeadcount, rotation.RatePercent),
            new DashboardExitInterviewCoverageResponse(coverage.Separations, coverage.Completed, coverage.CoveragePercent),
            settlementsByStatus);

        return Result<DashboardMovementsResponse>.Success(response);
    }

    private static DashboardMovementsSeriesResponse MapSeries(MovementsSeries series) =>
        new(
            series.ByMonth.Select(bucket => new DashboardMovementsSeriesMonthResponse(bucket.Month, bucket.Count)).ToArray(),
            series.Total);
}
