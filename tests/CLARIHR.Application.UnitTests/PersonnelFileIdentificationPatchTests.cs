using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical Identification JSON Patch surface (PersonnelFilePersonalInfo
/// remediation): the pure <see cref="PersonnelFileIdentificationPatchApplier"/> apply/validate
/// logic and the <see cref="PersonnelFileIdentificationPatchState"/> projection.
/// </summary>
public sealed class PersonnelFileIdentificationPatchTests
{
    private static PersonnelFileIdentificationResponse Baseline() =>
        new(
            Guid.NewGuid(),
            "DUI",
            "DUI",
            "00000000-0",
            new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2028, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            "RNPN",
            true,
            Guid.NewGuid());

    private static PersonnelFileIdentificationPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFileIdentificationPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var state = PersonnelFileIdentificationPatchState.From(Baseline());

        Assert.Equal("DUI", state.IdentificationTypeCode);
        Assert.Equal("00000000-0", state.IdentificationNumber);
        Assert.Equal("RNPN", state.Issuer);
        Assert.True(state.IsPrimary);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var input = PersonnelFileIdentificationPatchState.From(Baseline()).ToInput();

        Assert.Equal("DUI", input.IdentificationTypeCode);
        Assert.Equal("00000000-0", input.IdentificationNumber);
        Assert.True(input.IsPrimary);
        Assert.Equal(2018, input.IssuedDate!.Value.Year);
    }

    [Fact]
    public void Apply_ReplaceScalarField_Mutates()
    {
        var state = PersonnelFileIdentificationPatchState.From(Baseline());

        Assert.True(PersonnelFileIdentificationPatchApplier.Apply([Replace("/identificationNumber", "11111111-1")], state).IsSuccess);
        Assert.Equal("11111111-1", state.IdentificationNumber);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceExpiryDate_ParsesIsoString()
    {
        var state = PersonnelFileIdentificationPatchState.From(Baseline());

        Assert.True(PersonnelFileIdentificationPatchApplier.Apply([Replace("/expiryDate", "2030-06-30T00:00:00Z")], state).IsSuccess);
        Assert.Equal(2030, state.ExpiryDate!.Value.Year);
    }

    [Fact]
    public void Apply_RemoveOptional_SetsNull()
    {
        var state = PersonnelFileIdentificationPatchState.From(Baseline());

        Assert.True(PersonnelFileIdentificationPatchApplier.Apply([Remove("/issuer")], state).IsSuccess);
        Assert.Null(state.Issuer);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveRequired_FailsValidation()
    {
        var state = PersonnelFileIdentificationPatchState.From(Baseline());

        // Remove clears the value to empty, which the subsequent Validate rejects.
        Assert.True(PersonnelFileIdentificationPatchApplier.Apply([Remove("/identificationNumber")], state).IsSuccess);
        Assert.True(PersonnelFileIdentificationPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveIsPrimary_Fails()
    {
        var state = PersonnelFileIdentificationPatchState.From(Baseline());

        Assert.True(PersonnelFileIdentificationPatchApplier.Apply([Remove("/isPrimary")], state).IsFailure);
    }

    [Fact]
    public void Apply_NonStringForCode_Fails()
    {
        var state = PersonnelFileIdentificationPatchState.From(Baseline());

        Assert.True(PersonnelFileIdentificationPatchApplier.Apply([Replace("/identificationTypeCode", 42)], state).IsFailure);
    }

    [Fact]
    public void Apply_NonBooleanForIsPrimary_Fails()
    {
        var state = PersonnelFileIdentificationPatchState.From(Baseline());

        Assert.True(PersonnelFileIdentificationPatchApplier.Apply([Replace("/isPrimary", "yes")], state).IsFailure);
    }

    [Fact]
    public void Apply_InvalidDate_Fails()
    {
        var state = PersonnelFileIdentificationPatchState.From(Baseline());

        Assert.True(PersonnelFileIdentificationPatchApplier.Apply([Replace("/issuedDate", "not-a-date")], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFileIdentificationPatchState.From(Baseline());

        Assert.True(PersonnelFileIdentificationPatchApplier.Apply(
            [new PersonnelFileIdentificationPatchOperation("copy", "/issuer", "/issuer", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFileIdentificationPatchState.From(Baseline());

        Assert.True(PersonnelFileIdentificationPatchApplier.Apply([Replace("/notARealField", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_Fails()
    {
        var state = PersonnelFileIdentificationPatchState.From(Baseline());

        Assert.True(PersonnelFileIdentificationPatchApplier.Apply([Replace("/issuer/0", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFileIdentificationPatchState.From(Baseline());

        Assert.True(PersonnelFileIdentificationPatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_Baseline_Succeeds()
    {
        Assert.True(PersonnelFileIdentificationPatchApplier.Validate(PersonnelFileIdentificationPatchState.From(Baseline())).IsSuccess);
    }

    [Fact]
    public void Validate_BlankTypeCode_Fails()
    {
        var state = PersonnelFileIdentificationPatchState.From(Baseline());
        state.IdentificationTypeCode = "  ";

        Assert.True(PersonnelFileIdentificationPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_ExpiryBeforeIssued_Fails()
    {
        var state = PersonnelFileIdentificationPatchState.From(Baseline());
        state.ExpiryDate = state.IssuedDate!.Value.AddYears(-1);

        Assert.True(PersonnelFileIdentificationPatchApplier.Validate(state).IsFailure);
    }
}
