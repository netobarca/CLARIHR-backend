using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical position-competency-result JSON Patch surface
/// (PersonnelFileTalent remediation): the pure
/// <see cref="PersonnelFilePositionCompetencyResultPatchApplier"/> and the
/// <see cref="PersonnelFilePositionCompetencyResultPatchState"/> projection.
/// </summary>
public sealed class PersonnelFilePositionCompetencyResultPatchTests
{
    private static PersonnelFilePositionCompetencyResultResponse Baseline() =>
        new(
            Guid.NewGuid(),
            "LEADERSHIP",
            "Mentors peers.",
            5m,
            4m,
            1m,
            new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            "HRIS",
            "PCR-1",
            new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc),
            Guid.NewGuid());

    private static PersonnelFilePositionCompetencyResultPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFilePositionCompetencyResultPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var state = PersonnelFilePositionCompetencyResultPatchState.From(Baseline());

        Assert.Equal("LEADERSHIP", state.CompetencyCode);
        Assert.Equal(4m, state.AchievedScore);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var input = PersonnelFilePositionCompetencyResultPatchState.From(Baseline()).ToInput();

        Assert.Equal("LEADERSHIP", input.CompetencyCode);
        Assert.Equal(1m, input.GapScore);
    }

    [Fact]
    public void Apply_ReplaceCompetencyCode_Mutates()
    {
        var state = PersonnelFilePositionCompetencyResultPatchState.From(Baseline());

        Assert.True(PersonnelFilePositionCompetencyResultPatchApplier.Apply([Replace("/competencyCode", "TEAMWORK")], state).IsSuccess);
        Assert.Equal("TEAMWORK", state.CompetencyCode);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceAchievedScore_AcceptsNumber()
    {
        var state = PersonnelFilePositionCompetencyResultPatchState.From(Baseline());

        Assert.True(PersonnelFilePositionCompetencyResultPatchApplier.Apply([Replace("/achievedScore", 4.75m)], state).IsSuccess);
        Assert.Equal(4.75m, state.AchievedScore);
    }

    [Fact]
    public void Apply_RemoveNullableGapScore_ClearsValue()
    {
        var state = PersonnelFilePositionCompetencyResultPatchState.From(Baseline());

        Assert.True(PersonnelFilePositionCompetencyResultPatchApplier.Apply([Remove("/gapScore")], state).IsSuccess);
        Assert.Null(state.GapScore);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveRequiredCompetencyCode_FailsValidation()
    {
        var state = PersonnelFilePositionCompetencyResultPatchState.From(Baseline());

        Assert.True(PersonnelFilePositionCompetencyResultPatchApplier.Apply([Remove("/competencyCode")], state).IsSuccess);
        Assert.True(PersonnelFilePositionCompetencyResultPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_NonNumberForExpectedScore_Fails()
    {
        var state = PersonnelFilePositionCompetencyResultPatchState.From(Baseline());

        Assert.True(PersonnelFilePositionCompetencyResultPatchApplier.Apply([Replace("/expectedScore", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFilePositionCompetencyResultPatchState.From(Baseline());

        Assert.True(PersonnelFilePositionCompetencyResultPatchApplier.Apply(
            [new PersonnelFilePositionCompetencyResultPatchOperation("copy", "/competencyCode", "/competencyCode", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFilePositionCompetencyResultPatchState.From(Baseline());

        Assert.True(PersonnelFilePositionCompetencyResultPatchApplier.Apply([Replace("/unknown", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_Fails()
    {
        var state = PersonnelFilePositionCompetencyResultPatchState.From(Baseline());

        Assert.True(PersonnelFilePositionCompetencyResultPatchApplier.Apply([Replace("/competencyCode/0", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFilePositionCompetencyResultPatchState.From(Baseline());

        Assert.True(PersonnelFilePositionCompetencyResultPatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_Baseline_Succeeds()
    {
        Assert.True(PersonnelFilePositionCompetencyResultPatchApplier.Validate(PersonnelFilePositionCompetencyResultPatchState.From(Baseline())).IsSuccess);
    }

    [Fact]
    public void Validate_BlankCompetencyCode_Fails()
    {
        var state = PersonnelFilePositionCompetencyResultPatchState.From(Baseline());
        state.CompetencyCode = " ";

        Assert.True(PersonnelFilePositionCompetencyResultPatchApplier.Validate(state).IsFailure);
    }
}
