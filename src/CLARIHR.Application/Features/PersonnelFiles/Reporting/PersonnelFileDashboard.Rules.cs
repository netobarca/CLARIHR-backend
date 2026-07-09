namespace CLARIHR.Application.Features.PersonnelFiles.Reporting;

/// <summary>
/// Pure rules for the HR analytics dashboard (no I/O): reference-date resolution, age/seniority computation
/// and bucketization, the "expediente actualizado" rule (D-08), the active-at-year-end approximation (R-02)
/// and the dimension-filter predicate. Unit-tested in isolation.
/// </summary>
public static class PersonnelFileDashboardRules
{
    public const int DefaultFileUpToDateThresholdMonths = 12;
    public const string CompletedLifecycleStatus = "Completed";
    public const string UnassignedKey = "UNASSIGNED";
    public const string UnassignedLabel = "Sin asignar";
    public const string UnknownLabel = "Sin dato";

    /// <summary>
    /// Descriptive rotation formula the dashboard metadata advertises for the frontend (D-08/RN-10). The movements
    /// section (PR-4) computes it; PR-2 only declares it. Average headcount 0 → tasa "N/D".
    /// </summary>
    public const string RotationFormula =
        "rotación = (bajas del período ÷ headcount promedio) × 100; headcount promedio = (activos al inicio + activos al fin) ÷ 2; si el promedio es 0 la tasa es N/D";

    /// <summary>
    /// Snapshot reference date: with a year filter we approximate "at the close of that year" (Dec 31);
    /// otherwise the current date. Snapshots over a past year are approximate (no historical headcount snapshots — R-02).
    /// </summary>
    public static DateTime ResolveReferenceDate(int? year, DateTime todayUtc) =>
        year.HasValue
            ? new DateTime(year.Value, 12, 31, 0, 0, 0, DateTimeKind.Utc)
            : todayUtc.Date;

    public static int CalculateAge(DateTime birthDate, DateTime asOf)
    {
        var age = asOf.Year - birthDate.Year;
        if (birthDate.Date > asOf.Date.AddYears(-age))
        {
            age--;
        }

        return age < 0 ? 0 : age;
    }

    public static int CalculateSeniorityMonths(DateTime hireDate, DateTime asOf)
    {
        if (hireDate.Date > asOf.Date)
        {
            return 0;
        }

        var months = ((asOf.Year - hireDate.Year) * 12) + asOf.Month - hireDate.Month;
        if (asOf.Day < hireDate.Day)
        {
            months--;
        }

        return months < 0 ? 0 : months;
    }

    /// <summary>R-02 approximation: a file is "active at year end" if hired on/before Dec 31 and not retired before it.</summary>
    public static bool IsActiveAtYearEnd(DateTime? hireDate, DateTime? retirementDate, DateTime yearEnd) =>
        hireDate.HasValue
        && hireDate.Value.Date <= yearEnd.Date
        && (!retirementDate.HasValue || retirementDate.Value.Date > yearEnd.Date);

    /// <summary>D-08: a personnel file is "up to date" when finalized AND modified within the configured window.</summary>
    public static bool IsFileUpToDate(string lifecycleStatus, DateTime? modifiedAtUtc, DateTime asOf, int thresholdMonths)
    {
        if (!string.Equals(lifecycleStatus, CompletedLifecycleStatus, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!modifiedAtUtc.HasValue)
        {
            return false;
        }

        var cutoff = asOf.AddMonths(-Math.Max(1, thresholdMonths));
        return modifiedAtUtc.Value >= cutoff;
    }

    public static bool MatchesFilter(EmployeeDimensionRow row, DashboardDimensionFilter filter, DateTime referenceDate)
    {
        if (!filter.IncludeInactive && !row.IsActive)
        {
            return false;
        }

        if (filter.Year.HasValue && !IsActiveAtYearEnd(row.HireDate, row.RetirementDate, referenceDate))
        {
            return false;
        }

        if (filter.FunctionalAreaId.HasValue && row.FunctionalAreaId != filter.FunctionalAreaId)
        {
            return false;
        }

        if (filter.OrgUnitId.HasValue && row.OrgUnitId != filter.OrgUnitId)
        {
            return false;
        }

        if (filter.PositionCategoryId.HasValue && row.PositionCategoryId != filter.PositionCategoryId)
        {
            return false;
        }

        if (filter.JobProfileId.HasValue && row.JobProfileId != filter.JobProfileId)
        {
            return false;
        }

        if (filter.WorkCenterId.HasValue && row.WorkCenterId != filter.WorkCenterId)
        {
            return false;
        }

        return MatchesPayrollTypeAndCostCenter(row, filter);
    }

    /// <summary>
    /// Dimension-only predicate (área/unidad/tipo-puesto/puesto/centro + tipo-planilla/centro-costo), ignoring the
    /// active/year logic. Used by indicators that scope by event date rather than current-state (e.g. altas by hire
    /// date — D-02).
    /// </summary>
    public static bool MatchesDimensions(EmployeeDimensionRow row, DashboardDimensionFilter filter)
    {
        if (filter.FunctionalAreaId.HasValue && row.FunctionalAreaId != filter.FunctionalAreaId)
        {
            return false;
        }

        if (filter.OrgUnitId.HasValue && row.OrgUnitId != filter.OrgUnitId)
        {
            return false;
        }

        if (filter.PositionCategoryId.HasValue && row.PositionCategoryId != filter.PositionCategoryId)
        {
            return false;
        }

        if (filter.JobProfileId.HasValue && row.JobProfileId != filter.JobProfileId)
        {
            return false;
        }

        if (filter.WorkCenterId.HasValue && row.WorkCenterId != filter.WorkCenterId)
        {
            return false;
        }

        return MatchesPayrollTypeAndCostCenter(row, filter);
    }

    /// <summary>
    /// REQ-004 PR-2: the two new common filters that scope EVERY endpoint (aclaración №6). Payroll type is compared
    /// case-insensitively (stored codes are already normalized); cost center matches the plaza's assigned centre.
    /// <c>Month</c> is intentionally NOT applied here — it is a flow-only filter consumed by PR-3/PR-4.
    /// </summary>
    private static bool MatchesPayrollTypeAndCostCenter(EmployeeDimensionRow row, DashboardDimensionFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.PayrollTypeCode)
            && !string.Equals(row.PayrollTypeCode, filter.PayrollTypeCode, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (filter.CostCenterId.HasValue && row.CostCenterPublicId != filter.CostCenterId)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Groups rows by a catalog-code selector (e.g. payrollTypeCode), resolving each label from
    /// <paramref name="labelsByCode"/> (never hardcoded — aclaración №12). Rows with no code fall into the
    /// <see cref="UnassignedKey"/> bucket labelled <see cref="UnknownLabel"/> ("Sin dato"); a code missing from the
    /// catalog falls back to the raw code as its label. Ordered by descending count, then label.
    /// </summary>
    public static IReadOnlyCollection<DashboardBreakdownResponse> BuildBreakdownByCode(
        IReadOnlyCollection<EmployeeDimensionRow> rows,
        Func<EmployeeDimensionRow, string?> codeSelector,
        IReadOnlyDictionary<string, string> labelsByCode) =>
        rows
            .GroupBy(row => codeSelector(row) ?? UnassignedKey)
            .Select(group => new DashboardBreakdownResponse(
                group.Key,
                group.Key == UnassignedKey
                    ? UnknownLabel
                    : (labelsByCode.TryGetValue(group.Key, out var label) ? label : group.Key),
                group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>Returns the code of the first range whose [lower, upper] (inclusive; null upper = open) contains the value.</summary>
    public static string? BucketByRange(int value, IReadOnlyCollection<RangeBucket> ranges)
    {
        foreach (var range in ranges)
        {
            if (value >= range.LowerBound && (!range.UpperBound.HasValue || value <= range.UpperBound.Value))
            {
                return range.Code;
            }
        }

        return null;
    }
}
