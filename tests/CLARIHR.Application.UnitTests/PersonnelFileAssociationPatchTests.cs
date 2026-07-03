using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical Association JSON Patch surface (PersonnelFileInterests
/// remediation): the pure <see cref="PersonnelFileAssociationPatchApplier"/> and the
/// <see cref="PersonnelFileAssociationPatchState"/> projection.
/// </summary>
public sealed class PersonnelFileAssociationPatchTests
{
    private static PersonnelFileAssociationResponse Baseline() =>
        new(
            Guid.NewGuid(),
            "COLEGIO_PROF",
            "Bar Association",
            "Member",
            new DateTime(2020, 1, 1),
            new DateTime(2022, 6, 1),
            120.50m,
            Guid.NewGuid());

    private static PersonnelFileAssociationPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFileAssociationPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var state = PersonnelFileAssociationPatchState.From(Baseline());

        Assert.Equal("Bar Association", state.AssociationName);
        Assert.Equal("Member", state.Role);
        Assert.Equal(new DateTime(2020, 1, 1), state.JoinedDate);
        Assert.Equal(new DateTime(2022, 6, 1), state.LeftDate);
        Assert.Equal(120.50m, state.Payment);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var input = PersonnelFileAssociationPatchState.From(Baseline()).ToInput();

        Assert.Equal("Bar Association", input.AssociationName);
        Assert.Equal("Member", input.Role);
        Assert.Equal(120.50m, input.Payment);
    }

    [Fact]
    public void Apply_ReplaceAssociationName_Mutates()
    {
        var state = PersonnelFileAssociationPatchState.From(Baseline());

        Assert.True(PersonnelFileAssociationPatchApplier.Apply([Replace("/associationName", "Engineers Guild")], state).IsSuccess);
        Assert.Equal("Engineers Guild", state.AssociationName);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplacePayment_Mutates()
    {
        var state = PersonnelFileAssociationPatchState.From(Baseline());

        Assert.True(PersonnelFileAssociationPatchApplier.Apply([Replace("/payment", 99.99m)], state).IsSuccess);
        Assert.Equal(99.99m, state.Payment);
    }

    [Fact]
    public void Apply_RemoveOptionalRole_SetsNull()
    {
        var state = PersonnelFileAssociationPatchState.From(Baseline());

        Assert.True(PersonnelFileAssociationPatchApplier.Apply([Remove("/role")], state).IsSuccess);
        Assert.Null(state.Role);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveOptionalDates_SetNull()
    {
        var state = PersonnelFileAssociationPatchState.From(Baseline());

        Assert.True(PersonnelFileAssociationPatchApplier.Apply([Remove("/joinedDate"), Remove("/leftDate")], state).IsSuccess);
        Assert.Null(state.JoinedDate);
        Assert.Null(state.LeftDate);
    }

    [Fact]
    public void Apply_RemoveRequiredAssociationName_FailsValidation()
    {
        var state = PersonnelFileAssociationPatchState.From(Baseline());

        Assert.True(PersonnelFileAssociationPatchApplier.Apply([Remove("/associationName")], state).IsSuccess);
        Assert.True(PersonnelFileAssociationPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_NonNumberForPayment_Fails()
    {
        var state = PersonnelFileAssociationPatchState.From(Baseline());

        Assert.True(PersonnelFileAssociationPatchApplier.Apply([Replace("/payment", "abc")], state).IsFailure);
    }

    [Fact]
    public void Apply_InvalidDate_Fails()
    {
        var state = PersonnelFileAssociationPatchState.From(Baseline());

        Assert.True(PersonnelFileAssociationPatchApplier.Apply([Replace("/joinedDate", "not-a-date")], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFileAssociationPatchState.From(Baseline());

        Assert.True(PersonnelFileAssociationPatchApplier.Apply(
            [new PersonnelFileAssociationPatchOperation("copy", "/role", "/associationName", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFileAssociationPatchState.From(Baseline());

        Assert.True(PersonnelFileAssociationPatchApplier.Apply([Replace("/tier", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFileAssociationPatchState.From(Baseline());

        Assert.True(PersonnelFileAssociationPatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_Baseline_Succeeds()
    {
        Assert.True(PersonnelFileAssociationPatchApplier.Validate(PersonnelFileAssociationPatchState.From(Baseline())).IsSuccess);
    }

    [Fact]
    public void Validate_BlankAssociationName_Fails()
    {
        var state = PersonnelFileAssociationPatchState.From(Baseline());
        state.AssociationName = " ";

        Assert.True(PersonnelFileAssociationPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_NegativePayment_Fails()
    {
        var state = PersonnelFileAssociationPatchState.From(Baseline());
        state.Payment = -1m;

        Assert.True(PersonnelFileAssociationPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_LeftDateBeforeJoinedDate_Fails()
    {
        var state = PersonnelFileAssociationPatchState.From(Baseline());
        state.JoinedDate = new DateTime(2022, 1, 1);
        state.LeftDate = new DateTime(2020, 1, 1);

        Assert.True(PersonnelFileAssociationPatchApplier.Validate(state).IsFailure);
    }
}
