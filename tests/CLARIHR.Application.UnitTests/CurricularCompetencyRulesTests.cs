using CLARIHR.Application.Features.PersonnelFiles;
using Xunit;

namespace CLARIHR.Application.UnitTests;

public sealed class CurricularCompetencyRulesTests
{
    // ── ValidateExperience (D-06 + metric coherence) ──────────────────────────────────────────────

    [Fact]
    public void ValidateExperience_NoValueNoMetric_Succeeds()
    {
        var result = CurricularCompetencyRules.ValidateExperience(value: null, metricCode: null);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateExperience_NoValueWithMetric_Succeeds()
    {
        var result = CurricularCompetencyRules.ValidateExperience(value: null, metricCode: "ANOS");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateExperience_ZeroWithMetric_Succeeds()
    {
        var result = CurricularCompetencyRules.ValidateExperience(0m, "ANOS");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateExperience_PositiveWithMetric_Succeeds()
    {
        var result = CurricularCompetencyRules.ValidateExperience(5m, "ANOS");

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void ValidateExperience_ValueWithoutMetric_FailsMetricRequired(int value)
    {
        var result = CurricularCompetencyRules.ValidateExperience(value, metricCode: "   ");

        Assert.True(result.IsFailure);
        Assert.Equal("CURRICULAR_COMPETENCY_METRIC_REQUIRED", result.Error.Code);
    }

    [Fact]
    public void ValidateExperience_NegativeValue_FailsExperienceNegative()
    {
        var result = CurricularCompetencyRules.ValidateExperience(-1m, "ANOS");

        Assert.True(result.IsFailure);
        Assert.Equal("CURRICULAR_COMPETENCY_EXPERIENCE_NEGATIVE", result.Error.Code);
    }

    [Fact]
    public void ValidateExperience_NegativeValueTakesPrecedenceOverMissingMetric()
    {
        var result = CurricularCompetencyRules.ValidateExperience(-1m, metricCode: null);

        Assert.True(result.IsFailure);
        Assert.Equal("CURRICULAR_COMPETENCY_EXPERIENCE_NEGATIVE", result.Error.Code);
    }

    // ── Key normalization ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Key_TrimsAndUpperCasesBothParts()
    {
        Assert.Equal(
            CurricularCompetencyRules.Key("PROF", "Excel Avanzado"),
            CurricularCompetencyRules.Key("  prof  ", "  excel avanzado  "));
    }

    // ── CheckDuplicate (D-05) ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void CheckDuplicate_NoSiblings_Succeeds()
    {
        var result = CurricularCompetencyRules.CheckDuplicate(
            candidatePublicId: null, "PROF", "Excel", []);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void CheckDuplicate_DifferentTypeSameName_Succeeds()
    {
        var siblings = new[]
        {
            new CurricularCompetencyRules.Existing(Guid.NewGuid(), CurricularCompetencyRules.Key("EDU", "Excel")),
        };

        var result = CurricularCompetencyRules.CheckDuplicate(
            candidatePublicId: null, "PROF", "Excel", siblings);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void CheckDuplicate_SameTypeAndNameCaseInsensitive_Fails()
    {
        var siblings = new[]
        {
            new CurricularCompetencyRules.Existing(Guid.NewGuid(), CurricularCompetencyRules.Key("PROF", "Excel Avanzado")),
        };

        var result = CurricularCompetencyRules.CheckDuplicate(
            candidatePublicId: null, "prof", "  excel avanzado  ", siblings);

        Assert.True(result.IsFailure);
        Assert.Equal("CURRICULAR_COMPETENCY_DUPLICATE", result.Error.Code);
    }

    [Fact]
    public void CheckDuplicate_SameRowOnUpdate_IsExcluded()
    {
        var selfId = Guid.NewGuid();
        var siblings = new[]
        {
            new CurricularCompetencyRules.Existing(selfId, CurricularCompetencyRules.Key("PROF", "Excel")),
        };

        var result = CurricularCompetencyRules.CheckDuplicate(
            candidatePublicId: selfId, "PROF", "Excel", siblings);

        Assert.True(result.IsSuccess);
    }
}
