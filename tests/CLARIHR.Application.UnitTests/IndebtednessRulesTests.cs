using CLARIHR.Application.Features.PersonnelFiles.Compensation;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// The golden cases of the indebtedness engine (REQ-010 PR-2). The rules that carry the weight are not the
/// arithmetic — it is what happens at the EDGES: an employee with no salary must not be blocked (never divide by
/// zero), a company with no parameters must not be validated at all (the whole feature is opt-in), and a per-type
/// ceiling must prevail over the global one EVEN WHEN IT IS MORE PERMISSIVE (granting a type its own ceiling is
/// the entire point of the table).
/// </summary>
public sealed class IndebtednessRulesTests
{
    private static IndebtednessLoadItem Load(decimal installment, string frequency, bool included = true) =>
        new(Guid.NewGuid(), "PRESTAMO_BANCARIO", "Banco X", "PR-001", installment, frequency, "VIGENTE", included);

    // ── Monthly-ization (RN-13) ───────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("MENSUAL", 100)]      // ×1
    [InlineData("QUINCENAL", 200)]    // ×24/12
    [InlineData("SEMANAL", 433.33)]   // ×52/12 = 4.3333… — the "×4.33" of the analysis, unrounded until the end
    public void Monthlyize_ScalesByThePayPeriod(string frequency, decimal expected) =>
        Assert.Equal(expected, IndebtednessRules.Monthlyize(100m, frequency));

    [Fact]
    public void Monthlyize_AOneOffAmount_SpreadsOverTheYear() =>
        // UNICA = 1 period per year ⇒ a twelfth of it lands on each month.
        Assert.Equal(10m, IndebtednessRules.Monthlyize(120m, "UNICA"));

    [Fact]
    public void Monthlyize_AnUnknownPeriod_DegradesToMonthly() =>
        Assert.Equal(100m, IndebtednessRules.Monthlyize(100m, "LO_QUE_SEA"));

    // ── The base and the load ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeBaseIncome_SumsTheActivePlazas_EachOnItsOwnPayPeriod()
    {
        var items = new[]
        {
            new IndebtednessBaseItem(Guid.NewGuid(), "SALARIO_BASE", 600m, "MENSUAL"),
            new IndebtednessBaseItem(Guid.NewGuid(), "SALARIO_BASE", 100m, "SEMANAL"),
        };

        // 600 + 433.33 — a multi-plaza employee earns the sum, and each plaza is monthly-ized on its own cadence.
        Assert.Equal(1033.33m, IndebtednessRules.ComputeBaseIncome(items));
    }

    [Fact]
    public void ComputeMonthlyLoad_ExcludesWhatIsNotCounted()
    {
        var items = new[]
        {
            Load(170m, "QUINCENAL"),                      // 340 monthly
            Load(50m, "MENSUAL"),                         // 50 monthly
            Load(999m, "MENSUAL", included: false),       // SUSPENDIDO: visible, NOT counted (P-12)
        };

        Assert.Equal(390m, IndebtednessRules.ComputeMonthlyLoad(items));
    }

    // ── Which ceiling applies ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ResolveLimit_ThePerTypeCeilingPrevails_EvenWhenItIsMorePermissive()
    {
        var byType = new Dictionary<string, decimal> { ["PRESTAMO_BANCARIO"] = 45m };

        // The global is 30% and the type's is 45%: the type wins. RF-020 says "a bank loan validates against its
        // ceiling", NOT against min(global, type). Granting a type more room is exactly what the table is for.
        var (percent, source) = IndebtednessRules.ResolveLimit("PRESTAMO_BANCARIO", 30m, byType);

        Assert.Equal(45m, percent);
        Assert.Equal(IndebtednessLimitSources.PorTipo, source);
    }

    [Fact]
    public void ResolveLimit_ATypeWithoutARow_FallsBackToTheGlobal()
    {
        var byType = new Dictionary<string, decimal> { ["PRESTAMO_BANCARIO"] = 25m };

        var (percent, source) = IndebtednessRules.ResolveLimit("COOPERATIVA", 30m, byType);

        Assert.Equal(30m, percent);
        Assert.Equal(IndebtednessLimitSources.Global, source);
    }

    [Fact]
    public void ResolveLimit_WithNoParametersAtAll_ThereIsNoCeiling()
    {
        var (percent, source) = IndebtednessRules.ResolveLimit("PRESTAMO_BANCARIO", null, null);

        Assert.Null(percent);
        Assert.Null(source);
    }

    // ── The verdict ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Assess_TheGoldenCaseOfTheAnalysis_Exceeds()
    {
        // Salary $1,200 · load $340 · new installment $80 · limit 30% → (340+80)/1200 = 35% → exceeded.
        var assessment = IndebtednessRules.Assess(
            baseIncome: 1200m,
            loadItems: [Load(340m, "MENSUAL")],
            newInstallment: 80m,
            newInstallmentFrequencyCode: "MENSUAL",
            globalLimitPercent: 30m,
            limitsByType: null,
            candidateTypeCode: "PRESTAMO_BANCARIO");

        Assert.Equal(1200m, assessment.BaseIncome);
        Assert.Equal(340m, assessment.CurrentLoad);
        Assert.Equal(80m, assessment.NewInstallment);
        Assert.Equal(35m, assessment.ProjectedPercent);
        Assert.Equal(30m, assessment.LimitPercent);
        Assert.True(assessment.IsExceeded);
        Assert.Equal(IndebtednessStatuses.Excedido, assessment.Status);
    }

    [Fact]
    public void Assess_SittingExactlyOnTheCeiling_DoesNotExceedIt()
    {
        // 30% of 1000 is 300, and 300 is not MORE than 300. The comparison is strict: a limit is a limit, and
        // hitting it exactly is still compliant.
        var assessment = IndebtednessRules.Assess(
            baseIncome: 1000m,
            loadItems: [Load(200m, "MENSUAL")],
            newInstallment: 100m,
            newInstallmentFrequencyCode: "MENSUAL",
            globalLimitPercent: 30m,
            limitsByType: null,
            candidateTypeCode: null);

        Assert.Equal(30m, assessment.ProjectedPercent);
        Assert.False(assessment.IsExceeded);
        Assert.Equal(IndebtednessStatuses.Dentro, assessment.Status);
    }

    [Fact]
    public void Assess_WithNoParameters_NeverExceeds()
    {
        // A company that configured nothing has NO indebtedness control: even a deduction that eats the entire
        // salary registers without a warning. That is what keeps REQ-008/009 working exactly as before.
        var assessment = IndebtednessRules.Assess(
            baseIncome: 1000m,
            loadItems: [Load(900m, "MENSUAL")],
            newInstallment: 500m,
            newInstallmentFrequencyCode: "MENSUAL",
            globalLimitPercent: null,
            limitsByType: null,
            candidateTypeCode: "PRESTAMO_BANCARIO");

        Assert.Equal(140m, assessment.ProjectedPercent);   // the figure is still reported...
        Assert.Null(assessment.LimitPercent);
        Assert.False(assessment.IsExceeded);               // ...but nothing is exceeded, because nothing was set.
        Assert.Equal(IndebtednessStatuses.SinControl, assessment.Status);
    }

    [Fact]
    public void Assess_AnEmployeeWithNoSalary_IsNeverBlocked()
    {
        // THE edge that must not blow up: base 0 would make the percentage infinite and would lock out an employee
        // whose salary simply is not configured yet. No income ⇒ no percentage ⇒ no exceeding.
        var assessment = IndebtednessRules.Assess(
            baseIncome: 0m,
            loadItems: [Load(100m, "MENSUAL")],
            newInstallment: 50m,
            newInstallmentFrequencyCode: "MENSUAL",
            globalLimitPercent: 30m,
            limitsByType: null,
            candidateTypeCode: null);

        Assert.Equal(0m, assessment.ProjectedPercent);
        Assert.False(assessment.IsExceeded);
    }

    [Fact]
    public void Assess_WithoutACandidate_ReportsTheCurrentSituation()
    {
        // The plain query adds nothing: it is the employee's standing today.
        var assessment = IndebtednessRules.Assess(
            baseIncome: 1000m,
            loadItems: [Load(150m, "QUINCENAL")],          // 300 monthly
            newInstallment: null,
            newInstallmentFrequencyCode: null,
            globalLimitPercent: 25m,
            limitsByType: null,
            candidateTypeCode: null);

        Assert.Equal(0m, assessment.NewInstallment);
        Assert.Equal(300m, assessment.CurrentLoad);
        Assert.Equal(30m, assessment.ProjectedPercent);
        Assert.True(assessment.IsExceeded);               // already over the ceiling, before adding anything
    }

    [Fact]
    public void Assess_ThePerTypeCeiling_IsTheOneCompared()
    {
        var byType = new Dictionary<string, decimal> { ["PRESTAMO_BANCARIO"] = 25m };

        // 28% is under the global 30% but over the loan's own 25% → the loan is refused (pending confirmation).
        var assessment = IndebtednessRules.Assess(
            baseIncome: 1000m,
            loadItems: [Load(180m, "MENSUAL")],
            newInstallment: 100m,
            newInstallmentFrequencyCode: "MENSUAL",
            globalLimitPercent: 30m,
            limitsByType: byType,
            candidateTypeCode: "PRESTAMO_BANCARIO");

        Assert.Equal(28m, assessment.ProjectedPercent);
        Assert.Equal(25m, assessment.LimitPercent);
        Assert.Equal(IndebtednessLimitSources.PorTipo, assessment.LimitSource);
        Assert.True(assessment.IsExceeded);
    }

    [Fact]
    public void Assess_TheCandidateInstallment_IsMonthlyizedBeforeBeingCompared()
    {
        // A $100 WEEKLY deduction is NOT $100 of monthly debt — it is $433.33. Comparing the raw installment
        // would let a weekly credit slip four times under the ceiling.
        var assessment = IndebtednessRules.Assess(
            baseIncome: 1000m,
            loadItems: [],
            newInstallment: 100m,
            newInstallmentFrequencyCode: "SEMANAL",
            globalLimitPercent: 30m,
            limitsByType: null,
            candidateTypeCode: null);

        Assert.Equal(433.33m, assessment.NewInstallment);
        Assert.Equal(43.33m, assessment.ProjectedPercent);
        Assert.True(assessment.IsExceeded);
    }

    [Fact]
    public void ToProblemExtensions_CarriesTheWholeBreakdown()
    {
        var assessment = IndebtednessRules.Assess(
            1200m, [Load(340m, "MENSUAL")], 80m, "MENSUAL", 30m, null, null);

        var extensions = IndebtednessRules.ToProblemExtensions(assessment);

        // The client renders the confirmation dialog from these figures; they cannot ride in `detail`, which the
        // localizer overwrites with the catalogued message.
        Assert.Equal(1200m, extensions["baseIncome"]);
        Assert.Equal(340m, extensions["currentLoad"]);
        Assert.Equal(80m, extensions["newInstallment"]);
        Assert.Equal(35m, extensions["projectedPercent"]);
        Assert.Equal(30m, extensions["limitPercent"]);
        Assert.Equal(IndebtednessLimitSources.Global, extensions["limitSource"]);
    }
}
