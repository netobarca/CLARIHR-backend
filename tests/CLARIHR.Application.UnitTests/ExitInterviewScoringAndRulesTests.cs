using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

public sealed class ExitInterviewScoringTests
{
    [Theory]
    [InlineData(1, 5, 0)]
    [InlineData(3, 5, 50)]
    [InlineData(4, 5, 75)]
    [InlineData(5, 5, 100)]
    public void NormalizeScale_MapsLikertTo0To100(double value, int scaleMax, double expected) =>
        Assert.Equal((decimal)expected, ExitInterviewScoring.NormalizeScale((decimal)value, scaleMax));

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    public void NormalizeScale_NotScorableScale_ReturnsNull(int? scaleMax) =>
        Assert.Null(ExitInterviewScoring.NormalizeScale(3m, scaleMax));

    [Fact]
    public void NormalizeOptions_AveragesSelectedScores() =>
        Assert.Equal(50m, ExitInterviewScoring.NormalizeOptions([100m, 0m]));

    [Fact]
    public void NormalizeOptions_NoScores_ReturnsNull() =>
        Assert.Null(ExitInterviewScoring.NormalizeOptions([]));

    [Fact]
    public void ComputeIndex_WeightedAverage_RoundsTo2()
    {
        // (2×75 + 1×100) / 3 = 83.33 — the worked example from RF-012.
        var index = ExitInterviewScoring.ComputeIndex([(2m, 75m), (1m, 100m)]);
        Assert.Equal(83.33m, index);
    }

    [Fact]
    public void ComputeIndex_NoWeight_ReturnsNull() =>
        Assert.Null(ExitInterviewScoring.ComputeIndex([]));
}

public sealed class ExitInterviewRulesTests
{
    [Fact]
    public void CheckFieldKeyUnique_Duplicate_Fails()
    {
        var result = ExitInterviewRules.CheckFieldKeyUnique(
            candidatePublicId: null,
            normalizedFieldKey: "MOTIVO",
            siblings: [new ExitInterviewRules.ExistingFieldKey(Guid.NewGuid(), "MOTIVO")]);
        Assert.True(result.IsFailure);
        Assert.Equal("EXIT_INTERVIEW_FIELD_KEY_DUPLICATE", result.Error.Code);
    }

    [Fact]
    public void CheckFieldKeyUnique_Distinct_Succeeds() =>
        Assert.True(ExitInterviewRules.CheckFieldKeyUnique(null, "OTRO", [new ExitInterviewRules.ExistingFieldKey(Guid.NewGuid(), "MOTIVO")]).IsSuccess);

    [Fact]
    public void CheckFieldConfig_RangeNotAllowed_Fails()
    {
        var result = ExitInterviewRules.CheckFieldConfig(supportsRange: false, minValue: 1m, maxValue: 5m);
        Assert.True(result.IsFailure);
        Assert.Equal("EXIT_INTERVIEW_FIELD_RANGE_NOT_ALLOWED", result.Error.Code);
    }

    [Fact]
    public void CheckFieldConfig_MinGreaterThanMax_Fails()
    {
        var result = ExitInterviewRules.CheckFieldConfig(supportsRange: true, minValue: 10m, maxValue: 5m);
        Assert.True(result.IsFailure);
        Assert.Equal("EXIT_INTERVIEW_FIELD_RANGE_INVALID", result.Error.Code);
    }

    [Fact]
    public void CheckFieldConfig_ValidRange_Succeeds() =>
        Assert.True(ExitInterviewRules.CheckFieldConfig(true, 1m, 5m).IsSuccess);

    [Fact]
    public void CheckOptionsAllowed_NonOptionsField_Fails() =>
        Assert.True(ExitInterviewRules.CheckOptionsAllowed(fieldSupportsOptions: false).IsFailure);

    [Fact]
    public void ValidateDefinitionForPublish_NoFields_Fails()
    {
        var result = ExitInterviewRules.ValidateDefinitionForPublish([]);
        Assert.True(result.IsFailure);
        Assert.Equal("EXIT_INTERVIEW_FORM_NOT_PUBLISHABLE", result.Error.Code);
    }

    [Fact]
    public void ValidateDefinitionForPublish_SelectionWithoutOptions_Fails()
    {
        var result = ExitInterviewRules.ValidateDefinitionForPublish(
            [new ExitInterviewRules.FieldDefinition(SupportsOptions: true, SupportsRange: false, MinValue: null, MaxValue: null, ActiveOptionCount: 0)]);
        Assert.True(result.IsFailure);
        Assert.Equal("EXIT_INTERVIEW_FIELD_OPTIONS_REQUIRED", result.Error.Code);
    }

    [Fact]
    public void ValidateDefinitionForPublish_NonSelectionWithOptions_Fails()
    {
        var result = ExitInterviewRules.ValidateDefinitionForPublish(
            [new ExitInterviewRules.FieldDefinition(SupportsOptions: false, SupportsRange: false, MinValue: null, MaxValue: null, ActiveOptionCount: 2)]);
        Assert.True(result.IsFailure);
        Assert.Equal("EXIT_INTERVIEW_FIELD_OPTIONS_NOT_ALLOWED", result.Error.Code);
    }

    [Fact]
    public void ValidateDefinitionForPublish_CoherentDefinition_Succeeds()
    {
        var result = ExitInterviewRules.ValidateDefinitionForPublish(
        [
            new ExitInterviewRules.FieldDefinition(SupportsOptions: true, SupportsRange: false, MinValue: null, MaxValue: null, ActiveOptionCount: 3),
            new ExitInterviewRules.FieldDefinition(SupportsOptions: false, SupportsRange: false, MinValue: null, MaxValue: null, ActiveOptionCount: 0),
        ]);
        Assert.True(result.IsSuccess);
    }
}
