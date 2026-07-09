using CLARIHR.Application.Features.PersonnelFiles.Reporting;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Golden A.3 for the movements dashboard section (REQ-004 PR-4). Deterministic cases pinning the pure
/// <see cref="MovementsDashboardRules"/>: hires/separations 12-month series, the by-category/by-reason breakdowns,
/// the altas−bajas net, the turnover rate (average 0 → null "N/D") and the exit-interview coverage (0 separations
/// → null). The canonical source of movements is the PROFILE, never the journal (aclaración №4): a reverted baja
/// (RetirementDate null) never counts. Labels come from the catalogs, never hardcoded (aclaración №12).
/// </summary>
public sealed class MovementsDashboardRulesTests
{
    private static readonly IReadOnlyDictionary<string, string> CategoryLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["VOLUNTARIA"] = "Renuncia voluntaria",
        ["INVOLUNTARIA"] = "Despido / involuntaria",
    };

    private static readonly IReadOnlyDictionary<string, string> ReasonLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["MEJOR_OFERTA_SALARIAL"] = "Mejor oferta salarial",
        ["BAJO_DESEMPENO"] = "Bajo desempeño",
    };

    // ---- Hires: 12-month series + total; month filter restricts (same criterion as dashboard/hires) ----

    [Fact]
    public void BuildHires_FillsTwelveMonthsAndTotal()
    {
        var rows = new[]
        {
            Row(hireDate: Utc(2026, 3, 10)),
            Row(hireDate: Utc(2026, 3, 20)),
            Row(hireDate: Utc(2026, 7, 1)),
            Row(hireDate: Utc(2025, 7, 1)),   // different year → ignored
            Row(hireDate: null),               // no hire date → ignored
        };

        var hires = MovementsDashboardRules.BuildHires(rows, 2026, month: null);

        Assert.Equal(12, hires.ByMonth.Count);
        Assert.Equal(2, hires.ByMonth.Single(m => m.Month == 3).Count);
        Assert.Equal(1, hires.ByMonth.Single(m => m.Month == 7).Count);
        Assert.Equal(0, hires.ByMonth.Single(m => m.Month == 1).Count);
        Assert.Equal(3, hires.Total);
    }

    [Fact]
    public void BuildHires_MonthFilterRestrictsToThatMonth()
    {
        var rows = new[]
        {
            Row(hireDate: Utc(2026, 3, 10)),
            Row(hireDate: Utc(2026, 7, 1)),
        };

        var hires = MovementsDashboardRules.BuildHires(rows, 2026, month: 7);

        Assert.Equal(1, hires.Total);
        Assert.Equal(1, hires.ByMonth.Single(m => m.Month == 7).Count);
        Assert.Equal(0, hires.ByMonth.Single(m => m.Month == 3).Count);
    }

    // ---- Separations: series + by-category/by-reason; a reverted baja (RetirementDate null) never counts ----

    [Fact]
    public void BuildSeparations_SeriesAndBreakdowns_ExcludesRevertedBaja()
    {
        var rows = new[]
        {
            Row(retirementDate: Utc(2026, 2, 5), categoryCode: "VOLUNTARIA", reasonCode: "MEJOR_OFERTA_SALARIAL"),
            Row(retirementDate: Utc(2026, 2, 20), categoryCode: "VOLUNTARIA", reasonCode: "MEJOR_OFERTA_SALARIAL"),
            Row(retirementDate: Utc(2026, 5, 12), categoryCode: "INVOLUNTARIA", reasonCode: "BAJO_DESEMPENO"),
            Row(retirementDate: Utc(2025, 5, 12), categoryCode: "VOLUNTARIA", reasonCode: "MEJOR_OFERTA_SALARIAL"), // other year → ignored
            Row(retirementDate: null, categoryCode: null, reasonCode: null), // reversal cleared the date → NOT a baja
        };

        var separations = MovementsDashboardRules.BuildSeparations(rows, 2026, month: null, CategoryLabels, ReasonLabels);

        // Series: feb 2, may 1, total 3 (the reverted row and the 2025 row are out).
        Assert.Equal(2, separations.Series.ByMonth.Single(m => m.Month == 2).Count);
        Assert.Equal(1, separations.Series.ByMonth.Single(m => m.Month == 5).Count);
        Assert.Equal(3, separations.Series.Total);

        // byCategory: VOLUNTARIA 2, INVOLUNTARIA 1 — labels from the catalog, descending order.
        Assert.Equal("VOLUNTARIA", separations.ByCategory.First().Key);
        Assert.Equal("Renuncia voluntaria", separations.ByCategory.First().Label);
        Assert.Equal(2, separations.ByCategory.First().Count);
        Assert.Equal(1, separations.ByCategory.Single(item => item.Key == "INVOLUNTARIA").Count);

        // byReason: MEJOR_OFERTA_SALARIAL 2, BAJO_DESEMPENO 1.
        Assert.Equal(2, separations.ByReason.Single(item => item.Key == "MEJOR_OFERTA_SALARIAL").Count);
        Assert.Equal("Mejor oferta salarial", separations.ByReason.Single(item => item.Key == "MEJOR_OFERTA_SALARIAL").Label);
    }

    [Fact]
    public void BuildSeparations_MonthFilterRestricts()
    {
        var rows = new[]
        {
            Row(retirementDate: Utc(2026, 2, 5), categoryCode: "VOLUNTARIA", reasonCode: "MEJOR_OFERTA_SALARIAL"),
            Row(retirementDate: Utc(2026, 5, 12), categoryCode: "INVOLUNTARIA", reasonCode: "BAJO_DESEMPENO"),
        };

        var separations = MovementsDashboardRules.BuildSeparations(rows, 2026, month: 5, CategoryLabels, ReasonLabels);

        Assert.Equal(1, separations.Series.Total);
        Assert.Equal(1, separations.Series.ByMonth.Single(m => m.Month == 5).Count);
        // The category breakdown honors the month scope too (only INVOLUNTARIA in May).
        Assert.Equal("INVOLUNTARIA", Assert.Single(separations.ByCategory).Key);
    }

    [Fact]
    public void BuildSeparations_UncataloguedCodeKeepsRawCode_NullCodeIsUnassigned()
    {
        var rows = new[]
        {
            Row(retirementDate: Utc(2026, 3, 1), categoryCode: "CATEGORIA_SIN_CATALOGO", reasonCode: null),
        };

        var separations = MovementsDashboardRules.BuildSeparations(rows, 2026, month: null, CategoryLabels, ReasonLabels);

        // A category absent from the catalog keeps its raw code as the label (never invented).
        var category = Assert.Single(separations.ByCategory);
        Assert.Equal("CATEGORIA_SIN_CATALOGO", category.Key);
        Assert.Equal("CATEGORIA_SIN_CATALOGO", category.Label);

        // A null reason falls into the "Sin asignar" bucket.
        var reason = Assert.Single(separations.ByReason);
        Assert.Equal(PersonnelFileDashboardRules.UnassignedKey, reason.Key);
        Assert.Equal(PersonnelFileDashboardRules.UnassignedLabel, reason.Label);
    }

    // ---- Net = hires − separations, per month + total (may be negative) ----

    [Fact]
    public void ComputeNet_SubtractsSeparationsFromHiresPerMonthAndTotal()
    {
        var hires = new MovementsSeries(
            new[] { new MovementsSeriesMonth(2, 3), new MovementsSeriesMonth(5, 1) },
            Total: 4);
        var separations = new MovementsSeries(
            new[] { new MovementsSeriesMonth(2, 1), new MovementsSeriesMonth(5, 4) },
            Total: 5);

        var net = MovementsDashboardRules.ComputeNet(hires, separations);

        Assert.Equal(2, net.ByMonth.Single(m => m.Month == 2).Count); // 3 − 1
        Assert.Equal(-3, net.ByMonth.Single(m => m.Month == 5).Count); // 1 − 4 (negative net)
        Assert.Equal(-1, net.Total); // 4 − 5
    }

    // ---- Rotation: 2 bajas / promedio 100 → 2.0 %; promedio 0 → null (A.3-4) ----

    [Fact]
    public void ComputeRotation_TwoSeparationsOverAverageHundred_IsTwoPercent()
    {
        var rotation = MovementsDashboardRules.ComputeRotation(separations: 2, headcountStart: 100, headcountEnd: 100);

        Assert.Equal(2, rotation.Separations);
        Assert.Equal(100m, rotation.AverageHeadcount);
        Assert.Equal(2.0m, rotation.RatePercent);
    }

    [Fact]
    public void ComputeRotation_AverageHeadcountZero_IsNull()
    {
        var rotation = MovementsDashboardRules.ComputeRotation(separations: 0, headcountStart: 0, headcountEnd: 0);

        Assert.Equal(0m, rotation.AverageHeadcount);
        Assert.Null(rotation.RatePercent); // "N/D", never a division by zero
    }

    [Fact]
    public void ComputeRotation_AveragesStartAndEndHeadcount()
    {
        // (10 + 20) / 2 = 15 → 3 / 15 × 100 = 20 %.
        var rotation = MovementsDashboardRules.ComputeRotation(separations: 3, headcountStart: 10, headcountEnd: 20);

        Assert.Equal(15m, rotation.AverageHeadcount);
        Assert.Equal(20.0m, rotation.RatePercent);
    }

    // ---- Coverage: 3/4 → 75 %; denominator excludes reverted; 0 separations → null (A.3-9) ----

    [Fact]
    public void ComputeExitInterviewCoverage_ThreeOfFour_IsSeventyFivePercent()
    {
        var f1 = Guid.NewGuid();
        var f2 = Guid.NewGuid();
        var f3 = Guid.NewGuid();
        var f4 = Guid.NewGuid();
        var separationFileIds = new[] { f1, f2, f3, f4 };
        var completed = new HashSet<Guid> { f1, f2, f3, Guid.NewGuid() /* not a separation → ignored */ };

        var coverage = MovementsDashboardRules.ComputeExitInterviewCoverage(separationFileIds, completed);

        Assert.Equal(4, coverage.Separations);
        Assert.Equal(3, coverage.Completed);
        Assert.Equal(75.0m, coverage.CoveragePercent);
    }

    [Fact]
    public void ComputeExitInterviewCoverage_ZeroSeparations_IsNull()
    {
        var coverage = MovementsDashboardRules.ComputeExitInterviewCoverage(Array.Empty<Guid>(), new HashSet<Guid>());

        Assert.Equal(0, coverage.Separations);
        Assert.Equal(0, coverage.Completed);
        Assert.Null(coverage.CoveragePercent);
    }

    private static DateTime Utc(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    private static EmployeeDimensionRow Row(
        Guid? fileId = null,
        DateTime? hireDate = null,
        DateTime? retirementDate = null,
        string? categoryCode = null,
        string? reasonCode = null) =>
        new(
            FileId: fileId ?? Guid.NewGuid(),
            IsActive: retirementDate is null,
            LifecycleStatus: "Completed",
            ModifiedAtUtc: Utc(2026, 1, 1),
            BirthDate: new DateTime(1990, 1, 1),
            HireDate: hireDate,
            RetirementDate: retirementDate,
            MaritalStatus: null,
            RecordType: "Employee",
            EmploymentStatusCode: "ACTIVO",
            ContractTypeCode: "INDEFINIDO",
            OrgUnitId: null,
            OrgUnitName: null,
            FunctionalAreaId: null,
            FunctionalAreaCode: null,
            FunctionalAreaName: null,
            WorkCenterId: null,
            WorkCenterName: null,
            JobProfileId: null,
            JobProfileTitle: null,
            PositionCategoryId: null,
            PositionCategoryName: null,
            RetirementCategoryCode: categoryCode,
            RetirementReasonCode: reasonCode);
}
