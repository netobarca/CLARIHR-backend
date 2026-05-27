using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical asset/access JSON Patch surface: the pure
/// <see cref="PersonnelFileAssetAccessPatchApplier"/> and the
/// <see cref="PersonnelFileAssetAccessPatchState"/> projection. The asset/access's <c>isActive</c>
/// flag is patchable (replacing the former dedicated <c>/deactivate</c> endpoint), so the applier
/// must accept boolean values and flag the mutation while preserving the business-field validation
/// the Add/Update commands run.
/// </summary>
public sealed class PersonnelFileAssetAccessPatchTests
{
    private static PersonnelFileAssetAccessResponse Baseline() =>
        new(
            Guid.NewGuid(),
            "LAPTOP",
            "Dell Latitude 7440",
            "FULL",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            "DELIVERED",
            true,
            "Assigned for remote work.",
            Guid.NewGuid());

    private static PersonnelFileAssetAccessPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFileAssetAccessPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var state = PersonnelFileAssetAccessPatchState.From(Baseline());

        Assert.Equal("LAPTOP", state.AssetTypeCode);
        Assert.Equal("Dell Latitude 7440", state.AssetOrAccessName);
        Assert.Equal("FULL", state.AccessLevelCode);
        Assert.Equal("DELIVERED", state.DeliveryStatusCode);
        Assert.Equal("Assigned for remote work.", state.Notes);
        Assert.True(state.IsActive);
        Assert.False(state.IsActiveMutated);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var input = PersonnelFileAssetAccessPatchState.From(Baseline()).ToInput();

        Assert.Equal("LAPTOP", input.AssetTypeCode);
        Assert.Equal("Dell Latitude 7440", input.AssetOrAccessName);
        Assert.Equal("FULL", input.AccessLevelCode);
        Assert.Equal("Assigned for remote work.", input.Notes);
        Assert.True(input.IsActive);
    }

    [Fact]
    public void Apply_ReplaceAssetTypeCode_Mutates()
    {
        var state = PersonnelFileAssetAccessPatchState.From(Baseline());

        Assert.True(PersonnelFileAssetAccessPatchApplier.Apply([Replace("/assetTypeCode", "PHONE")], state).IsSuccess);
        Assert.Equal("PHONE", state.AssetTypeCode);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceAssetOrAccessName_Mutates()
    {
        var state = PersonnelFileAssetAccessPatchState.From(Baseline());

        Assert.True(PersonnelFileAssetAccessPatchApplier.Apply([Replace("/assetOrAccessName", "iPhone 15")], state).IsSuccess);
        Assert.Equal("iPhone 15", state.AssetOrAccessName);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceIsActiveFalse_MutatesAndFlagsActiveChange()
    {
        var state = PersonnelFileAssetAccessPatchState.From(Baseline());

        Assert.True(PersonnelFileAssetAccessPatchApplier.Apply([Replace("/isActive", false)], state).IsSuccess);
        Assert.False(state.IsActive);
        Assert.True(state.IsActiveMutated);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceIsActiveTrue_MutatesAndFlagsActiveChange()
    {
        var state = PersonnelFileAssetAccessPatchState.From(Baseline() with { IsActive = false });

        Assert.True(PersonnelFileAssetAccessPatchApplier.Apply([Replace("/isActive", true)], state).IsSuccess);
        Assert.True(state.IsActive);
        Assert.True(state.IsActiveMutated);
    }

    [Fact]
    public void Apply_BusinessFieldOnly_DoesNotFlagActiveChange()
    {
        var state = PersonnelFileAssetAccessPatchState.From(Baseline());

        Assert.True(PersonnelFileAssetAccessPatchApplier.Apply([Replace("/assetTypeCode", "PHONE")], state).IsSuccess);
        Assert.False(state.IsActiveMutated);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_NonBooleanForIsActive_Fails()
    {
        var state = PersonnelFileAssetAccessPatchState.From(Baseline());

        Assert.True(PersonnelFileAssetAccessPatchApplier.Apply([Replace("/isActive", "yes")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveIsActive_Fails()
    {
        var state = PersonnelFileAssetAccessPatchState.From(Baseline());

        Assert.True(PersonnelFileAssetAccessPatchApplier.Apply([Remove("/isActive")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveEndDateUtc_ClearsValue()
    {
        var state = PersonnelFileAssetAccessPatchState.From(Baseline());

        Assert.True(PersonnelFileAssetAccessPatchApplier.Apply([Remove("/endDateUtc")], state).IsSuccess);
        Assert.Null(state.EndDateUtc);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveOptionalAccessLevelCode_ClearsValue()
    {
        var state = PersonnelFileAssetAccessPatchState.From(Baseline());

        Assert.True(PersonnelFileAssetAccessPatchApplier.Apply([Remove("/accessLevelCode")], state).IsSuccess);
        Assert.Null(state.AccessLevelCode);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveOptionalNotes_ClearsValue()
    {
        var state = PersonnelFileAssetAccessPatchState.From(Baseline());

        Assert.True(PersonnelFileAssetAccessPatchApplier.Apply([Remove("/notes")], state).IsSuccess);
        Assert.Null(state.Notes);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveRequiredAssetTypeCode_FailsValidation()
    {
        var state = PersonnelFileAssetAccessPatchState.From(Baseline());

        Assert.True(PersonnelFileAssetAccessPatchApplier.Apply([Remove("/assetTypeCode")], state).IsSuccess);
        Assert.True(PersonnelFileAssetAccessPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveRequiredAssetOrAccessName_FailsValidation()
    {
        var state = PersonnelFileAssetAccessPatchState.From(Baseline());

        Assert.True(PersonnelFileAssetAccessPatchApplier.Apply([Remove("/assetOrAccessName")], state).IsSuccess);
        Assert.True(PersonnelFileAssetAccessPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveRequiredStartDateUtc_Fails()
    {
        var state = PersonnelFileAssetAccessPatchState.From(Baseline());

        Assert.True(PersonnelFileAssetAccessPatchApplier.Apply([Remove("/startDateUtc")], state).IsFailure);
    }

    [Fact]
    public void Apply_NonStringForAssetTypeCode_Fails()
    {
        var state = PersonnelFileAssetAccessPatchState.From(Baseline());

        Assert.True(PersonnelFileAssetAccessPatchApplier.Apply([Replace("/assetTypeCode", 42)], state).IsFailure);
    }

    [Fact]
    public void Apply_NonDateForStartDateUtc_Fails()
    {
        var state = PersonnelFileAssetAccessPatchState.From(Baseline());

        Assert.True(PersonnelFileAssetAccessPatchApplier.Apply([Replace("/startDateUtc", "not-a-date")], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFileAssetAccessPatchState.From(Baseline());

        Assert.True(PersonnelFileAssetAccessPatchApplier.Apply(
            [new PersonnelFileAssetAccessPatchOperation("copy", "/assetTypeCode", "/assetTypeCode", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFileAssetAccessPatchState.From(Baseline());

        Assert.True(PersonnelFileAssetAccessPatchApplier.Apply([Replace("/unknown", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_Fails()
    {
        var state = PersonnelFileAssetAccessPatchState.From(Baseline());

        Assert.True(PersonnelFileAssetAccessPatchApplier.Apply([Replace("/assetTypeCode/0", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFileAssetAccessPatchState.From(Baseline());

        Assert.True(PersonnelFileAssetAccessPatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_Baseline_Succeeds()
    {
        Assert.True(PersonnelFileAssetAccessPatchApplier.Validate(PersonnelFileAssetAccessPatchState.From(Baseline())).IsSuccess);
    }

    [Fact]
    public void Validate_BlankAssetTypeCode_Fails()
    {
        var state = PersonnelFileAssetAccessPatchState.From(Baseline());
        state.AssetTypeCode = " ";

        Assert.True(PersonnelFileAssetAccessPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_BlankAssetOrAccessName_Fails()
    {
        var state = PersonnelFileAssetAccessPatchState.From(Baseline());
        state.AssetOrAccessName = " ";

        Assert.True(PersonnelFileAssetAccessPatchApplier.Validate(state).IsFailure);
    }
}
