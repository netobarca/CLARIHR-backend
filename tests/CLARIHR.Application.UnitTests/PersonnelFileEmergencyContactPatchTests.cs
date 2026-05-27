using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical Emergency Contact JSON Patch surface
/// (PersonnelFilePersonalInfo remediation): the pure
/// <see cref="PersonnelFileEmergencyContactPatchApplier"/> apply/validate logic and the
/// <see cref="PersonnelFileEmergencyContactPatchState"/> projection.
/// </summary>
public sealed class PersonnelFileEmergencyContactPatchTests
{
    private static PersonnelFileEmergencyContactResponse Baseline() =>
        new(
            Guid.NewGuid(),
            "Maria Lopez",
            "Madre",
            "+50370000001",
            "Colonia Escalon",
            "Hospital General",
            Guid.NewGuid());

    private static PersonnelFileEmergencyContactPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFileEmergencyContactPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var state = PersonnelFileEmergencyContactPatchState.From(Baseline());

        Assert.Equal("Maria Lopez", state.Name);
        Assert.Equal("Madre", state.Relationship);
        Assert.Equal("+50370000001", state.Phone);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var input = PersonnelFileEmergencyContactPatchState.From(Baseline()).ToInput();

        Assert.Equal("Maria Lopez", input.Name);
        Assert.Equal("Madre", input.Relationship);
        Assert.Equal("Hospital General", input.Workplace);
    }

    [Fact]
    public void Apply_ReplaceName_Mutates()
    {
        var state = PersonnelFileEmergencyContactPatchState.From(Baseline());

        Assert.True(PersonnelFileEmergencyContactPatchApplier.Apply([Replace("/name", "Ana Perez")], state).IsSuccess);
        Assert.Equal("Ana Perez", state.Name);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveOptional_SetsNull()
    {
        var state = PersonnelFileEmergencyContactPatchState.From(Baseline());

        Assert.True(PersonnelFileEmergencyContactPatchApplier.Apply([Remove("/workplace")], state).IsSuccess);
        Assert.Null(state.Workplace);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveRequiredName_FailsValidation()
    {
        var state = PersonnelFileEmergencyContactPatchState.From(Baseline());

        // Remove clears the value to empty, which the subsequent Validate rejects.
        Assert.True(PersonnelFileEmergencyContactPatchApplier.Apply([Remove("/name")], state).IsSuccess);
        Assert.True(PersonnelFileEmergencyContactPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveRequiredPhone_FailsValidation()
    {
        var state = PersonnelFileEmergencyContactPatchState.From(Baseline());

        Assert.True(PersonnelFileEmergencyContactPatchApplier.Apply([Remove("/phone")], state).IsSuccess);
        Assert.True(PersonnelFileEmergencyContactPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_NonStringForName_Fails()
    {
        var state = PersonnelFileEmergencyContactPatchState.From(Baseline());

        Assert.True(PersonnelFileEmergencyContactPatchApplier.Apply([Replace("/name", 42)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFileEmergencyContactPatchState.From(Baseline());

        Assert.True(PersonnelFileEmergencyContactPatchApplier.Apply(
            [new PersonnelFileEmergencyContactPatchOperation("copy", "/name", "/relationship", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFileEmergencyContactPatchState.From(Baseline());

        Assert.True(PersonnelFileEmergencyContactPatchApplier.Apply([Replace("/notARealField", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_Fails()
    {
        var state = PersonnelFileEmergencyContactPatchState.From(Baseline());

        Assert.True(PersonnelFileEmergencyContactPatchApplier.Apply([Replace("/name/0", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFileEmergencyContactPatchState.From(Baseline());

        Assert.True(PersonnelFileEmergencyContactPatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_Baseline_Succeeds()
    {
        Assert.True(PersonnelFileEmergencyContactPatchApplier.Validate(PersonnelFileEmergencyContactPatchState.From(Baseline())).IsSuccess);
    }

    [Fact]
    public void Validate_BlankRelationship_Fails()
    {
        var state = PersonnelFileEmergencyContactPatchState.From(Baseline());
        state.Relationship = "  ";

        Assert.True(PersonnelFileEmergencyContactPatchApplier.Validate(state).IsFailure);
    }
}
