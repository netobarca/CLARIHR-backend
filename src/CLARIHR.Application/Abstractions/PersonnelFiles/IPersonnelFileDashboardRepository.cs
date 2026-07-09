using CLARIHR.Application.Features.PersonnelFiles.Reporting;

namespace CLARIHR.Application.Abstractions.PersonnelFiles;

/// <summary>
/// Read-only data source for the HR analytics dashboard. Resolves, per active-assignment, the dimensional
/// row used by every indicator (the join PersonnelFile → asignación activa → puesto/unidad/área/centro), plus
/// the parametrizable range catalogs and the per-company dashboard settings. Pure aggregation/bucketization
/// is done in <see cref="PersonnelFileDashboardRules"/> on top of the rows this returns.
/// </summary>
public interface IPersonnelFileDashboardRepository
{
    /// <summary>
    /// Loads the dimensional dataset for a tenant: one row per <c>Employee</c> personnel file (active and
    /// inactive — the caller filters), the active age/seniority range catalogs and the company's dashboard
    /// parametrization (HR functional-area marker + "expediente actualizado" threshold).
    /// </summary>
    Task<DashboardDataSet> GetDashboardDataSetAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Lightweight metadata for the dashboard UI: the active age/seniority range catalogs (with bounds) and the
    /// resolved company parametrization (HR functional-area marker + freshness threshold). No employee rows.
    /// </summary>
    Task<DashboardMetadata> GetDashboardMetadataAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Position occupancy (plazas ocupadas vs vacantes) aggregated from the position slots, honoring the
    /// dashboard dimension filters (D-13).
    /// </summary>
    Task<DashboardPositionOccupancy> GetPositionOccupancyAsync(
        Guid tenantId,
        DashboardDimensionFilter filter,
        CancellationToken cancellationToken);

    /// <summary>
    /// Span-of-control (colaboradores por jefe, D-05): for every active employee, resolves the manager as the
    /// occupant of the slot referenced by <c>DirectDependencyPositionSlotId</c>, then groups reports per manager.
    /// </summary>
    Task<IReadOnlyCollection<DashboardManagerSpan>> GetSpanOfControlAsync(
        Guid tenantId,
        DashboardDimensionFilter filter,
        CancellationToken cancellationToken);

    /// <summary>
    /// Tenant-wide personnel-action journal facts in the requested window (RF-001): every entry whose
    /// <c>ActionDateUtc</c> falls in <paramref name="year"/> (and <paramref name="month"/>, if supplied), projected
    /// AsNoTracking to the minimal <see cref="ActionFactRow"/> (no monetary fields — aclaración №8). Served by
    /// <c>ix_personnel_file_personnel_actions_tenant_action_date</c> on <c>(tenant_id, action_date_utc)</c>. The
    /// FULL status universe is always returned because the byStatus breakdown must span every status (RN-04); the
    /// APLICADA items split is applied downstream in <see cref="PersonnelActionsDashboardRules.SelectItems"/>, so
    /// <paramref name="includeAllStatuses"/> does not narrow the returned set.
    /// </summary>
    Task<IReadOnlyList<ActionFactRow>> GetPersonnelActionFactsAsync(
        Guid tenantId,
        int year,
        int? month,
        bool includeAllStatuses,
        CancellationToken cancellationToken);

    /// <summary>
    /// Code → display-name labels for the action type (<c>action-types</c>) and action status (<c>action-statuses</c>)
    /// catalogs, backing the byType/byStatus breakdown labels (never hardcoded — aclaración №12).
    /// </summary>
    Task<PersonnelActionCatalogLabels> GetPersonnelActionCatalogLabelsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Code → display-name labels for the retirement category (<c>retirement-categories</c>), retirement reason
    /// (<c>retirement-reasons</c>) and settlement status (<c>settlement-statuses</c>) catalogs, backing the
    /// movements section's byCategory/byReason/settlementsByStatus labels (never hardcoded — aclaración №12).
    /// </summary>
    Task<MovementsCatalogLabels> GetMovementsCatalogLabelsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Of the supplied separation file public ids, the subset whose personnel file has at least one COMPLETED
    /// (<see cref="CLARIHR.Domain.PersonnelFiles.ExitInterviewSubmissionStatus.Submitted"/>) exit-interview
    /// submission — the numerator of the exit-interview coverage ratio (RN-15). Anonymous submissions (no file
    /// link) never match.
    /// </summary>
    Task<IReadOnlyCollection<Guid>> GetCompletedExitInterviewFilePublicIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> separationFilePublicIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Count of REAL settlements (scenarios excluded — they carry a null status) grouped by lifecycle
    /// <c>StatusCode</c> whose RetirementDate falls in <paramref name="year"/> (and <paramref name="month"/>, if
    /// supplied). Counts only — NO monetary aggregates are read (aclaración №8).
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> GetSettlementStatusCountsAsync(
        Guid tenantId,
        int year,
        int? month,
        CancellationToken cancellationToken);
}
