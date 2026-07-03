using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical Address JSON Patch surface (PersonnelFilePersonalInfo
/// remediation): the pure <see cref="PersonnelFileAddressPatchApplier"/> apply/validate logic
/// and the <see cref="PersonnelFileAddressPatchState"/> projection.
/// </summary>
public sealed class PersonnelFileAddressPatchTests
{
    private static PersonnelFileAddressResponse Baseline() =>
        new(
            Guid.NewGuid(),
            "Colonia Escalon",
            "CASA",
            "SV",
            "SAN_SALVADOR",
            "SAN_SALVADOR_CENTRO",
            "1101",
            true,
            Guid.NewGuid());

    private static PersonnelFileAddressPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFileAddressPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var state = PersonnelFileAddressPatchState.From(Baseline());

        Assert.Equal("Colonia Escalon", state.AddressLine);
        Assert.Equal("SV", state.Country);
        Assert.True(state.IsCurrent);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var input = PersonnelFileAddressPatchState.From(Baseline()).ToInput();

        Assert.Equal("Colonia Escalon", input.AddressLine);
        Assert.Equal("1101", input.PostalCode);
        Assert.True(input.IsCurrent);
    }

    [Fact]
    public void Apply_ReplaceAddressLine_Mutates()
    {
        var state = PersonnelFileAddressPatchState.From(Baseline());

        Assert.True(PersonnelFileAddressPatchApplier.Apply([Replace("/addressLine", "Boulevard Los Heroes")], state).IsSuccess);
        Assert.Equal("Boulevard Los Heroes", state.AddressLine);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceIsCurrent_Mutates()
    {
        var state = PersonnelFileAddressPatchState.From(Baseline());

        Assert.True(PersonnelFileAddressPatchApplier.Apply([Replace("/isCurrent", false)], state).IsSuccess);
        Assert.False(state.IsCurrent);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveOptional_SetsNull()
    {
        var state = PersonnelFileAddressPatchState.From(Baseline());

        Assert.True(PersonnelFileAddressPatchApplier.Apply([Remove("/postalCode")], state).IsSuccess);
        Assert.Null(state.PostalCode);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveRequired_FailsValidation()
    {
        var state = PersonnelFileAddressPatchState.From(Baseline());

        // Remove clears the value to empty, which the subsequent Validate rejects.
        Assert.True(PersonnelFileAddressPatchApplier.Apply([Remove("/addressLine")], state).IsSuccess);
        Assert.True(PersonnelFileAddressPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveIsCurrent_Fails()
    {
        var state = PersonnelFileAddressPatchState.From(Baseline());

        Assert.True(PersonnelFileAddressPatchApplier.Apply([Remove("/isCurrent")], state).IsFailure);
    }

    [Fact]
    public void Apply_NonStringForAddressLine_Fails()
    {
        var state = PersonnelFileAddressPatchState.From(Baseline());

        Assert.True(PersonnelFileAddressPatchApplier.Apply([Replace("/addressLine", 42)], state).IsFailure);
    }

    [Fact]
    public void Apply_NonBooleanForIsCurrent_Fails()
    {
        var state = PersonnelFileAddressPatchState.From(Baseline());

        Assert.True(PersonnelFileAddressPatchApplier.Apply([Replace("/isCurrent", "yes")], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFileAddressPatchState.From(Baseline());

        Assert.True(PersonnelFileAddressPatchApplier.Apply(
            [new PersonnelFileAddressPatchOperation("move", "/addressLine", "/country", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFileAddressPatchState.From(Baseline());

        Assert.True(PersonnelFileAddressPatchApplier.Apply([Replace("/notARealField", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_Fails()
    {
        var state = PersonnelFileAddressPatchState.From(Baseline());

        Assert.True(PersonnelFileAddressPatchApplier.Apply([Replace("/addressLine/0", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFileAddressPatchState.From(Baseline());

        Assert.True(PersonnelFileAddressPatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_Baseline_Succeeds()
    {
        Assert.True(PersonnelFileAddressPatchApplier.Validate(PersonnelFileAddressPatchState.From(Baseline())).IsSuccess);
    }

    [Fact]
    public void Validate_BlankAddressLine_Fails()
    {
        var state = PersonnelFileAddressPatchState.From(Baseline());
        state.AddressLine = "  ";

        Assert.True(PersonnelFileAddressPatchApplier.Validate(state).IsFailure);
    }
}
