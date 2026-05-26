using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical Training JSON Patch surface (PersonnelFileBackground
/// remediation): the pure <see cref="PersonnelFileTrainingPatchApplier"/> and the
/// <see cref="PersonnelFileTrainingPatchState"/> projection.
/// </summary>
public sealed class PersonnelFileTrainingPatchTests
{
    private static PersonnelFileTrainingResponse Baseline() =>
        new(
            Guid.NewGuid(),
            "Leadership 101",
            "INTERNAL_TRAINING",
            "A leadership course.",
            "Leadership",
            "Acme Academy",
            "Jane Doe",
            85m,
            new DateTime(2023, 1, 10),
            new DateTime(2023, 2, 10),
            true,
            true,
            "SV",
            40m,
            "HOURS",
            120m,
            "USD",
            Guid.NewGuid());

    private static PersonnelFileTrainingPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFileTrainingPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var state = PersonnelFileTrainingPatchState.From(Baseline());

        Assert.Equal("Leadership 101", state.TrainingName);
        Assert.Equal("INTERNAL_TRAINING", state.TrainingTypeCode);
        Assert.Equal("A leadership course.", state.Description);
        Assert.Equal(85m, state.Score);
        Assert.Equal(new DateTime(2023, 1, 10), state.StartDate);
        Assert.Equal(new DateTime(2023, 2, 10), state.EndDate);
        Assert.True(state.IsInternal);
        Assert.True(state.IsLocal);
        Assert.Equal("SV", state.CountryCode);
        Assert.Equal(40m, state.DurationValue);
        Assert.Equal("HOURS", state.DurationUnitCode);
        Assert.Equal(120m, state.CostAmount);
        Assert.Equal("USD", state.CostCurrencyCode);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var input = PersonnelFileTrainingPatchState.From(Baseline()).ToInput();

        Assert.Equal("Leadership 101", input.TrainingName);
        Assert.Equal("INTERNAL_TRAINING", input.TrainingTypeCode);
        Assert.Equal(40m, input.DurationValue);
        Assert.Equal("USD", input.CostCurrencyCode);
    }

    [Fact]
    public void Apply_ReplaceTrainingName_Mutates()
    {
        var state = PersonnelFileTrainingPatchState.From(Baseline());

        Assert.True(PersonnelFileTrainingPatchApplier.Apply([Replace("/trainingName", "Advanced Leadership")], state).IsSuccess);
        Assert.Equal("Advanced Leadership", state.TrainingName);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceDurationValue_Mutates()
    {
        var state = PersonnelFileTrainingPatchState.From(Baseline());

        Assert.True(PersonnelFileTrainingPatchApplier.Apply([Replace("/durationValue", 60)], state).IsSuccess);
        Assert.Equal(60m, state.DurationValue);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceCostAmount_Mutates()
    {
        var state = PersonnelFileTrainingPatchState.From(Baseline());

        Assert.True(PersonnelFileTrainingPatchApplier.Apply([Replace("/costAmount", 250.50)], state).IsSuccess);
        Assert.Equal(250.50m, state.CostAmount);
    }

    [Fact]
    public void Apply_RemoveOptionalCostAmount_SetsNull()
    {
        var state = PersonnelFileTrainingPatchState.From(Baseline());

        Assert.True(PersonnelFileTrainingPatchApplier.Apply([Remove("/costAmount")], state).IsSuccess);
        Assert.Null(state.CostAmount);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveOptionalEndDate_SetsNull()
    {
        var state = PersonnelFileTrainingPatchState.From(Baseline());

        Assert.True(PersonnelFileTrainingPatchApplier.Apply([Remove("/endDate")], state).IsSuccess);
        Assert.Null(state.EndDate);
    }

    [Fact]
    public void Apply_RemoveRequiredTrainingName_Fails()
    {
        var state = PersonnelFileTrainingPatchState.From(Baseline());

        Assert.True(PersonnelFileTrainingPatchApplier.Apply([Remove("/trainingName")], state).IsSuccess);
        Assert.True(PersonnelFileTrainingPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveDurationValue_Fails()
    {
        var state = PersonnelFileTrainingPatchState.From(Baseline());

        Assert.True(PersonnelFileTrainingPatchApplier.Apply([Remove("/durationValue")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveBool_Fails()
    {
        var state = PersonnelFileTrainingPatchState.From(Baseline());

        Assert.True(PersonnelFileTrainingPatchApplier.Apply([Remove("/isInternal")], state).IsFailure);
    }

    [Fact]
    public void Apply_NonNumberForDurationValue_Fails()
    {
        var state = PersonnelFileTrainingPatchState.From(Baseline());

        Assert.True(PersonnelFileTrainingPatchApplier.Apply([Replace("/durationValue", "abc")], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFileTrainingPatchState.From(Baseline());

        Assert.True(PersonnelFileTrainingPatchApplier.Apply(
            [new PersonnelFileTrainingPatchOperation("copy", "/topic", "/trainingName", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFileTrainingPatchState.From(Baseline());

        Assert.True(PersonnelFileTrainingPatchApplier.Apply([Replace("/unknown", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFileTrainingPatchState.From(Baseline());

        Assert.True(PersonnelFileTrainingPatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_Baseline_Succeeds()
    {
        Assert.True(PersonnelFileTrainingPatchApplier.Validate(PersonnelFileTrainingPatchState.From(Baseline())).IsSuccess);
    }

    [Fact]
    public void Validate_EndDateBeforeStartDate_Fails()
    {
        var state = PersonnelFileTrainingPatchState.From(Baseline());
        state.EndDate = state.StartDate.AddDays(-1);

        Assert.True(PersonnelFileTrainingPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_NonPositiveDurationValue_Fails()
    {
        var state = PersonnelFileTrainingPatchState.From(Baseline());
        state.DurationValue = 0m;

        Assert.True(PersonnelFileTrainingPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_NegativeCostAmount_Fails()
    {
        var state = PersonnelFileTrainingPatchState.From(Baseline());
        state.CostAmount = -1m;

        Assert.True(PersonnelFileTrainingPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_CostAmountWithoutCurrency_Fails()
    {
        var state = PersonnelFileTrainingPatchState.From(Baseline());
        state.CostCurrencyCode = null;

        Assert.True(PersonnelFileTrainingPatchApplier.Validate(state).IsFailure);
    }
}
