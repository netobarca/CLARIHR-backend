namespace CLARIHR.Application.Features.PersonnelFiles.Reporting;

/// <summary>
/// Code → display-name label dictionaries for the retirement category (<c>retirement-categories</c>), retirement
/// reason (<c>retirement-reasons</c>) and settlement status (<c>settlement-statuses</c>) catalogs. Labels are
/// always resolved from the catalog, never hardcoded (aclaración №12).
/// </summary>
public sealed record MovementsCatalogLabels(
    IReadOnlyDictionary<string, string> CategoryLabels,
    IReadOnlyDictionary<string, string> ReasonLabels,
    IReadOnlyDictionary<string, string> SettlementStatusLabels);

/// <summary>One month of a movements series (hires/separations/net). The net count may be negative.</summary>
public sealed record MovementsSeriesMonth(int Month, int Count);

/// <summary>A 12-month movements series (Jan–Dec) plus its running total.</summary>
public sealed record MovementsSeries(IReadOnlyCollection<MovementsSeriesMonth> ByMonth, int Total);

/// <summary>Separations of the period: the monthly series plus the by-category / by-reason breakdowns.</summary>
public sealed record MovementsSeparations(
    MovementsSeries Series,
    IReadOnlyCollection<DashboardBreakdownResponse> ByCategory,
    IReadOnlyCollection<DashboardBreakdownResponse> ByReason);

/// <summary>Annual turnover: separations ÷ average headcount × 100 (null rate when the average is 0 — "N/D").</summary>
public sealed record MovementsRotation(int Separations, decimal AverageHeadcount, decimal? RatePercent);

/// <summary>Exit-interview coverage: separations of the period whose file has a completed submission ÷ separations.</summary>
public sealed record MovementsExitInterviewCoverage(int Separations, int Completed, decimal? CoveragePercent);

/// <summary>
/// Pure rules for the movements dashboard section (no I/O), unit-tested in isolation (golden A.3). The canonical
/// source of movements is the employee PROFILE, never the journal (aclaración №4 / D-03/RN-03): separations are
/// derived from <see cref="EmployeeDimensionRow.RetirementDate"/> (a reversal clears it → the baja leaves the
/// series/ratios) and hires from <see cref="EmployeeDimensionRow.HireDate"/> — the SAME criterion as
/// <c>dashboard/hires</c>, recomputed here so that endpoint is never touched (riesgo §8). Labels come from the
/// retirement/settlement catalogs, never hardcoded (aclaración №12); "Sin asignar" reuses the dashboard const.
/// </summary>
public static class MovementsDashboardRules
{
    /// <summary>True when <paramref name="date"/> falls in the requested year (and month, if supplied).</summary>
    public static bool FallsInPeriod(DateTime? date, int year, int? month) =>
        date.HasValue && date.Value.Year == year && (!month.HasValue || date.Value.Month == month.Value);

    /// <summary>Monthly hires series by <see cref="EmployeeDimensionRow.HireDate"/> (same criterion as dashboard/hires).</summary>
    public static MovementsSeries BuildHires(IReadOnlyCollection<EmployeeDimensionRow> rows, int year, int? month) =>
        BuildMonthlySeries(rows, year, month, row => row.HireDate);

    /// <summary>
    /// Monthly separations series by <see cref="EmployeeDimensionRow.RetirementDate"/> plus the by-category
    /// (<see cref="EmployeeDimensionRow.RetirementCategoryCode"/>) and by-reason
    /// (<see cref="EmployeeDimensionRow.RetirementReasonCode"/>) breakdowns. A row without a retirement date in
    /// the period is ignored; a reversal clears the date, so a reverted baja never counts (A.3-3).
    /// </summary>
    public static MovementsSeparations BuildSeparations(
        IReadOnlyCollection<EmployeeDimensionRow> rows,
        int year,
        int? month,
        IReadOnlyDictionary<string, string> categoryLabels,
        IReadOnlyDictionary<string, string> reasonLabels)
    {
        var separated = rows
            .Where(row => FallsInPeriod(row.RetirementDate, year, month))
            .ToArray();

        var series = BuildMonthlySeries(separated, year, month, row => row.RetirementDate);
        var byCategory = BuildBreakdown(separated, row => row.RetirementCategoryCode, categoryLabels);
        var byReason = BuildBreakdown(separated, row => row.RetirementReasonCode, reasonLabels);

        return new MovementsSeparations(series, byCategory, byReason);
    }

    /// <summary>Net movement = hires − separations, per month and in total (may be negative — A.3-5).</summary>
    public static MovementsSeries ComputeNet(MovementsSeries hires, MovementsSeries separations)
    {
        var separationsByMonth = separations.ByMonth.ToDictionary(bucket => bucket.Month, bucket => bucket.Count);

        var byMonth = hires.ByMonth
            .Select(bucket => new MovementsSeriesMonth(
                bucket.Month,
                bucket.Count - separationsByMonth.GetValueOrDefault(bucket.Month, 0)))
            .ToArray();

        return new MovementsSeries(byMonth, hires.Total - separations.Total);
    }

    /// <summary>
    /// Turnover rate (D-08/RN-10): <c>separations ÷ averageHeadcount × 100</c>, average headcount =
    /// <c>(headcountStart + headcountEnd) / 2</c> (R-02 approximation with HireDate/RetirementDate). When the
    /// average is 0 the rate is <c>null</c> ("N/D"), never a division by zero (A.3-4).
    /// </summary>
    public static MovementsRotation ComputeRotation(int separations, int headcountStart, int headcountEnd)
    {
        var averageHeadcount = (headcountStart + headcountEnd) / 2m;
        decimal? ratePercent = averageHeadcount == 0m
            ? null
            : Math.Round(separations * 100m / averageHeadcount, 2);

        return new MovementsRotation(separations, averageHeadcount, ratePercent);
    }

    /// <summary>
    /// Exit-interview coverage (D-08/RN-15): the fraction of the period's separations whose file has a completed
    /// exit-interview submission, as a percentage. The denominator is the period's separations, so a reverted
    /// baja (no RetirementDate) is not in it (A.3-9). With 0 separations the coverage is <c>null</c>.
    /// </summary>
    public static MovementsExitInterviewCoverage ComputeExitInterviewCoverage(
        IReadOnlyCollection<Guid> separationFileIds,
        IReadOnlySet<Guid> completedFileIds)
    {
        var separations = separationFileIds.Count;
        var completed = separationFileIds.Count(completedFileIds.Contains);
        decimal? coveragePercent = separations == 0
            ? null
            : Math.Round(completed * 100m / separations, 2);

        return new MovementsExitInterviewCoverage(separations, completed, coveragePercent);
    }

    private static MovementsSeries BuildMonthlySeries(
        IReadOnlyCollection<EmployeeDimensionRow> rows,
        int year,
        int? month,
        Func<EmployeeDimensionRow, DateTime?> dateSelector)
    {
        var counts = new int[13]; // index 1..12
        foreach (var row in rows)
        {
            var date = dateSelector(row);
            if (!FallsInPeriod(date, year, month))
            {
                continue;
            }

            counts[date!.Value.Month]++;
        }

        var byMonth = Enumerable.Range(1, 12)
            .Select(m => new MovementsSeriesMonth(m, counts[m]))
            .ToArray();

        return new MovementsSeries(byMonth, byMonth.Sum(bucket => bucket.Count));
    }

    private static IReadOnlyCollection<DashboardBreakdownResponse> BuildBreakdown(
        IReadOnlyCollection<EmployeeDimensionRow> rows,
        Func<EmployeeDimensionRow, string?> codeSelector,
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
}
