using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical Hobby JSON Patch surface (PersonnelFileInterests
/// remediation): the pure <see cref="PersonnelFileHobbyPatchApplier"/> and the
/// <see cref="PersonnelFileHobbyPatchState"/> projection. hobbyCode is the required,
/// catalog-backed field (RF-005, DP-07); hobbyName is an optional free-text label.
/// </summary>
public sealed class PersonnelFileHobbyPatchTests
{
    private static PersonnelFileHobbyResponse Baseline() =>
        new(Guid.NewGuid(), "LECTURA", "Reading", Guid.NewGuid());

    private static PersonnelFileHobbyPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFileHobbyPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var state = PersonnelFileHobbyPatchState.From(Baseline());

        Assert.Equal("LECTURA", state.HobbyCode);
        Assert.Equal("Reading", state.HobbyName);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var input = PersonnelFileHobbyPatchState.From(Baseline()).ToInput();

        Assert.Equal("LECTURA", input.HobbyCode);
        Assert.Equal("Reading", input.HobbyName);
    }

    [Fact]
    public void Apply_ReplaceHobbyCode_Mutates()
    {
        var state = PersonnelFileHobbyPatchState.From(Baseline());

        Assert.True(PersonnelFileHobbyPatchApplier.Apply([Replace("/hobbyCode", "DEPORTE")], state).IsSuccess);
        Assert.Equal("DEPORTE", state.HobbyCode);
        Assert.True(state.HasMutation);
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
    public void Apply_RemoveRequiredCode_FailsValidate()
    {
        var state = PersonnelFileHobbyPatchState.From(Baseline());

        // Remove clears the value to empty, which the subsequent Validate rejects.
        Assert.True(PersonnelFileHobbyPatchApplier.Apply([Remove("/hobbyCode")], state).IsSuccess);
        Assert.True(PersonnelFileHobbyPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveOptionalName_ValidateSucceeds()
    {
        var state = PersonnelFileHobbyPatchState.From(Baseline());

        Assert.True(PersonnelFileHobbyPatchApplier.Apply([Remove("/hobbyName")], state).IsSuccess);
        Assert.Null(state.HobbyName);
        Assert.True(PersonnelFileHobbyPatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_NonStringForHobbyCode_Fails()
    {
        var state = PersonnelFileHobbyPatchState.From(Baseline());

        Assert.True(PersonnelFileHobbyPatchApplier.Apply([Replace("/hobbyCode", 42)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFileHobbyPatchState.From(Baseline());

        Assert.True(PersonnelFileHobbyPatchApplier.Apply(
            [new PersonnelFileHobbyPatchOperation("copy", "/hobbyCode", "/hobbyCode", null)], state).IsFailure);
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

        Assert.True(PersonnelFileHobbyPatchApplier.Apply([Replace("/hobbyCode/0", "x")], state).IsFailure);
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
    public void Validate_BlankHobbyCode_Fails()
    {
        var state = PersonnelFileHobbyPatchState.From(Baseline());
        state.HobbyCode = " ";

        Assert.True(PersonnelFileHobbyPatchApplier.Validate(state).IsFailure);
    }
}
