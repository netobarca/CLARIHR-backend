using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical performance-evaluation JSON Patch surface
/// (PersonnelFileTalent remediation): the pure
/// <see cref="PersonnelFilePerformanceEvaluationPatchApplier"/> and the
/// <see cref="PersonnelFilePerformanceEvaluationPatchState"/> projection.
/// </summary>
public sealed class PersonnelFilePerformanceEvaluationPatchTests
{
    private static PersonnelFilePerformanceEvaluationResponse Baseline() =>
        new(
            Guid.NewGuid(),
            "Jane Manager",
            new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            4.5m,
            "EXCEEDS",
            "Strong year.",
            "HRIS",
            "EVAL-1",
            new DateTime(2026, 1, 16, 0, 0, 0, DateTimeKind.Utc),
            Guid.NewGuid());

    private static PersonnelFilePerformanceEvaluationPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFilePerformanceEvaluationPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var state = PersonnelFilePerformanceEvaluationPatchState.From(Baseline());

        Assert.Equal("Jane Manager", state.EvaluatorName);
        Assert.Equal(4.5m, state.Score);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var input = PersonnelFilePerformanceEvaluationPatchState.From(Baseline()).ToInput();

        Assert.Equal("Jane Manager", input.EvaluatorName);
        Assert.Equal("EXCEEDS", input.QualitativeScoreCode);
    }

    [Fact]
    public void Apply_ReplaceEvaluatorName_Mutates()
    {
        var state = PersonnelFilePerformanceEvaluationPatchState.From(Baseline());

        Assert.True(PersonnelFilePerformanceEvaluationPatchApplier.Apply([Replace("/evaluatorName", "John Lead")], state).IsSuccess);
        Assert.Equal("John Lead", state.EvaluatorName);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceScore_AcceptsNumber()
    {
        var state = PersonnelFilePerformanceEvaluationPatchState.From(Baseline());

        Assert.True(PersonnelFilePerformanceEvaluationPatchApplier.Apply([Replace("/score", 3.25m)], state).IsSuccess);
        Assert.Equal(3.25m, state.Score);
    }

    [Fact]
    public void Apply_RemoveNullableScore_ClearsValue()
    {
        var state = PersonnelFilePerformanceEvaluationPatchState.From(Baseline());

        Assert.True(PersonnelFilePerformanceEvaluationPatchApplier.Apply([Remove("/score")], state).IsSuccess);
        Assert.Null(state.Score);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveRequiredEvaluatorName_FailsValidation()
    {
        var state = PersonnelFilePerformanceEvaluationPatchState.From(Baseline());

        Assert.True(PersonnelFilePerformanceEvaluationPatchApplier.Apply([Remove("/evaluatorName")], state).IsSuccess);
        Assert.True(PersonnelFilePerformanceEvaluationPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveEvaluationDate_Fails()
    {
        var state = PersonnelFilePerformanceEvaluationPatchState.From(Baseline());

        Assert.True(PersonnelFilePerformanceEvaluationPatchApplier.Apply([Remove("/evaluationDateUtc")], state).IsFailure);
    }

    [Fact]
    public void Apply_NonStringForEvaluatorName_Fails()
    {
        var state = PersonnelFilePerformanceEvaluationPatchState.From(Baseline());

        Assert.True(PersonnelFilePerformanceEvaluationPatchApplier.Apply([Replace("/evaluatorName", 42)], state).IsFailure);
    }

    [Fact]
    public void Apply_NonNumberForScore_Fails()
    {
        var state = PersonnelFilePerformanceEvaluationPatchState.From(Baseline());

        Assert.True(PersonnelFilePerformanceEvaluationPatchApplier.Apply([Replace("/score", "not-a-number")], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFilePerformanceEvaluationPatchState.From(Baseline());

        Assert.True(PersonnelFilePerformanceEvaluationPatchApplier.Apply(
            [new PersonnelFilePerformanceEvaluationPatchOperation("copy", "/evaluatorName", "/evaluatorName", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFilePerformanceEvaluationPatchState.From(Baseline());

        Assert.True(PersonnelFilePerformanceEvaluationPatchApplier.Apply([Replace("/unknown", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_Fails()
    {
        var state = PersonnelFilePerformanceEvaluationPatchState.From(Baseline());

        Assert.True(PersonnelFilePerformanceEvaluationPatchApplier.Apply([Replace("/evaluatorName/0", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFilePerformanceEvaluationPatchState.From(Baseline());

        Assert.True(PersonnelFilePerformanceEvaluationPatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_Baseline_Succeeds()
    {
        Assert.True(PersonnelFilePerformanceEvaluationPatchApplier.Validate(PersonnelFilePerformanceEvaluationPatchState.From(Baseline())).IsSuccess);
    }

    [Fact]
    public void Validate_BlankEvaluatorName_Fails()
    {
        var state = PersonnelFilePerformanceEvaluationPatchState.From(Baseline());
        state.EvaluatorName = " ";

        Assert.True(PersonnelFilePerformanceEvaluationPatchApplier.Validate(state).IsFailure);
    }
}
