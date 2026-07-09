namespace CLARIHR.Application.Features.PersonnelFiles.Reporting;

/// <summary>
/// One minimal row of the tenant's personnel-action journal in the requested window (repository projection,
/// AsNoTracking + Select). <see cref="PersonnelFilePublicId"/> is the owning file's public id, used to join to
/// the dimensional row bundle (<see cref="EmployeeDimensionRow.FileId"/>) for the by-dimension breakdown. No
/// monetary fields are projected (aclaración №8 — the settlement entry carries the net amount and it is never
/// exposed by the dashboard).
/// </summary>
public sealed record ActionFactRow(
    string ActionTypeCode,
    string ActionStatusCode,
    DateTime ActionDateUtc,
    bool IsSystemGenerated,
    Guid PersonnelFilePublicId);

/// <summary>Code → display-name label dictionaries for the action type/status catalogs (labels never hardcoded — aclaración №12).</summary>
public sealed record PersonnelActionCatalogLabels(
    IReadOnlyDictionary<string, string> TypeLabels,
    IReadOnlyDictionary<string, string> StatusLabels);

/// <summary>A 12-month value series plus its running total for the documentary-actions section.</summary>
public sealed record ActionsSeries(IReadOnlyCollection<ActionsSeriesMonth> ByMonth, int Total);

public sealed record ActionsSeriesMonth(int Month, int Count);

/// <summary>
/// Pure rules for the documentary personnel-actions dashboard section (no I/O): monthly series, catalog/origin/
/// dimension breakdowns and the APLICADA-default items split. Unit-tested in isolation (golden A.3). Labels come
/// from catalogs/structure, never hardcoded (aclaración №12); "Sin asignar"/"Sin dato" reuse the dashboard consts.
/// </summary>
public static class PersonnelActionsDashboardRules
{
    /// <summary>Default documentary population (D-05/RN-04): only APLICADA entries feed the items unless includeAllStatuses.</summary>
    public const string AppliedStatusCode = "APLICADA";

    public const string OriginManualKey = "MANUAL";
    public const string OriginSystemKey = "SYSTEM";
    public const string OriginManualLabel = "Manual";
    public const string OriginSystemLabel = "Automático";

    /// <summary>
    /// Splits the scoped universe into the "items" set: every status when <paramref name="includeAllStatuses"/> is
    /// true (D-05), otherwise only <see cref="AppliedStatusCode"/>. The byStatus breakdown is ALWAYS computed over
    /// the full universe (never this set) so it spans every status regardless of the default (RN-04).
    /// </summary>
    public static IReadOnlyList<ActionFactRow> SelectItems(IReadOnlyCollection<ActionFactRow> universe, bool includeAllStatuses) =>
        includeAllStatuses
            ? universe.ToArray()
            : universe
                .Where(row => string.Equals(row.ActionStatusCode, AppliedStatusCode, StringComparison.OrdinalIgnoreCase))
                .ToArray();

    /// <summary>
    /// 12 monthly buckets (Jan–Dec) for <paramref name="year"/>, zeros where no action falls, plus the total.
    /// When <paramref name="month"/> is supplied only that month is counted (the rest stay zero) — the flow month
    /// filter restricts the series to a single month (A.3-7). Rows outside <paramref name="year"/> are ignored.
    /// </summary>
    public static ActionsSeries BuildActionsSeries(IReadOnlyCollection<ActionFactRow> rows, int year, int? month)
    {
        var counts = new int[13]; // index 1..12
        foreach (var row in rows)
        {
            if (row.ActionDateUtc.Year != year)
            {
                continue;
            }

            var rowMonth = row.ActionDateUtc.Month;
            if (month.HasValue && rowMonth != month.Value)
            {
                continue;
            }

            counts[rowMonth]++;
        }

        var byMonth = Enumerable.Range(1, 12)
            .Select(m => new ActionsSeriesMonth(m, counts[m]))
            .ToArray();

        return new ActionsSeries(byMonth, byMonth.Sum(bucket => bucket.Count));
    }

    /// <summary>
    /// Groups <paramref name="rows"/> by a catalog code (e.g. ActionTypeCode/ActionStatusCode), resolving each
    /// label from <paramref name="labels"/> (never hardcoded — aclaración №12). A code missing from the catalog
    /// falls back to the raw code; a null code to the "Sin asignar" bucket. Ordered by descending count, then label.
    /// </summary>
    public static IReadOnlyCollection<DashboardBreakdownResponse> BuildBreakdown(
        IReadOnlyCollection<ActionFactRow> rows,
        Func<ActionFactRow, string?> codeSelector,
        IReadOnlyDictionary<string, string> labels) =>
        rows
            .GroupBy(row => codeSelector(row) ?? PersonnelFileDashboardRules.UnassignedKey)
            .Select(group => new DashboardBreakdownResponse(
                group.Key,
                group.Key == PersonnelFileDashboardRules.UnassignedKey
                    ? PersonnelFileDashboardRules.UnassignedLabel
                    : (labels.TryGetValue(group.Key, out var label) ? label : group.Key),
                group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>
    /// Origin distribution (manual vs automático = <see cref="ActionFactRow.IsSystemGenerated"/>). Always emits both
    /// segments (0 when empty) so the frontend renders the two-slice donut deterministically. Descending by count.
    /// </summary>
    public static IReadOnlyCollection<DashboardBreakdownResponse> BuildOriginBreakdown(IReadOnlyCollection<ActionFactRow> rows)
    {
        var system = rows.Count(row => row.IsSystemGenerated);
        var manual = rows.Count - system;

        return new[]
        {
            new DashboardBreakdownResponse(OriginManualKey, OriginManualLabel, manual),
            new DashboardBreakdownResponse(OriginSystemKey, OriginSystemLabel, system),
        }
        .OrderByDescending(item => item.Count)
        .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    }

    /// <summary>
    /// Groups the items by a dimension of the joined <see cref="EmployeeDimensionRow"/> (org unit/área/centro/etc.).
    /// An action whose file has no dimensional row — or whose dimension is null — falls into "Sin asignar" (the
    /// D-07 approximation attributes each action to the employee's CURRENT unit). Labels come from the row
    /// (structure). Ordered by descending count, then label.
    /// </summary>
    public static IReadOnlyCollection<DashboardBreakdownResponse> BuildDimensionBreakdown(
        IReadOnlyCollection<ActionFactRow> items,
        IReadOnlyDictionary<Guid, EmployeeDimensionRow> rowsByFileId,
        Func<EmployeeDimensionRow, string?> keySelector,
        Func<EmployeeDimensionRow, string?> labelSelector) =>
        items
            .Select(item =>
            {
                if (rowsByFileId.TryGetValue(item.PersonnelFilePublicId, out var row) && keySelector(row) is string key)
                {
                    return new BreakdownBucket(key, labelSelector(row) ?? key);
                }

                return new BreakdownBucket(PersonnelFileDashboardRules.UnassignedKey, PersonnelFileDashboardRules.UnassignedLabel);
            })
            .GroupBy(bucket => bucket.Key)
            .Select(group => new DashboardBreakdownResponse(group.Key, group.First().Label, group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private readonly record struct BreakdownBucket(string Key, string Label);
}
