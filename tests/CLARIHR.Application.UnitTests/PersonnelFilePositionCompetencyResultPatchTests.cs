using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the position-competency-result JSON Patch surface after the Fase-1 restructure:
/// the result is now an achieved score recorded against a competency matrix expectation, with a derived gap.
/// Exercises the pure <see cref="PersonnelFilePositionCompetencyResultPatchApplier"/> and the
/// <see cref="PersonnelFilePositionCompetencyResultPatchState"/> projection.
/// </summary>
public sealed class PersonnelFilePositionCompetencyResultPatchTests
{
    private static readonly Guid ExpectationId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static PersonnelFilePositionCompetencyResultResponse Baseline() =>
        new(
            Guid.NewGuid(),
            ExpectationId,
            Guid.NewGuid(),
            "LEADERSHIP",
            "Liderazgo",
            Guid.NewGuid(),
            "GESTION",
            "Gestión",
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

        Assert.Equal(ExpectationId, state.ExpectationPublicId);
        Assert.Equal(4m, state.AchievedScore);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var input = PersonnelFilePositionCompetencyResultPatchState.From(Baseline()).ToInput();

        Assert.Equal(ExpectationId, input.ExpectationPublicId);
        Assert.Equal(4m, input.AchievedScore);
    }

    [Fact]
    public void Apply_ReplaceAchievedScore_AcceptsNumber()
    {
        var state = PersonnelFilePositionCompetencyResultPatchState.From(Baseline());

        Assert.True(PersonnelFilePositionCompetencyResultPatchApplier.Apply([Replace("/achievedScore", 4.75m)], state).IsSuccess);
        Assert.Equal(4.75m, state.AchievedScore);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceExpectation_Mutates()
    {
        var state = PersonnelFilePositionCompetencyResultPatchState.From(Baseline());
        var newExpectation = Guid.NewGuid();

        Assert.True(PersonnelFilePositionCompetencyResultPatchApplier.Apply([Replace("/expectationPublicId", newExpectation)], state).IsSuccess);
        Assert.Equal(newExpectation, state.ExpectationPublicId);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveAchievedScore_Fails()
    {
        var state = PersonnelFilePositionCompetencyResultPatchState.From(Baseline());

        Assert.True(PersonnelFilePositionCompetencyResultPatchApplier.Apply([Remove("/achievedScore")], state).IsFailure);
    }

    [Fact]
    public void Apply_NonNumberForAchievedScore_Fails()
    {
        var state = PersonnelFilePositionCompetencyResultPatchState.From(Baseline());

        Assert.True(PersonnelFilePositionCompetencyResultPatchApplier.Apply([Replace("/achievedScore", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_DerivedGapScore_IsNotPatchable()
    {
        var state = PersonnelFilePositionCompetencyResultPatchState.From(Baseline());

        // gapScore is derived (expected − achieved) and not part of the patch surface.
        Assert.True(PersonnelFilePositionCompetencyResultPatchApplier.Apply([Replace("/gapScore", 0m)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFilePositionCompetencyResultPatchState.From(Baseline());

        Assert.True(PersonnelFilePositionCompetencyResultPatchApplier.Apply(
            [new PersonnelFilePositionCompetencyResultPatchOperation("copy", "/achievedScore", "/achievedScore", null)], state).IsFailure);
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

        Assert.True(PersonnelFilePositionCompetencyResultPatchApplier.Apply([Replace("/achievedScore/0", "x")], state).IsFailure);
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
    public void Validate_EmptyExpectation_Fails()
    {
        var state = PersonnelFilePositionCompetencyResultPatchState.From(Baseline());
        state.ExpectationPublicId = Guid.Empty;

        Assert.True(PersonnelFilePositionCompetencyResultPatchApplier.Validate(state).IsFailure);
    }
}
