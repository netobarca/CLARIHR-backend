using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles.Reporting;

// ----------------------------------------------------------------------------------------------------
// Documentary personnel-actions section (RF-005…RF-009) — the first tenant-wide read of the actions
// journal (PersonnelFilePersonnelAction). Monthly series + breakdowns by type / status / origin /
// organizational dimension. SIN MONTOS (aclaración №8): the journal carries the settlement net amount but
// neither `amount` nor `currency` is ever projected or exposed here.
// ----------------------------------------------------------------------------------------------------

public sealed record DashboardActionsSeriesMonthResponse(int Month, int Count);

public sealed record DashboardActionsSeriesResponse(
    IReadOnlyCollection<DashboardActionsSeriesMonthResponse> ByMonth,
    int Total);

/// <summary>Actions grouped by each organizational dimension of the employee's active-primary assignment (D-07 approximation).</summary>
public sealed record DashboardPersonnelActionsByDimensionResponse(
    IReadOnlyCollection<DashboardBreakdownResponse> OrgUnits,
    IReadOnlyCollection<DashboardBreakdownResponse> FunctionalAreas,
    IReadOnlyCollection<DashboardBreakdownResponse> WorkCenters,
    IReadOnlyCollection<DashboardBreakdownResponse> JobProfiles,
    IReadOnlyCollection<DashboardBreakdownResponse> PositionCategories,
    IReadOnlyCollection<DashboardBreakdownResponse> PayrollTypes);

public sealed record DashboardPersonnelActionsResponse(
    int Year,
    int? Month,
    bool IncludeAllStatuses,
    DashboardActionsSeriesResponse Series,
    IReadOnlyCollection<DashboardBreakdownResponse> ByType,
    // byStatus is computed over the FULL status universe (RN-04), independent of the APLICADA default.
    IReadOnlyCollection<DashboardBreakdownResponse> ByStatus,
    IReadOnlyCollection<DashboardBreakdownResponse> ByOrigin,
    DashboardPersonnelActionsByDimensionResponse ByDimension);

internal static class DashboardPersonnelActionsErrors
{
    // D-05/RN-04: month is a flow filter over a specific year; supplying it without a year is a 400 (A.3-7).
    public static readonly Error MonthRequiresYear = new(
        "DASHBOARD_MONTH_REQUIRES_YEAR",
        "The month filter requires an explicit year.",
        ErrorType.Validation);
}

public sealed record GetDashboardPersonnelActionsQuery(
    Guid CompanyId,
    int? Year,
    int? Month,
    bool IncludeAllStatuses,
    Guid? FunctionalAreaId,
    Guid? OrgUnitId,
    Guid? PositionCategoryId,
    Guid? JobProfileId,
    Guid? WorkCenterId,
    string? PayrollTypeCode,
    Guid? CostCenterId)
    : IQuery<DashboardPersonnelActionsResponse>
{
    // Actions are scoped by ActionDateUtc range (in the repository) + the current dimensional row, never by
    // active-at-year-end; hence Year:null and IncludeInactive:true (a retired employee's BAJA still counts).
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

internal sealed class GetDashboardPersonnelActionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileDashboardRepository repository)
    : IQueryHandler<GetDashboardPersonnelActionsQuery, DashboardPersonnelActionsResponse>
{
    public async Task<Result<DashboardPersonnelActionsResponse>> Handle(
        GetDashboardPersonnelActionsQuery query,
        CancellationToken cancellationToken)
    {
        var authorization = await authorizationService.EnsureCanViewReportsAsync(query.CompanyId, cancellationToken);
        if (authorization.IsFailure)
        {
            return Result<DashboardPersonnelActionsResponse>.Failure(authorization.Error);
        }

        // month requires an explicit year (A.3-7); otherwise default to the current year (as dashboard/hires does).
        if (query.Month.HasValue && !query.Year.HasValue)
        {
            return Result<DashboardPersonnelActionsResponse>.Failure(DashboardPersonnelActionsErrors.MonthRequiresYear);
        }

        var year = query.Year ?? DateTime.UtcNow.Year;
        var filter = query.ToFilter();

        // The repository returns the FULL status universe in the window (byStatus needs it); the APLICADA items
        // split is applied in the rules below. GetDashboardDataSetAsync supplies the dimensional row bundle
        // (join by PublicId) and the payroll-type labels; the action type/status labels come from their catalogs.
        var facts = await repository.GetPersonnelActionFactsAsync(
            query.CompanyId, year, query.Month, query.IncludeAllStatuses, cancellationToken);
        var dataSet = await repository.GetDashboardDataSetAsync(query.CompanyId, cancellationToken);
        var catalogLabels = await repository.GetPersonnelActionCatalogLabelsAsync(cancellationToken);

        var rowsByFileId = dataSet.Rows.ToDictionary(row => row.FileId);

        // Scope the journal to the dimension filters via the employee's CURRENT dimensional row (D-07). An action
        // whose file has no row survives only when no dimension constraint is active (then it lands in "Sin asignar").
        var hasDimensionConstraint = filter.FunctionalAreaId.HasValue
            || filter.OrgUnitId.HasValue
            || filter.PositionCategoryId.HasValue
            || filter.JobProfileId.HasValue
            || filter.WorkCenterId.HasValue
            || !string.IsNullOrWhiteSpace(filter.PayrollTypeCode)
            || filter.CostCenterId.HasValue;

        var scoped = facts
            .Where(fact => rowsByFileId.TryGetValue(fact.PersonnelFilePublicId, out var row)
                ? PersonnelFileDashboardRules.MatchesDimensions(row, filter)
                : !hasDimensionConstraint)
            .ToArray();

        var items = PersonnelActionsDashboardRules.SelectItems(scoped, query.IncludeAllStatuses);

        var series = PersonnelActionsDashboardRules.BuildActionsSeries(items, year, query.Month);
        var byType = PersonnelActionsDashboardRules.BuildBreakdown(items, row => row.ActionTypeCode, catalogLabels.TypeLabels);
        // byStatus over the full status universe (scoped by dimensions, NOT by the APLICADA default) — RN-04.
        var byStatus = PersonnelActionsDashboardRules.BuildBreakdown(scoped, row => row.ActionStatusCode, catalogLabels.StatusLabels);
        var byOrigin = PersonnelActionsDashboardRules.BuildOriginBreakdown(items);

        var byDimension = new DashboardPersonnelActionsByDimensionResponse(
            PersonnelActionsDashboardRules.BuildDimensionBreakdown(items, rowsByFileId, row => row.OrgUnitId?.ToString(), row => row.OrgUnitName),
            PersonnelActionsDashboardRules.BuildDimensionBreakdown(items, rowsByFileId, row => row.FunctionalAreaId?.ToString(), row => row.FunctionalAreaName),
            PersonnelActionsDashboardRules.BuildDimensionBreakdown(items, rowsByFileId, row => row.WorkCenterId?.ToString(), row => row.WorkCenterName),
            PersonnelActionsDashboardRules.BuildDimensionBreakdown(items, rowsByFileId, row => row.JobProfileId?.ToString(), row => row.JobProfileTitle),
            PersonnelActionsDashboardRules.BuildDimensionBreakdown(items, rowsByFileId, row => row.PositionCategoryId?.ToString(), row => row.PositionCategoryName),
            PersonnelActionsDashboardRules.BuildDimensionBreakdown(
                items,
                rowsByFileId,
                row => row.PayrollTypeCode,
                row => row.PayrollTypeCode is string code && dataSet.PayrollTypeLabels.TryGetValue(code, out var label)
                    ? label
                    : row.PayrollTypeCode));

        var response = new DashboardPersonnelActionsResponse(
            year,
            query.Month,
            query.IncludeAllStatuses,
            new DashboardActionsSeriesResponse(
                series.ByMonth.Select(bucket => new DashboardActionsSeriesMonthResponse(bucket.Month, bucket.Count)).ToArray(),
                series.Total),
            byType,
            byStatus,
            byOrigin,
            byDimension);

        return Result<DashboardPersonnelActionsResponse>.Success(response);
    }
}
