using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical Hobby JSON Patch surface (PersonnelFileInterests
/// remediation): the pure <see cref="PersonnelFileHobbyPatchApplier"/> and the
/// <see cref="PersonnelFileHobbyPatchState"/> projection.
/// </summary>
public sealed class PersonnelFileHobbyPatchTests
{
    private static PersonnelFileHobbyResponse Baseline() =>
        new(Guid.NewGuid(), "Reading", Guid.NewGuid());

    private static PersonnelFileHobbyPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFileHobbyPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var state = PersonnelFileHobbyPatchState.From(Baseline());

        Assert.Equal("Reading", state.HobbyName);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var input = PersonnelFileHobbyPatchState.From(Baseline()).ToInput();

        Assert.Equal("Reading", input.HobbyName);
    }

    [Fact]
    public void Apply_ReplaceHobbyName_Mutates()
    {
        var state = PersonnelFileHobbyPatchState.From(Baseline());

        Assert.True(PersonnelFileHobbyPatchApplier.Apply([Replace("/hobbyName", "Chess")], state).IsSuccess);
        Assert.Equal("Chess", state.HobbyName);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveRequired_Fails()
    {
        var state = PersonnelFileHobbyPatchState.From(Baseline());

        // Remove clears the value to empty, which the subsequent Validate rejects.
        Assert.True(PersonnelFileHobbyPatchApplier.Apply([Remove("/hobbyName")], state).IsSuccess);
        Assert.True(PersonnelFileHobbyPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_NonStringForHobbyName_Fails()
    {
        var state = PersonnelFileHobbyPatchState.From(Baseline());

        Assert.True(PersonnelFileHobbyPatchApplier.Apply([Replace("/hobbyName", 42)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFileHobbyPatchState.From(Baseline());

        Assert.True(PersonnelFileHobbyPatchApplier.Apply(
            [new PersonnelFileHobbyPatchOperation("copy", "/hobbyName", "/hobbyName", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFileHobbyPatchState.From(Baseline());

        Assert.True(PersonnelFileHobbyPatchApplier.Apply([Replace("/category", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_Fails()
    {
        var state = PersonnelFileHobbyPatchState.From(Baseline());

        Assert.True(PersonnelFileHobbyPatchApplier.Apply([Replace("/hobbyName/0", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFileHobbyPatchState.From(Baseline());

        Assert.True(PersonnelFileHobbyPatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_Baseline_Succeeds()
    {
        Assert.True(PersonnelFileHobbyPatchApplier.Validate(PersonnelFileHobbyPatchState.From(Baseline())).IsSuccess);
    }

    [Fact]
    public void Validate_BlankHobbyName_Fails()
    {
        var state = PersonnelFileHobbyPatchState.From(Baseline());
        state.HobbyName = " ";

        Assert.True(PersonnelFileHobbyPatchApplier.Validate(state).IsFailure);
    }
}
