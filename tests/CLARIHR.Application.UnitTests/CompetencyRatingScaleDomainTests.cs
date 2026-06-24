using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.CompetencyFramework;

namespace CLARIHR.Application.UnitTests;

public sealed class PositionCompetencyResultRulesTests
{
    [Theory]
    [InlineData(5, 3, 2)]
    [InlineData(3, 5, -2)]
    [InlineData(4, 4, 0)]
    public void DeriveGap_WithExpected_ReturnsDifference(decimal expected, decimal achieved, decimal gap)
    {
        Assert.Equal(gap, PositionCompetencyResultRules.DeriveGap(expected, achieved));
    }

    [Fact]
    public void DeriveGap_WithoutExpected_ReturnsNull()
    {
        Assert.Null(PositionCompetencyResultRules.DeriveGap(null, 3m));
    }
}

public sealed class CompetencyRatingScaleDomainTests
{
    [Fact]
    public void CreateNumeric_AllowsValuesWithinRange()
    {
        var scale = CompetencyRatingScale.CreateNumeric("ZERO_100", "0 a 100", 0m, 100m, 2);

        Assert.True(scale.IsValueAllowed(0m));
        Assert.True(scale.IsValueAllowed(87.5m));
        Assert.True(scale.IsValueAllowed(100m));
        Assert.False(scale.IsValueAllowed(-1m));
        Assert.False(scale.IsValueAllowed(101m));
        Assert.Empty(scale.Levels);
    }

    [Fact]
    public void CreateNumeric_WithMinNotLessThanMax_Throws()
    {
        Assert.Throws<ArgumentException>(() => CompetencyRatingScale.CreateNumeric("BAD", "Bad", 5m, 5m, 0));
    }

    [Fact]
    public void CreateDiscrete_AllowsLevelValuesOnly()
    {
        var scale = CompetencyRatingScale.CreateDiscrete("A_F", "A a F",
        [
            CompetencyRatingScaleLevel.Create("F", "Deficiente", 0m, 10),
            CompetencyRatingScaleLevel.Create("C", "Bueno", 3m, 20),
            CompetencyRatingScaleLevel.Create("A", "Excelente", 5m, 30),
        ]);

        Assert.True(scale.IsValueAllowed(0m));
        Assert.True(scale.IsValueAllowed(5m));
        Assert.False(scale.IsValueAllowed(4m));
        Assert.Equal(3, scale.Levels.Count);
    }

    [Fact]
    public void CreateDiscrete_WithFewerThanTwoLevels_Throws()
    {
        Assert.Throws<ArgumentException>(() => CompetencyRatingScale.CreateDiscrete("ONE", "One",
        [
            CompetencyRatingScaleLevel.Create("A", "Solo", 1m, 10),
        ]));
    }

    [Fact]
    public void CreateDiscrete_WithDuplicateValues_Throws()
    {
        Assert.Throws<ArgumentException>(() => CompetencyRatingScale.CreateDiscrete("DUP", "Dup",
        [
            CompetencyRatingScaleLevel.Create("A", "Uno", 1m, 10),
            CompetencyRatingScaleLevel.Create("B", "Otro uno", 1m, 20),
        ]));
    }

    [Fact]
    public void Update_SwitchesNumericToDiscrete()
    {
        var scale = CompetencyRatingScale.CreateNumeric("S", "S", 0m, 10m, 0);

        scale.Update("S", "S", CompetencyRatingScaleType.Discrete, null, null, 0,
        [
            CompetencyRatingScaleLevel.Create("1", "Bajo", 1m, 10),
            CompetencyRatingScaleLevel.Create("2", "Alto", 2m, 20),
        ]);

        Assert.Equal(CompetencyRatingScaleType.Discrete, scale.ScaleType);
        Assert.Equal(2, scale.Levels.Count);
        Assert.Null(scale.MinValue);
    }
}
