using CLARIHR.Application.Features.PersonnelFiles.Reporting;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit tests for the pure HR-dashboard rules (no I/O): age/seniority computation + bucketization, the
/// "expediente actualizado" rule (D-08), the active-at-year-end approximation (R-02) and the filter predicates.
/// </summary>
public sealed class PersonnelFileDashboardRulesTests
{
    private static readonly DateTime AsOf = new(2026, 06, 27, 0, 0, 0, DateTimeKind.Utc);

    private static readonly IReadOnlyCollection<RangeBucket> AgeRanges = new[]
    {
        new RangeBucket("EDAD_18_25", "18 a 25 años", 18, 25),
        new RangeBucket("EDAD_26_35", "26 a 35 años", 26, 35),
        new RangeBucket("EDAD_56_MAS", "56 años o más", 56, null),
    };

    [Theory]
    [InlineData("2000-06-15", 26)] // birthday already passed this year
    [InlineData("2000-06-27", 26)] // birthday is today
    [InlineData("2000-06-28", 25)] // birthday not reached yet
    public void CalculateAge_RespectsBirthday(string birthDate, int expected)
    {
        var age = PersonnelFileDashboardRules.CalculateAge(DateTime.Parse(birthDate), AsOf);

        Assert.Equal(expected, age);
    }

    [Theory]
    [InlineData("2025-06-27", 12)]
    [InlineData("2026-01-27", 5)]
    [InlineData("2016-06-27", 120)]
    [InlineData("2026-07-01", 0)] // future hire → 0 (never negative)
    public void CalculateSeniorityMonths_ComputesWholeMonths(string hireDate, int expected)
    {
        var months = PersonnelFileDashboardRules.CalculateSeniorityMonths(DateTime.Parse(hireDate), AsOf);

        Assert.Equal(expected, months);
    }

    [Theory]
    [InlineData(20, "EDAD_18_25")]
    [InlineData(26, "EDAD_26_35")]
    [InlineData(70, "EDAD_56_MAS")] // open upper bound
    [InlineData(10, null)] // below all ranges → no bucket
    public void BucketByRange_PicksInclusiveMatchingRange(int value, string? expectedCode)
    {
        var code = PersonnelFileDashboardRules.BucketByRange(value, AgeRanges);

        Assert.Equal(expectedCode, code);
    }

    [Fact]
    public void IsFileUpToDate_RequiresCompletedAndWithinThreshold()
    {
        Assert.True(PersonnelFileDashboardRules.IsFileUpToDate("Completed", AsOf.AddMonths(-6), AsOf, 12));
        Assert.False(PersonnelFileDashboardRules.IsFileUpToDate("Completed", AsOf.AddMonths(-18), AsOf, 12)); // too old
        Assert.False(PersonnelFileDashboardRules.IsFileUpToDate("Draft", AsOf, AsOf, 12)); // not finalized
        Assert.False(PersonnelFileDashboardRules.IsFileUpToDate("Completed", null, AsOf, 12)); // never modified
    }

    [Fact]
    public void IsActiveAtYearEnd_HonorsHireAndRetirement()
    {
        var yearEnd = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        Assert.True(PersonnelFileDashboardRules.IsActiveAtYearEnd(new DateTime(2024, 01, 01), null, yearEnd));
        Assert.False(PersonnelFileDashboardRules.IsActiveAtYearEnd(new DateTime(2026, 01, 01), null, yearEnd)); // hired after
        Assert.False(PersonnelFileDashboardRules.IsActiveAtYearEnd(new DateTime(2024, 01, 01), new DateTime(2025, 06, 30), yearEnd)); // retired before
        Assert.True(PersonnelFileDashboardRules.IsActiveAtYearEnd(new DateTime(2024, 01, 01), new DateTime(2026, 02, 01), yearEnd)); // retired after
        Assert.False(PersonnelFileDashboardRules.IsActiveAtYearEnd(null, null, yearEnd)); // no hire date
    }

    [Fact]
    public void ResolveReferenceDate_UsesYearEndOrToday()
    {
        Assert.Equal(new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc), PersonnelFileDashboardRules.ResolveReferenceDate(2025, AsOf));
        Assert.Equal(AsOf.Date, PersonnelFileDashboardRules.ResolveReferenceDate(null, AsOf));
    }

    [Fact]
    public void MatchesFilter_ExcludesInactiveUnlessIncluded()
    {
        var inactive = Row(isActive: false);

        Assert.False(PersonnelFileDashboardRules.MatchesFilter(inactive, Filter(includeInactive: false), AsOf));
        Assert.True(PersonnelFileDashboardRules.MatchesFilter(inactive, Filter(includeInactive: true), AsOf));
    }

    [Fact]
    public void MatchesFilter_AppliesDimensionEquality()
    {
        var orgUnitId = Guid.NewGuid();
        var row = Row(orgUnitId: orgUnitId);

        Assert.True(PersonnelFileDashboardRules.MatchesFilter(row, Filter(orgUnitId: orgUnitId), AsOf));
        Assert.False(PersonnelFileDashboardRules.MatchesFilter(row, Filter(orgUnitId: Guid.NewGuid()), AsOf));
    }

    [Fact]
    public void MatchesDimensions_IgnoresActiveAndYear()
    {
        var inactive = Row(isActive: false);

        // Dimension-only predicate: an inactive row with matching dimensions still matches (used by altas).
        Assert.True(PersonnelFileDashboardRules.MatchesDimensions(inactive, Filter(includeInactive: false)));
    }

    private static DashboardDimensionFilter Filter(
        int? year = null,
        Guid? functionalAreaId = null,
        Guid? orgUnitId = null,
        Guid? positionCategoryId = null,
        Guid? jobProfileId = null,
        Guid? workCenterId = null,
        bool includeInactive = false) =>
        new(year, functionalAreaId, orgUnitId, positionCategoryId, jobProfileId, workCenterId, includeInactive);

    private static EmployeeDimensionRow Row(
        bool isActive = true,
        Guid? orgUnitId = null) =>
        new(
            FileId: Guid.NewGuid(),
            IsActive: isActive,
            LifecycleStatus: "Completed",
            ModifiedAtUtc: AsOf,
            BirthDate: new DateTime(1990, 01, 01),
            HireDate: new DateTime(2020, 01, 01),
            RetirementDate: null,
            MaritalStatus: "SOLTERO_A",
            RecordType: "Employee",
            EmploymentStatusCode: "ACTIVO",
            ContractTypeCode: "INDEFINIDO",
            OrgUnitId: orgUnitId,
            OrgUnitName: orgUnitId.HasValue ? "Unidad" : null,
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
