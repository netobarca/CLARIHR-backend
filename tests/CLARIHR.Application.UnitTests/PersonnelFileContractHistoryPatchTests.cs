using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical contract-history JSON Patch surface: the pure
/// <see cref="PersonnelFileContractHistoryPatchApplier"/> and the
/// <see cref="PersonnelFileContractHistoryPatchState"/> projection. Contract history is the one
/// Employment sub-resource without a hard DELETE, so deactivation flows exclusively through the
/// patchable <c>isActive</c> flag; the applier must accept boolean values and flag the mutation
/// while preserving the business-field validation the Add/Update commands run.
/// </summary>
public sealed class PersonnelFileContractHistoryPatchTests
{
    private static PersonnelFileContractHistoryResponse Baseline() =>
        new(
            Guid.NewGuid(),
            "PERMANENT",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            Guid.NewGuid(),
            true,
            "Permanent contract.",
            Guid.NewGuid());

    private static PersonnelFileContractHistoryPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFileContractHistoryPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var baseline = Baseline();
        var state = PersonnelFileContractHistoryPatchState.From(baseline);

        Assert.Equal("PERMANENT", state.ContractTypeCode);
        Assert.Equal(baseline.PositionSlotId, state.PositionSlotId);
        Assert.Equal("Permanent contract.", state.Notes);
        Assert.True(state.IsActive);
        Assert.False(state.IsActiveMutated);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var baseline = Baseline();
        var input = PersonnelFileContractHistoryPatchState.From(baseline).ToInput();

        Assert.Equal("PERMANENT", input.ContractTypeCode);
        Assert.Equal(baseline.PositionSlotId, input.PositionSlotId);
        Assert.Equal("Permanent contract.", input.Notes);
        Assert.True(input.IsActive);
    }

    [Fact]
    public void Apply_ReplaceContractTypeCode_Mutates()
    {
        var state = PersonnelFileContractHistoryPatchState.From(Baseline());

        Assert.True(PersonnelFileContractHistoryPatchApplier.Apply([Replace("/contractTypeCode", "TEMPORARY")], state).IsSuccess);
        Assert.Equal("TEMPORARY", state.ContractTypeCode);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceIsActiveFalse_MutatesAndFlagsActiveChange()
    {
        var state = PersonnelFileContractHistoryPatchState.From(Baseline());

        Assert.True(PersonnelFileContractHistoryPatchApplier.Apply([Replace("/isActive", false)], state).IsSuccess);
        Assert.False(state.IsActive);
        Assert.True(state.IsActiveMutated);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceIsActiveTrue_MutatesAndFlagsActiveChange()
    {
        var state = PersonnelFileContractHistoryPatchState.From(Baseline() with { IsActive = false });

        Assert.True(PersonnelFileContractHistoryPatchApplier.Apply([Replace("/isActive", true)], state).IsSuccess);
        Assert.True(state.IsActive);
        Assert.True(state.IsActiveMutated);
    }

    [Fact]
    public void Apply_BusinessFieldOnly_DoesNotFlagActiveChange()
    {
        var state = PersonnelFileContractHistoryPatchState.From(Baseline());

        Assert.True(PersonnelFileContractHistoryPatchApplier.Apply([Replace("/contractTypeCode", "TEMPORARY")], state).IsSuccess);
        Assert.False(state.IsActiveMutated);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_NonBooleanForIsActive_Fails()
    {
        var state = PersonnelFileContractHistoryPatchState.From(Baseline());

        Assert.True(PersonnelFileContractHistoryPatchApplier.Apply([Replace("/isActive", "yes")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveIsActive_Fails()
    {
        var state = PersonnelFileContractHistoryPatchState.From(Baseline());

        Assert.True(PersonnelFileContractHistoryPatchApplier.Apply([Remove("/isActive")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveContractEndDate_ClearsValue()
    {
        var state = PersonnelFileContractHistoryPatchState.From(Baseline());

        Assert.True(PersonnelFileContractHistoryPatchApplier.Apply([Remove("/contractEndDate")], state).IsSuccess);
        Assert.Null(state.ContractEndDate);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveOptionalPositionSlotId_ClearsValue()
    {
        var state = PersonnelFileContractHistoryPatchState.From(Baseline());

        Assert.True(PersonnelFileContractHistoryPatchApplier.Apply([Remove("/positionSlotId")], state).IsSuccess);
        Assert.Null(state.PositionSlotId);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveOptionalNotes_ClearsValue()
    {
        var state = PersonnelFileContractHistoryPatchState.From(Baseline());

        Assert.True(PersonnelFileContractHistoryPatchApplier.Apply([Remove("/notes")], state).IsSuccess);
        Assert.Null(state.Notes);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveRequiredContractTypeCode_FailsValidation()
    {
        var state = PersonnelFileContractHistoryPatchState.From(Baseline());

        Assert.True(PersonnelFileContractHistoryPatchApplier.Apply([Remove("/contractTypeCode")], state).IsSuccess);
        Assert.True(PersonnelFileContractHistoryPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveRequiredContractDate_Fails()
    {
        var state = PersonnelFileContractHistoryPatchState.From(Baseline());

        Assert.True(PersonnelFileContractHistoryPatchApplier.Apply([Remove("/contractDate")], state).IsFailure);
    }

    [Fact]
    public void Apply_NonStringForContractTypeCode_Fails()
    {
        var state = PersonnelFileContractHistoryPatchState.From(Baseline());

        Assert.True(PersonnelFileContractHistoryPatchApplier.Apply([Replace("/contractTypeCode", 42)], state).IsFailure);
    }

    [Fact]
    public void Apply_NonDateForContractDate_Fails()
    {
        var state = PersonnelFileContractHistoryPatchState.From(Baseline());

        Assert.True(PersonnelFileContractHistoryPatchApplier.Apply([Replace("/contractDate", "not-a-date")], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFileContractHistoryPatchState.From(Baseline());

        Assert.True(PersonnelFileContractHistoryPatchApplier.Apply(
            [new PersonnelFileContractHistoryPatchOperation("copy", "/contractTypeCode", "/contractTypeCode", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFileContractHistoryPatchState.From(Baseline());

        Assert.True(PersonnelFileContractHistoryPatchApplier.Apply([Replace("/unknown", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_Fails()
    {
        var state = PersonnelFileContractHistoryPatchState.From(Baseline());

        Assert.True(PersonnelFileContractHistoryPatchApplier.Apply([Replace("/contractTypeCode/0", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFileContractHistoryPatchState.From(Baseline());

        Assert.True(PersonnelFileContractHistoryPatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_Baseline_Succeeds()
    {
        Assert.True(PersonnelFileContractHistoryPatchApplier.Validate(PersonnelFileContractHistoryPatchState.From(Baseline())).IsSuccess);
    }

    [Fact]
    public void Validate_BlankContractTypeCode_Fails()
    {
        var state = PersonnelFileContractHistoryPatchState.From(Baseline());
        state.ContractTypeCode = " ";

        Assert.True(PersonnelFileContractHistoryPatchApplier.Validate(state).IsFailure);
    }
}
