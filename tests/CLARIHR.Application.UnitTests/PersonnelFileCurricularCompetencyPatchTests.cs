using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical curricular-competency JSON Patch surface
/// (PersonnelFileTalent remediation): the pure
/// <see cref="PersonnelFileCurricularCompetencyPatchApplier"/> and the
/// <see cref="PersonnelFileCurricularCompetencyPatchState"/> projection.
/// </summary>
public sealed class PersonnelFileCurricularCompetencyPatchTests
{
    private static PersonnelFileCurricularCompetencyResponse Baseline() =>
        new(
            Guid.NewGuid(),
            "DEGREE",
            "Computer Science",
            "ENGINEERING",
            5m,
            "YEARS",
            "Required for role.",
            "HRIS",
            "CC-1",
            new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc),
            Guid.NewGuid());

    private static PersonnelFileCurricularCompetencyPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFileCurricularCompetencyPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var state = PersonnelFileCurricularCompetencyPatchState.From(Baseline());

        Assert.Equal("DEGREE", state.RequirementTypeCode);
        Assert.Equal("Computer Science", state.RequirementName);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var input = PersonnelFileCurricularCompetencyPatchState.From(Baseline()).ToInput();

        Assert.Equal("ENGINEERING", input.CompetencyDomain);
        Assert.Equal(5m, input.ExperienceTimeValue);
    }

    [Fact]
    public void Apply_ReplaceRequirementName_Mutates()
    {
        var state = PersonnelFileCurricularCompetencyPatchState.From(Baseline());

        Assert.True(PersonnelFileCurricularCompetencyPatchApplier.Apply([Replace("/requirementName", "Software Engineering")], state).IsSuccess);
        Assert.Equal("Software Engineering", state.RequirementName);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceExperienceTimeValue_AcceptsNumber()
    {
        var state = PersonnelFileCurricularCompetencyPatchState.From(Baseline());

        Assert.True(PersonnelFileCurricularCompetencyPatchApplier.Apply([Replace("/experienceTimeValue", 3.5m)], state).IsSuccess);
        Assert.Equal(3.5m, state.ExperienceTimeValue);
    }

    [Fact]
    public void Apply_RemoveNullableMetricCode_ClearsValue()
    {
        var state = PersonnelFileCurricularCompetencyPatchState.From(Baseline());

        Assert.True(PersonnelFileCurricularCompetencyPatchApplier.Apply([Remove("/metricCode")], state).IsSuccess);
        Assert.Null(state.MetricCode);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveRequiredCompetencyDomain_FailsValidation()
    {
        var state = PersonnelFileCurricularCompetencyPatchState.From(Baseline());

        Assert.True(PersonnelFileCurricularCompetencyPatchApplier.Apply([Remove("/competencyDomain")], state).IsSuccess);
        Assert.True(PersonnelFileCurricularCompetencyPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_NonStringForRequirementTypeCode_Fails()
    {
        var state = PersonnelFileCurricularCompetencyPatchState.From(Baseline());

        Assert.True(PersonnelFileCurricularCompetencyPatchApplier.Apply([Replace("/requirementTypeCode", 42)], state).IsFailure);
    }

    [Fact]
    public void Apply_NonNumberForExperienceTimeValue_Fails()
    {
        var state = PersonnelFileCurricularCompetencyPatchState.From(Baseline());

        Assert.True(PersonnelFileCurricularCompetencyPatchApplier.Apply([Replace("/experienceTimeValue", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFileCurricularCompetencyPatchState.From(Baseline());

        Assert.True(PersonnelFileCurricularCompetencyPatchApplier.Apply(
            [new PersonnelFileCurricularCompetencyPatchOperation("copy", "/requirementName", "/requirementName", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFileCurricularCompetencyPatchState.From(Baseline());

        Assert.True(PersonnelFileCurricularCompetencyPatchApplier.Apply([Replace("/unknown", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_Fails()
    {
        var state = PersonnelFileCurricularCompetencyPatchState.From(Baseline());

        Assert.True(PersonnelFileCurricularCompetencyPatchApplier.Apply([Replace("/requirementName/0", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFileCurricularCompetencyPatchState.From(Baseline());

        Assert.True(PersonnelFileCurricularCompetencyPatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_Baseline_Succeeds()
    {
        Assert.True(PersonnelFileCurricularCompetencyPatchApplier.Validate(PersonnelFileCurricularCompetencyPatchState.From(Baseline())).IsSuccess);
    }

    [Fact]
    public void Validate_BlankRequirementName_Fails()
    {
        var state = PersonnelFileCurricularCompetencyPatchState.From(Baseline());
        state.RequirementName = " ";

        Assert.True(PersonnelFileCurricularCompetencyPatchApplier.Validate(state).IsFailure);
    }
}
