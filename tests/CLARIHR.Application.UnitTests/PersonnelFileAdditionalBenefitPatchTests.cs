using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical additional-benefit JSON Patch surface: the pure
/// <see cref="PersonnelFileAdditionalBenefitPatchApplier"/> and the
/// <see cref="PersonnelFileAdditionalBenefitPatchState"/> projection. Like the salary item, the
/// additional benefit's <c>isActive</c> flag is patchable (replacing the former dedicated
/// <c>/deactivate</c> endpoint), so the applier must accept boolean values and flag the mutation
/// while preserving the business-field validation the Add/Update commands run.
/// </summary>
public sealed class PersonnelFileAdditionalBenefitPatchTests
{
    private static PersonnelFileAdditionalBenefitResponse Baseline() =>
        new(
            Guid.NewGuid(),
            "MEAL",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            true,
            "Daily meal allowance.",
            Guid.NewGuid());

    private static PersonnelFileAdditionalBenefitPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFileAdditionalBenefitPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var state = PersonnelFileAdditionalBenefitPatchState.From(Baseline());

        Assert.Equal("MEAL", state.BenefitTypeCode);
        Assert.Equal("Daily meal allowance.", state.Notes);
        Assert.True(state.IsActive);
        Assert.False(state.IsActiveMutated);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var input = PersonnelFileAdditionalBenefitPatchState.From(Baseline()).ToInput();

        Assert.Equal("MEAL", input.BenefitTypeCode);
        Assert.Equal("Daily meal allowance.", input.Notes);
        Assert.True(input.IsActive);
    }

    [Fact]
    public void Apply_ReplaceBenefitTypeCode_Mutates()
    {
        var state = PersonnelFileAdditionalBenefitPatchState.From(Baseline());

        Assert.True(PersonnelFileAdditionalBenefitPatchApplier.Apply([Replace("/benefitTypeCode", "TRANSPORT")], state).IsSuccess);
        Assert.Equal("TRANSPORT", state.BenefitTypeCode);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceIsActiveFalse_MutatesAndFlagsActiveChange()
    {
        var state = PersonnelFileAdditionalBenefitPatchState.From(Baseline());

        Assert.True(PersonnelFileAdditionalBenefitPatchApplier.Apply([Replace("/isActive", false)], state).IsSuccess);
        Assert.False(state.IsActive);
        Assert.True(state.IsActiveMutated);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceIsActiveTrue_MutatesAndFlagsActiveChange()
    {
        var state = PersonnelFileAdditionalBenefitPatchState.From(Baseline() with { IsActive = false });

        Assert.True(PersonnelFileAdditionalBenefitPatchApplier.Apply([Replace("/isActive", true)], state).IsSuccess);
        Assert.True(state.IsActive);
        Assert.True(state.IsActiveMutated);
    }

    [Fact]
    public void Apply_BusinessFieldOnly_DoesNotFlagActiveChange()
    {
        var state = PersonnelFileAdditionalBenefitPatchState.From(Baseline());

        Assert.True(PersonnelFileAdditionalBenefitPatchApplier.Apply([Replace("/benefitTypeCode", "GYM")], state).IsSuccess);
        Assert.False(state.IsActiveMutated);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_NonBooleanForIsActive_Fails()
    {
        var state = PersonnelFileAdditionalBenefitPatchState.From(Baseline());

        Assert.True(PersonnelFileAdditionalBenefitPatchApplier.Apply([Replace("/isActive", "yes")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveIsActive_Fails()
    {
        var state = PersonnelFileAdditionalBenefitPatchState.From(Baseline());

        Assert.True(PersonnelFileAdditionalBenefitPatchApplier.Apply([Remove("/isActive")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveEndDate_ClearsValue()
    {
        var state = PersonnelFileAdditionalBenefitPatchState.From(Baseline());

        Assert.True(PersonnelFileAdditionalBenefitPatchApplier.Apply([Remove("/endDate")], state).IsSuccess);
        Assert.Null(state.EndDate);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveStartDate_ClearsValue()
    {
        var state = PersonnelFileAdditionalBenefitPatchState.From(Baseline());

        Assert.True(PersonnelFileAdditionalBenefitPatchApplier.Apply([Remove("/startDate")], state).IsSuccess);
        Assert.Null(state.StartDate);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveOptionalNotes_ClearsValue()
    {
        var state = PersonnelFileAdditionalBenefitPatchState.From(Baseline());

        Assert.True(PersonnelFileAdditionalBenefitPatchApplier.Apply([Remove("/notes")], state).IsSuccess);
        Assert.Null(state.Notes);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveRequiredBenefitTypeCode_FailsValidation()
    {
        var state = PersonnelFileAdditionalBenefitPatchState.From(Baseline());

        Assert.True(PersonnelFileAdditionalBenefitPatchApplier.Apply([Remove("/benefitTypeCode")], state).IsSuccess);
        Assert.True(PersonnelFileAdditionalBenefitPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_NonStringForBenefitTypeCode_Fails()
    {
        var state = PersonnelFileAdditionalBenefitPatchState.From(Baseline());

        Assert.True(PersonnelFileAdditionalBenefitPatchApplier.Apply([Replace("/benefitTypeCode", 42)], state).IsFailure);
    }

    [Fact]
    public void Apply_NonDateForStartDate_Fails()
    {
        var state = PersonnelFileAdditionalBenefitPatchState.From(Baseline());

        Assert.True(PersonnelFileAdditionalBenefitPatchApplier.Apply([Replace("/startDate", "not-a-date")], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFileAdditionalBenefitPatchState.From(Baseline());

        Assert.True(PersonnelFileAdditionalBenefitPatchApplier.Apply(
            [new PersonnelFileAdditionalBenefitPatchOperation("copy", "/benefitTypeCode", "/benefitTypeCode", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFileAdditionalBenefitPatchState.From(Baseline());

        Assert.True(PersonnelFileAdditionalBenefitPatchApplier.Apply([Replace("/unknown", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_Fails()
    {
        var state = PersonnelFileAdditionalBenefitPatchState.From(Baseline());

        Assert.True(PersonnelFileAdditionalBenefitPatchApplier.Apply([Replace("/benefitTypeCode/0", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFileAdditionalBenefitPatchState.From(Baseline());

        Assert.True(PersonnelFileAdditionalBenefitPatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_Baseline_Succeeds()
    {
        Assert.True(PersonnelFileAdditionalBenefitPatchApplier.Validate(PersonnelFileAdditionalBenefitPatchState.From(Baseline())).IsSuccess);
    }

    [Fact]
    public void Validate_BlankBenefitTypeCode_Fails()
    {
        var state = PersonnelFileAdditionalBenefitPatchState.From(Baseline());
        state.BenefitTypeCode = " ";

        Assert.True(PersonnelFileAdditionalBenefitPatchApplier.Validate(state).IsFailure);
    }
}
