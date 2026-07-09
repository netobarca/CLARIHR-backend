using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles.Reporting;

// ----------------------------------------------------------------------------------------------------
// Shared dimensional types (repository ↔ rules ↔ handler). The repository resolves one EmployeeDimensionRow
// per Employee personnel file by joining the active-primary assignment to org/position/work-center; the
// rules bucketize/aggregate; the handler shapes the response. See the technical plan §3.1.
// ----------------------------------------------------------------------------------------------------

/// <summary>
/// Common dimension filter applied to every dashboard indicator (año/área/unidad/tipo-puesto/puesto/centro).
/// REQ-004 PR-2 adds three OPTIONAL fields: <see cref="PayrollTypeCode"/> and <see cref="CostCenterId"/> scope
/// EVERY endpoint (they live in the common filter — aclaración №6), while <see cref="Month"/> is stored here but
/// consumed ONLY by the flow queries (personnel-actions/movements — PR-3/PR-4); the snapshot indicators
/// (overview/span) ignore it and the metadata merely DECLARES it.
/// </summary>
public sealed record DashboardDimensionFilter(
    int? Year,
    Guid? FunctionalAreaId,
    Guid? OrgUnitId,
    Guid? PositionCategoryId,
    Guid? JobProfileId,
    Guid? WorkCenterId,
    bool IncludeInactive,
    string? PayrollTypeCode = null,
    Guid? CostCenterId = null,
    int? Month = null);

/// <summary>A parametrizable range bucket (edad/antigüedad) resolved from a country-scoped catalog; null upper = open.</summary>
public sealed record RangeBucket(string Code, string Label, int LowerBound, int? UpperBound);

/// <summary>
/// One Employee personnel file flattened with the dimensions of its active-primary assignment. REQ-004 PR-2
/// appends the plaza's <see cref="PayrollTypeCode"/> and <see cref="CostCenterPublicId"/>/<see cref="CostCenterName"/>
/// (cost-center name resolved in memory like the other dimensions) and the profile's retirement
/// <see cref="RetirementCategoryCode"/>/<see cref="RetirementReasonCode"/> (the seed for the movements breakdowns).
/// </summary>
public sealed record EmployeeDimensionRow(
    Guid FileId,
    bool IsActive,
    string LifecycleStatus,
    DateTime? ModifiedAtUtc,
    DateTime BirthDate,
    DateTime? HireDate,
    DateTime? RetirementDate,
    string? MaritalStatus,
    string RecordType,
    string? EmploymentStatusCode,
    string? ContractTypeCode,
    Guid? OrgUnitId,
    string? OrgUnitName,
    Guid? FunctionalAreaId,
    string? FunctionalAreaCode,
    string? FunctionalAreaName,
    Guid? WorkCenterId,
    string? WorkCenterName,
    Guid? JobProfileId,
    string? JobProfileTitle,
    Guid? PositionCategoryId,
    string? PositionCategoryName,
    string? PayrollTypeCode = null,
    Guid? CostCenterPublicId = null,
    string? CostCenterName = null,
    string? RetirementCategoryCode = null,
    string? RetirementReasonCode = null);

/// <summary>
/// The one-round-trip dataset the dashboard handler aggregates over. <see cref="PayrollTypeLabels"/> (code →
/// display name from the <c>payroll-types</c> catalog) backs the <c>byPayrollType</c> breakdown labels — labels
/// always come from the catalog, never hardcoded (aclaración №12).
/// </summary>
public sealed record DashboardDataSet(
    IReadOnlyCollection<EmployeeDimensionRow> Rows,
    IReadOnlyCollection<RangeBucket> AgeRanges,
    IReadOnlyCollection<RangeBucket> SeniorityRanges,
    string? HrFunctionalAreaCode,
    int? FileUpToDateThresholdMonths,
    IReadOnlyDictionary<string, string> PayrollTypeLabels);

/// <summary>Lightweight dashboard metadata: the parametrizable range catalogs + resolved company settings.</summary>
public sealed record DashboardMetadata(
    IReadOnlyCollection<RangeBucket> AgeRanges,
    IReadOnlyCollection<RangeBucket> SeniorityRanges,
    string? HrFunctionalAreaCode,
    int? FileUpToDateThresholdMonths);

/// <summary>Aggregate position occupancy (plazas) — repository output for the occupancy indicator (D-13).</summary>
public sealed record DashboardPositionOccupancy(int MaxPositions, int Occupied, int Vacant);

/// <summary>One manager and the count of their direct reports (span of control, D-05).</summary>
public sealed record DashboardManagerSpan(Guid ManagerEmployeeId, string ManagerName, string? PositionTitle, int DirectReports);

// ----------------------------------------------------------------------------------------------------
// Response DTOs
// ----------------------------------------------------------------------------------------------------

public sealed record DashboardBreakdownResponse(string Key, string Label, int Count);

public sealed record DashboardHeadcountResponse(int Total, int Active, int Inactive);

public sealed record DashboardFileFreshnessResponse(int UpToDate, int Outdated, int ThresholdMonths);

public sealed record DashboardHrRatioResponse(int HrHeadcount, int TotalHeadcount, decimal? RatioPer100, bool Configured);

public sealed record DashboardPositionOccupancyResponse(int MaxPositions, int Occupied, int Vacant);

public sealed record DashboardOverviewResponse(
    DashboardHeadcountResponse Headcount,
    IReadOnlyCollection<DashboardBreakdownResponse> ByRecordType,
    IReadOnlyCollection<DashboardBreakdownResponse> ByEmploymentStatus,
    IReadOnlyCollection<DashboardBreakdownResponse> ByContractType,
    IReadOnlyCollection<DashboardBreakdownResponse> ByPositionCategory,
    IReadOnlyCollection<DashboardBreakdownResponse> ByFunctionalArea,
    IReadOnlyCollection<DashboardBreakdownResponse> ByOrgUnit,
    IReadOnlyCollection<DashboardBreakdownResponse> ByWorkCenter,
    IReadOnlyCollection<DashboardBreakdownResponse> ByAgeRange,
    IReadOnlyCollection<DashboardBreakdownResponse> BySeniorityRange,
    IReadOnlyCollection<DashboardBreakdownResponse> ByMaritalStatus,
    DashboardFileFreshnessResponse FileFreshness,
    DashboardHrRatioResponse HrRatio,
    DashboardPositionOccupancyResponse PositionOccupancy,
    // REQ-004 PR-2 (additive): distribution by contractual payroll type; bucket UNASSIGNED = "Sin dato".
    IReadOnlyCollection<DashboardBreakdownResponse> ByPayrollType);

// ----------------------------------------------------------------------------------------------------
// Query + handler
// ----------------------------------------------------------------------------------------------------

public sealed record GetDashboardOverviewQuery(
    Guid CompanyId,
    int? Year,
    Guid? FunctionalAreaId,
    Guid? OrgUnitId,
    Guid? PositionCategoryId,
    Guid? JobProfileId,
    Guid? WorkCenterId,
    bool IncludeInactive,
    string? PayrollTypeCode = null,
    Guid? CostCenterId = null,
    int? Month = null)
    : IQuery<DashboardOverviewResponse>
{
    public DashboardDimensionFilter ToFilter() => new(
        Year,
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

internal sealed class GetDashboardOverviewQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileDashboardRepository repository)
    : IQueryHandler<GetDashboardOverviewQuery, DashboardOverviewResponse>
{
    public async Task<Result<DashboardOverviewResponse>> Handle(
        GetDashboardOverviewQuery query,
        CancellationToken cancellationToken)
    {
        var authorization = await authorizationService.EnsureCanViewReportsAsync(query.CompanyId, cancellationToken);
        if (authorization.IsFailure)
        {
            return Result<DashboardOverviewResponse>.Failure(authorization.Error);
        }

        var filter = query.ToFilter();
        var dataSet = await repository.GetDashboardDataSetAsync(query.CompanyId, cancellationToken);
        var occupancy = await repository.GetPositionOccupancyAsync(query.CompanyId, filter, cancellationToken);

        var referenceDate = PersonnelFileDashboardRules.ResolveReferenceDate(query.Year, DateTime.UtcNow);
        var rows = dataSet.Rows
            .Where(row => PersonnelFileDashboardRules.MatchesFilter(row, filter, referenceDate))
            .ToArray();

        var total = rows.Length;
        var active = rows.Count(row => row.IsActive);

        var thresholdMonths = dataSet.FileUpToDateThresholdMonths ?? PersonnelFileDashboardRules.DefaultFileUpToDateThresholdMonths;
        var upToDate = rows.Count(row =>
            PersonnelFileDashboardRules.IsFileUpToDate(row.LifecycleStatus, row.ModifiedAtUtc, referenceDate, thresholdMonths));

        var hrConfigured = !string.IsNullOrWhiteSpace(dataSet.HrFunctionalAreaCode);
        var hrHeadcount = hrConfigured
            ? rows.Count(row => string.Equals(row.FunctionalAreaCode, dataSet.HrFunctionalAreaCode, StringComparison.OrdinalIgnoreCase))
            : 0;
        var ratioPer100 = total == 0
            ? (decimal?)null
            : Math.Round(hrHeadcount * 100m / total, 2);

        var response = new DashboardOverviewResponse(
            new DashboardHeadcountResponse(total, active, total - active),
            Breakdown(rows, row => row.RecordType, row => row.RecordType),
            Breakdown(rows, row => row.EmploymentStatusCode, row => row.EmploymentStatusCode),
            Breakdown(rows, row => row.ContractTypeCode, row => row.ContractTypeCode),
            Breakdown(rows, row => row.PositionCategoryId?.ToString(), row => row.PositionCategoryName),
            Breakdown(rows, row => row.FunctionalAreaId?.ToString(), row => row.FunctionalAreaName),
            Breakdown(rows, row => row.OrgUnitId?.ToString(), row => row.OrgUnitName),
            Breakdown(rows, row => row.WorkCenterId?.ToString(), row => row.WorkCenterName),
            BucketBreakdown(rows, row => PersonnelFileDashboardRules.CalculateAge(row.BirthDate, referenceDate), dataSet.AgeRanges),
            BucketBreakdown(
                rows,
                row => row.HireDate.HasValue
                    ? PersonnelFileDashboardRules.CalculateSeniorityMonths(row.HireDate.Value, referenceDate)
                    : (int?)null,
                dataSet.SeniorityRanges),
            Breakdown(rows, row => row.MaritalStatus, row => row.MaritalStatus),
            new DashboardFileFreshnessResponse(upToDate, total - upToDate, thresholdMonths),
            new DashboardHrRatioResponse(hrHeadcount, total, ratioPer100, hrConfigured),
            new DashboardPositionOccupancyResponse(occupancy.MaxPositions, occupancy.Occupied, occupancy.Vacant),
            PersonnelFileDashboardRules.BuildBreakdownByCode(rows, row => row.PayrollTypeCode, dataSet.PayrollTypeLabels));

        return Result<DashboardOverviewResponse>.Success(response);
    }

    private static IReadOnlyCollection<DashboardBreakdownResponse> Breakdown(
        IReadOnlyCollection<EmployeeDimensionRow> rows,
        Func<EmployeeDimensionRow, string?> keySelector,
        Func<EmployeeDimensionRow, string?> labelSelector) =>
        rows
            .GroupBy(row => keySelector(row) ?? PersonnelFileDashboardRules.UnassignedKey)
            .Select(group => new DashboardBreakdownResponse(
                group.Key,
                labelSelector(group.First()) ?? PersonnelFileDashboardRules.UnassignedLabel,
                group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyCollection<DashboardBreakdownResponse> BucketBreakdown(
        IReadOnlyCollection<EmployeeDimensionRow> rows,
        Func<EmployeeDimensionRow, int?> valueSelector,
        IReadOnlyCollection<RangeBucket> ranges)
    {
        var counts = ranges.ToDictionary(range => range.Code, _ => 0, StringComparer.Ordinal);
        var unknown = 0;

        foreach (var row in rows)
        {
            var value = valueSelector(row);
            var code = value.HasValue ? PersonnelFileDashboardRules.BucketByRange(value.Value, ranges) : null;
            if (code is null)
            {
                unknown++;
            }
            else
            {
                counts[code]++;
            }
        }

        var result = ranges
            .Select(range => new DashboardBreakdownResponse(range.Code, range.Label, counts[range.Code]))
            .ToList();

        if (unknown > 0)
        {
            result.Add(new DashboardBreakdownResponse(
                PersonnelFileDashboardRules.UnassignedKey,
                PersonnelFileDashboardRules.UnknownLabel,
                unknown));
        }

        return result;
    }
}
