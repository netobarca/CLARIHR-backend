using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical selection-contest JSON Patch surface
/// (PersonnelFileTalent remediation): the pure
/// <see cref="PersonnelFileSelectionContestPatchApplier"/> and the
/// <see cref="PersonnelFileSelectionContestPatchState"/> projection.
/// </summary>
public sealed class PersonnelFileSelectionContestPatchTests
{
    private static PersonnelFileSelectionContestResponse Baseline() =>
        new(
            Guid.NewGuid(),
            "CONTEST-A",
            "Annual Selection",
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            "PASSED",
            "Top candidate.",
            "HRIS",
            "SC-1",
            new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc),
            Guid.NewGuid());

    private static PersonnelFileSelectionContestPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFileSelectionContestPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var state = PersonnelFileSelectionContestPatchState.From(Baseline());

        Assert.Equal("CONTEST-A", state.ContestCode);
        Assert.Equal("PASSED", state.ResultCode);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var input = PersonnelFileSelectionContestPatchState.From(Baseline()).ToInput();

        Assert.Equal("Annual Selection", input.ContestName);
        Assert.Equal("PASSED", input.ResultCode);
    }

    [Fact]
    public void Apply_ReplaceContestName_Mutates()
    {
        var state = PersonnelFileSelectionContestPatchState.From(Baseline());

        Assert.True(PersonnelFileSelectionContestPatchApplier.Apply([Replace("/contestName", "Quarterly Selection")], state).IsSuccess);
        Assert.Equal("Quarterly Selection", state.ContestName);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveNullableNotes_ClearsValue()
    {
        var state = PersonnelFileSelectionContestPatchState.From(Baseline());

        Assert.True(PersonnelFileSelectionContestPatchApplier.Apply([Remove("/notes")], state).IsSuccess);
        Assert.Null(state.Notes);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveRequiredResultCode_FailsValidation()
    {
        var state = PersonnelFileSelectionContestPatchState.From(Baseline());

        Assert.True(PersonnelFileSelectionContestPatchApplier.Apply([Remove("/resultCode")], state).IsSuccess);
        Assert.True(PersonnelFileSelectionContestPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveContestDate_Fails()
    {
        var state = PersonnelFileSelectionContestPatchState.From(Baseline());

        Assert.True(PersonnelFileSelectionContestPatchApplier.Apply([Remove("/contestDateUtc")], state).IsFailure);
    }

    [Fact]
    public void Apply_NonStringForContestCode_Fails()
    {
        var state = PersonnelFileSelectionContestPatchState.From(Baseline());

        Assert.True(PersonnelFileSelectionContestPatchApplier.Apply([Replace("/contestCode", 42)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFileSelectionContestPatchState.From(Baseline());

        Assert.True(PersonnelFileSelectionContestPatchApplier.Apply(
            [new PersonnelFileSelectionContestPatchOperation("copy", "/contestCode", "/contestCode", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFileSelectionContestPatchState.From(Baseline());

        Assert.True(PersonnelFileSelectionContestPatchApplier.Apply([Replace("/unknown", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_Fails()
    {
        var state = PersonnelFileSelectionContestPatchState.From(Baseline());

        Assert.True(PersonnelFileSelectionContestPatchApplier.Apply([Replace("/contestCode/0", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFileSelectionContestPatchState.From(Baseline());

        Assert.True(PersonnelFileSelectionContestPatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_Baseline_Succeeds()
    {
        Assert.True(PersonnelFileSelectionContestPatchApplier.Validate(PersonnelFileSelectionContestPatchState.From(Baseline())).IsSuccess);
    }

    [Fact]
    public void Validate_BlankContestName_Fails()
    {
        var state = PersonnelFileSelectionContestPatchState.From(Baseline());
        state.ContestName = " ";

        Assert.True(PersonnelFileSelectionContestPatchApplier.Validate(state).IsFailure);
    }
}
