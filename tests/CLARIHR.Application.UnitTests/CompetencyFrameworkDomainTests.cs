using CLARIHR.Domain.CompetencyFramework;

namespace CLARIHR.Application.UnitTests;

public sealed class CompetencyFrameworkDomainTests
{
    [Fact]
    public void OccupationalPyramidLevel_Create_ShouldNormalizeAndSetActiveState()
    {
        var level = OccupationalPyramidLevel.Create(" opl-strat ", " Estrategico ", 1, " Nivel superior ");

        Assert.Equal("OPL-STRAT", level.Code);
        Assert.Equal("OPL-STRAT", level.NormalizedCode);
        Assert.Equal("Estrategico", level.Name);
        Assert.Equal("ESTRATEGICO", level.NormalizedName);
        Assert.Equal("Nivel superior", level.Description);
        Assert.True(level.IsActive);
    }

    [Fact]
    public void OccupationalPyramidLevel_Update_ShouldRefreshConcurrencyToken()
    {
        var level = OccupationalPyramidLevel.Create("OPL-1", "Nivel 1", 1, null);
        var beforeToken = level.ConcurrencyToken;

        level.Update("OPL-2", "Nivel 2", 2, " Ajustado ");

        Assert.Equal("OPL-2", level.NormalizedCode);
        Assert.Equal("NIVEL 2", level.NormalizedName);
        Assert.Equal(2, level.LevelOrder);
        Assert.Equal("Ajustado", level.Description);
        Assert.NotEqual(beforeToken, level.ConcurrencyToken);
    }

    [Fact]
    public void CompetencyConduct_Create_ShouldNormalizeDescription()
    {
        var conduct = CompetencyConduct.Create(10, 20, 30, " Lidera con claridad ", 1);

        Assert.Equal("Lidera con claridad", conduct.Description);
        Assert.Equal("LIDERA CON CLARIDAD", conduct.NormalizedDescription);
        Assert.True(conduct.IsActive);
    }

    [Fact]
    public void CompetencyConduct_ReplaceBehaviors_ShouldReplaceCollectionAndRefreshToken()
    {
        var conduct = CompetencyConduct.Create(10, 20, 30, "Descripcion", 1);
        var beforeToken = conduct.ConcurrencyToken;

        conduct.ReplaceBehaviors(
        [
            CompetencyConductBehavior.Create(100, " Evidencia ", 0),
            CompetencyConductBehavior.Create(101, null, 1)
        ]);

        Assert.Equal(2, conduct.Behaviors.Count);
        Assert.NotEqual(beforeToken, conduct.ConcurrencyToken);
    }

    [Fact]
    public void JobProfileCompetencyExpectation_ReplaceConducts_ShouldRefreshConcurrencyToken()
    {
        var expectation = JobProfileCompetencyExpectation.Create(1, 2, 3, 4, 5, " Evidencia ", 0);
        var beforeToken = expectation.ConcurrencyToken;

        expectation.ReplaceConducts(
        [
            JobProfileCompetencyExpectationConduct.Create(100, 0),
            JobProfileCompetencyExpectationConduct.Create(101, 1)
        ]);

        Assert.Equal(2, expectation.Conducts.Count);
        Assert.NotEqual(beforeToken, expectation.ConcurrencyToken);
    }

    [Fact]
    public void CompetencyConductBehavior_Create_WithInvalidCatalogId_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CompetencyConductBehavior.Create(0, null, 0));
    }
}
