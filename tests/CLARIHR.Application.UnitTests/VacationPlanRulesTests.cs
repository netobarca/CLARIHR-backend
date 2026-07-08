using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Pure plan-line arithmetic (PR-9): the same-employee overlap detection that lets the handler surface a clean
/// 422 (VACATION_PLAN_LINE_OVERLAP) before the domain guard fires.
/// </summary>
public sealed class VacationPlanRulesTests
{
    private static readonly Guid EmployeeA = Guid.NewGuid();
    private static readonly Guid EmployeeB = Guid.NewGuid();

    private static VacationPlanLineItem Line(Guid employee, string start, string end, int days = 5) =>
        new(employee, DateOnly.Parse(start), DateOnly.Parse(end), days);

    [Fact]
    public void HasOverlappingLines_DisjointWindowsSameEmployee_ReturnsFalse()
    {
        var lines = new[]
        {
            Line(EmployeeA, "2026-03-01", "2026-03-05"),
            Line(EmployeeA, "2026-06-01", "2026-06-05"),
        };

        Assert.False(VacationPlanRules.HasOverlappingLines(lines));
    }

    [Fact]
    public void HasOverlappingLines_OverlappingWindowsSameEmployee_ReturnsTrue()
    {
        var lines = new[]
        {
            Line(EmployeeA, "2026-03-01", "2026-03-10"),
            Line(EmployeeA, "2026-03-08", "2026-03-12"),
        };

        Assert.True(VacationPlanRules.HasOverlappingLines(lines));
    }

    [Fact]
    public void HasOverlappingLines_SharedBoundaryDay_ReturnsTrue()
    {
        var lines = new[]
        {
            Line(EmployeeA, "2026-03-01", "2026-03-05"),
            Line(EmployeeA, "2026-03-05", "2026-03-08"),
        };

        Assert.True(VacationPlanRules.HasOverlappingLines(lines));
    }

    [Fact]
    public void HasOverlappingLines_OverlapAcrossDifferentEmployees_ReturnsFalse()
    {
        // Two employees may plan the very same window — only same-employee overlaps count.
        var lines = new[]
        {
            Line(EmployeeA, "2026-03-01", "2026-03-10"),
            Line(EmployeeB, "2026-03-01", "2026-03-10"),
        };

        Assert.False(VacationPlanRules.HasOverlappingLines(lines));
    }

    [Fact]
    public void HasOverlappingLines_EmptySet_ReturnsFalse() =>
        Assert.False(VacationPlanRules.HasOverlappingLines([]));
}
