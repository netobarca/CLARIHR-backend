using CLARIHR.Application.Features.PersonnelFiles.Reporting;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Golden A.3 for the documentary personnel-actions dashboard section (REQ-004 PR-3 — the section gate). These
/// deterministic cases pin the pure <see cref="PersonnelActionsDashboardRules"/>: the 12-month series (with the
/// APLICADA default and the month restriction), the type/status/origin/dimension breakdowns, and the invariant
/// that byStatus is ALWAYS computed over the FULL status universe even when the items are only APLICADA (RN-04).
/// Labels come from the catalog/structure, never hardcoded (aclaración №12).
/// </summary>
public sealed class PersonnelActionsDashboardRulesTests
{
    private static readonly IReadOnlyDictionary<string, string> TypeLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["BAJA"] = "Baja / retiro definitivo",
        ["AMONESTACION"] = "Amonestación",
        ["LIQUIDACION"] = "Liquidación de personal",
    };

    private static readonly IReadOnlyDictionary<string, string> StatusLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["APLICADA"] = "Aplicada",
        ["ANULADA"] = "Anulada",
    };

    private static ActionFactRow Fact(
        string type = "BAJA",
        string status = "APLICADA",
        int year = 2026,
        int month = 2,
        bool system = true,
        Guid? filePublicId = null) =>
        new(type, status, new DateTime(year, month, 15, 0, 0, 0, DateTimeKind.Utc), system, filePublicId ?? Guid.NewGuid());

    // ---- A.3-1: series honors the APLICADA default; byStatus always spans the full universe ----

    [Fact]
    public void BuildActionsSeries_FillsTwelveMonthsWithZerosAndTotal()
    {
        // Three APLICADA (feb, feb, may) → {feb: 2, may: 1}, everything else 0, total 3.
        var rows = new[]
        {
            Fact(month: 2),
            Fact(month: 2),
            Fact(month: 5),
        };

        var series = PersonnelActionsDashboardRules.BuildActionsSeries(rows, 2026, month: null);

        Assert.Equal(12, series.ByMonth.Count);
        Assert.Equal(2, series.ByMonth.Single(bucket => bucket.Month == 2).Count);
        Assert.Equal(1, series.ByMonth.Single(bucket => bucket.Month == 5).Count);
        Assert.Equal(0, series.ByMonth.Single(bucket => bucket.Month == 1).Count);
        Assert.Equal(0, series.ByMonth.Single(bucket => bucket.Month == 12).Count);
        Assert.Equal(3, series.Total);
    }

    [Fact]
    public void BuildActionsSeries_MonthFilterRestrictsToThatMonth()
    {
        var rows = new[]
        {
            Fact(month: 2),
            Fact(month: 2),
            Fact(month: 5),
        };

        var series = PersonnelActionsDashboardRules.BuildActionsSeries(rows, 2026, month: 2);

        Assert.Equal(2, series.ByMonth.Single(bucket => bucket.Month == 2).Count);
        Assert.Equal(0, series.ByMonth.Single(bucket => bucket.Month == 5).Count);
        Assert.Equal(2, series.Total);
    }

    [Fact]
    public void BuildActionsSeries_IgnoresRowsOutsideTheYear()
    {
        var rows = new[]
        {
            Fact(year: 2026, month: 3),
            Fact(year: 2025, month: 3),
        };

        var series = PersonnelActionsDashboardRules.BuildActionsSeries(rows, 2026, month: null);

        Assert.Equal(1, series.Total);
        Assert.Equal(1, series.ByMonth.Single(bucket => bucket.Month == 3).Count);
    }

    [Fact]
    public void SelectItems_And_ByStatus_HonorTheAplicadaDefaultOverTheFullUniverse()
    {
        // A.3-1: 3 APLICADA (feb, feb, may) + 1 ANULADA (feb).
        var universe = new[]
        {
            Fact(status: "APLICADA", month: 2),
            Fact(status: "APLICADA", month: 2),
            Fact(status: "APLICADA", month: 5),
            Fact(status: "ANULADA", month: 2),
        };

        // Default (includeAllStatuses=false): items are only the APLICADA → series {feb: 2, may: 1}, total 3.
        var items = PersonnelActionsDashboardRules.SelectItems(universe, includeAllStatuses: false);
        Assert.Equal(3, items.Count);
        var series = PersonnelActionsDashboardRules.BuildActionsSeries(items, 2026, month: null);
        Assert.Equal(2, series.ByMonth.Single(bucket => bucket.Month == 2).Count);
        Assert.Equal(1, series.ByMonth.Single(bucket => bucket.Month == 5).Count);
        Assert.Equal(3, series.Total);

        // byStatus is computed over the FULL universe → it shows the ANULADA entry even though items exclude it.
        var byStatus = PersonnelActionsDashboardRules.BuildBreakdown(universe, row => row.ActionStatusCode, StatusLabels);
        Assert.Equal(3, byStatus.Single(item => item.Key == "APLICADA").Count);
        var annulled = byStatus.Single(item => item.Key == "ANULADA");
        Assert.Equal("Anulada", annulled.Label); // label from the catalog
        Assert.Equal(1, annulled.Count);

        // "incluir todos": items now span every status → series {feb: 3, may: 1}, total 4.
        var allItems = PersonnelActionsDashboardRules.SelectItems(universe, includeAllStatuses: true);
        var allSeries = PersonnelActionsDashboardRules.BuildActionsSeries(allItems, 2026, month: null);
        Assert.Equal(3, allSeries.ByMonth.Single(bucket => bucket.Month == 2).Count);
        Assert.Equal(4, allSeries.Total);
    }

    // ---- BuildBreakdown by type: counts desc, catalog labels, uncatalogued code → raw code ----

    [Fact]
    public void BuildBreakdown_ByType_CountsDescendingWithCatalogLabels()
    {
        var rows = new[]
        {
            Fact(type: "BAJA"),
            Fact(type: "BAJA"),
            Fact(type: "AMONESTACION"),
            Fact(type: "TIPO_SIN_CATALOGO"),
        };

        var byType = PersonnelActionsDashboardRules.BuildBreakdown(rows, row => row.ActionTypeCode, TypeLabels)
            .ToArray();

        Assert.Equal("BAJA", byType[0].Key);
        Assert.Equal("Baja / retiro definitivo", byType[0].Label);
        Assert.Equal(2, byType[0].Count);

        var amonestacion = Assert.Single(byType, item => item.Key == "AMONESTACION");
        Assert.Equal("Amonestación", amonestacion.Label);
        Assert.Equal(1, amonestacion.Count);

        // A type absent from the catalog keeps its raw code as the label (never invented).
        var uncatalogued = Assert.Single(byType, item => item.Key == "TIPO_SIN_CATALOGO");
        Assert.Equal("TIPO_SIN_CATALOGO", uncatalogued.Label);
        Assert.Equal(1, uncatalogued.Count);
    }

    // ---- byOrigin: manual vs automático from IsSystemGenerated ----

    [Fact]
    public void BuildOriginBreakdown_SplitsManualAndAutomatic()
    {
        var rows = new[]
        {
            Fact(system: true),
            Fact(system: true),
            Fact(system: true),
            Fact(system: false),
        };

        var byOrigin = PersonnelActionsDashboardRules.BuildOriginBreakdown(rows);

        var system = Assert.Single(byOrigin, item => item.Key == PersonnelActionsDashboardRules.OriginSystemKey);
        Assert.Equal(PersonnelActionsDashboardRules.OriginSystemLabel, system.Label); // "Automático"
        Assert.Equal(3, system.Count);

        var manual = Assert.Single(byOrigin, item => item.Key == PersonnelActionsDashboardRules.OriginManualKey);
        Assert.Equal(PersonnelActionsDashboardRules.OriginManualLabel, manual.Label); // "Manual"
        Assert.Equal(1, manual.Count);

        // The most frequent origin is ordered first (automático here).
        Assert.Equal(PersonnelActionsDashboardRules.OriginSystemKey, byOrigin.First().Key);
    }

    [Fact]
    public void BuildOriginBreakdown_AlwaysEmitsBothSegmentsEvenWhenEmpty()
    {
        var byOrigin = PersonnelActionsDashboardRules.BuildOriginBreakdown(new[] { Fact(system: true) });

        Assert.Equal(2, byOrigin.Count);
        Assert.Equal(0, byOrigin.Single(item => item.Key == PersonnelActionsDashboardRules.OriginManualKey).Count);
        Assert.Equal(1, byOrigin.Single(item => item.Key == PersonnelActionsDashboardRules.OriginSystemKey).Count);
    }

    // ---- byDimension: an action whose file has no dimensional row → "Sin asignar" (A.3-6) ----

    [Fact]
    public void BuildDimensionBreakdown_AttributesToCurrentUnitAndUnassignedWhenNoRow()
    {
        var orgUnitId = Guid.NewGuid();
        var fileWithRow = Guid.NewGuid();
        var fileWithoutRow = Guid.NewGuid();

        var rowsByFileId = new Dictionary<Guid, EmployeeDimensionRow>
        {
            [fileWithRow] = DimensionRow(fileWithRow, orgUnitId, "Ventas"),
        };

        var items = new[]
        {
            Fact(filePublicId: fileWithRow),
            Fact(filePublicId: fileWithRow),
            Fact(filePublicId: fileWithoutRow), // no dimensional row → "Sin asignar"
        };

        var byOrgUnit = PersonnelActionsDashboardRules.BuildDimensionBreakdown(
            items,
            rowsByFileId,
            row => row.OrgUnitId?.ToString(),
            row => row.OrgUnitName);

        var ventas = Assert.Single(byOrgUnit, item => item.Key == orgUnitId.ToString());
        Assert.Equal("Ventas", ventas.Label);
        Assert.Equal(2, ventas.Count);

        var unassigned = Assert.Single(byOrgUnit, item => item.Key == PersonnelFileDashboardRules.UnassignedKey);
        Assert.Equal(PersonnelFileDashboardRules.UnassignedLabel, unassigned.Label); // "Sin asignar"
        Assert.Equal(1, unassigned.Count);
    }

    [Fact]
    public void BuildDimensionBreakdown_UnassignedWhenTheDimensionItselfIsNull()
    {
        var fileId = Guid.NewGuid();
        var rowsByFileId = new Dictionary<Guid, EmployeeDimensionRow>
        {
            [fileId] = DimensionRow(fileId, orgUnitId: null, orgUnitName: null),
        };

        var byOrgUnit = PersonnelActionsDashboardRules.BuildDimensionBreakdown(
            new[] { Fact(filePublicId: fileId) },
            rowsByFileId,
            row => row.OrgUnitId?.ToString(),
            row => row.OrgUnitName);

        var unassigned = Assert.Single(byOrgUnit);
        Assert.Equal(PersonnelFileDashboardRules.UnassignedKey, unassigned.Key);
        Assert.Equal(1, unassigned.Count);
    }

    private static EmployeeDimensionRow DimensionRow(Guid fileId, Guid? orgUnitId, string? orgUnitName) =>
        new(
            FileId: fileId,
            IsActive: true,
            LifecycleStatus: "Completed",
            ModifiedAtUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            BirthDate: new DateTime(1990, 1, 1),
            HireDate: new DateTime(2020, 1, 1),
            RetirementDate: null,
            MaritalStatus: null,
            RecordType: "Employee",
            EmploymentStatusCode: "ACTIVO",
            ContractTypeCode: "INDEFINIDO",
            OrgUnitId: orgUnitId,
            OrgUnitName: orgUnitName,
            FunctionalAreaId: null,
            FunctionalAreaCode: null,
            FunctionalAreaName: null,
            WorkCenterId: null,
            WorkCenterName: null,
            JobProfileId: null,
            JobProfileTitle: null,
            PositionCategoryId: null,
            PositionCategoryName: null);
}
