using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical employment-assignment JSON Patch surface: the pure
/// <see cref="PersonnelFileEmploymentAssignmentPatchApplier"/> and the
/// <see cref="PersonnelFileEmploymentAssignmentPatchState"/> projection. The assignment's
/// <c>isActive</c> flag is patchable (replacing the former dedicated <c>/deactivate</c> endpoint),
/// so the applier must accept boolean values and flag the mutation while preserving the
/// business-field validation the Add/Update commands run.
/// </summary>
public sealed class PersonnelFileEmploymentAssignmentPatchTests
{
    private static PersonnelFileEmploymentAssignmentResponse Baseline() =>
        new(
            Guid.NewGuid(),
            "PRIMARY",
            "INDEFINIDO",
            "DIURNA",
            "MENSUAL",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            true,
            true,
            "Primary assignment.",
            Guid.NewGuid());

    private static PersonnelFileEmploymentAssignmentPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFileEmploymentAssignmentPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var baseline = Baseline();
        var state = PersonnelFileEmploymentAssignmentPatchState.From(baseline);

        Assert.Equal("PRIMARY", state.AssignmentTypeCode);
        Assert.Equal(baseline.PositionSlotId, state.PositionSlotId);
        Assert.Equal("Primary assignment.", state.Notes);
        Assert.True(state.IsPrimary);
        Assert.True(state.IsActive);
        Assert.False(state.IsActiveMutated);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var baseline = Baseline();
        var input = PersonnelFileEmploymentAssignmentPatchState.From(baseline).ToInput();

        Assert.Equal("PRIMARY", input.AssignmentTypeCode);
        Assert.Equal(baseline.OrgUnitId, input.OrgUnitId);
        Assert.Equal("Primary assignment.", input.Notes);
        Assert.True(input.IsPrimary);
        Assert.True(input.IsActive);
    }

    [Fact]
    public void Apply_ReplaceAssignmentTypeCode_Mutates()
    {
        var state = PersonnelFileEmploymentAssignmentPatchState.From(Baseline());

        Assert.True(PersonnelFileEmploymentAssignmentPatchApplier.Apply([Replace("/assignmentTypeCode", "SECONDARY")], state).IsSuccess);
        Assert.Equal("SECONDARY", state.AssignmentTypeCode);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void From_MapsRestDayOfWeek()
    {
        var state = PersonnelFileEmploymentAssignmentPatchState.From(Baseline() with { RestDayOfWeek = 3 });

        Assert.Equal(3, state.RestDayOfWeek);
    }

    [Fact]
    public void ToInput_RoundTripsRestDayOfWeek()
    {
        var input = PersonnelFileEmploymentAssignmentPatchState.From(Baseline() with { RestDayOfWeek = 3 }).ToInput();

        Assert.Equal(3, input.RestDayOfWeek);
    }

    [Fact]
    public void Apply_ReplaceRestDayOfWeek_Mutates()
    {
        var state = PersonnelFileEmploymentAssignmentPatchState.From(Baseline());

        Assert.True(PersonnelFileEmploymentAssignmentPatchApplier.Apply([Replace("/restDayOfWeek", 3)], state).IsSuccess);
        Assert.Equal(3, state.RestDayOfWeek);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveRestDayOfWeek_ClearsValue()
    {
        var state = PersonnelFileEmploymentAssignmentPatchState.From(Baseline() with { RestDayOfWeek = 3 });

        Assert.True(PersonnelFileEmploymentAssignmentPatchApplier.Apply([Remove("/restDayOfWeek")], state).IsSuccess);
        Assert.Null(state.RestDayOfWeek);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Validate_RestDayOfWeekOutOfRange_Fails()
    {
        var state = PersonnelFileEmploymentAssignmentPatchState.From(Baseline());

        Assert.True(PersonnelFileEmploymentAssignmentPatchApplier.Apply([Replace("/restDayOfWeek", 7)], state).IsSuccess);
        Assert.True(PersonnelFileEmploymentAssignmentPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_RestDayOfWeekInRange_Succeeds()
    {
        var state = PersonnelFileEmploymentAssignmentPatchState.From(Baseline());

        Assert.True(PersonnelFileEmploymentAssignmentPatchApplier.Apply([Replace("/restDayOfWeek", 0)], state).IsSuccess);
        Assert.True(PersonnelFileEmploymentAssignmentPatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_ReplacePositionSlotId_Mutates()
    {
        var state = PersonnelFileEmploymentAssignmentPatchState.From(Baseline());
        var newSlot = Guid.NewGuid();

        Assert.True(PersonnelFileEmploymentAssignmentPatchApplier.Apply([Replace("/positionSlotId", newSlot)], state).IsSuccess);
        Assert.Equal(newSlot, state.PositionSlotId);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceIsActiveFalse_MutatesAndFlagsActiveChange()
    {
        var state = PersonnelFileEmploymentAssignmentPatchState.From(Baseline());

        Assert.True(PersonnelFileEmploymentAssignmentPatchApplier.Apply([Replace("/isActive", false)], state).IsSuccess);
        Assert.False(state.IsActive);
        Assert.True(state.IsActiveMutated);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceIsActiveTrue_MutatesAndFlagsActiveChange()
    {
        var state = PersonnelFileEmploymentAssignmentPatchState.From(Baseline() with { IsActive = false });

        Assert.True(PersonnelFileEmploymentAssignmentPatchApplier.Apply([Replace("/isActive", true)], state).IsSuccess);
        Assert.True(state.IsActive);
        Assert.True(state.IsActiveMutated);
    }

    [Fact]
    public void Apply_BusinessFieldOnly_DoesNotFlagActiveChange()
    {
        var state = PersonnelFileEmploymentAssignmentPatchState.From(Baseline());

        Assert.True(PersonnelFileEmploymentAssignmentPatchApplier.Apply([Replace("/isPrimary", false)], state).IsSuccess);
        Assert.False(state.IsActiveMutated);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_NonBooleanForIsActive_Fails()
    {
        var state = PersonnelFileEmploymentAssignmentPatchState.From(Baseline());

        Assert.True(PersonnelFileEmploymentAssignmentPatchApplier.Apply([Replace("/isActive", "yes")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveIsActive_Fails()
    {
        var state = PersonnelFileEmploymentAssignmentPatchState.From(Baseline());

        Assert.True(PersonnelFileEmploymentAssignmentPatchApplier.Apply([Remove("/isActive")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveEndDate_ClearsValue()
    {
        var state = PersonnelFileEmploymentAssignmentPatchState.From(Baseline());

        Assert.True(PersonnelFileEmploymentAssignmentPatchApplier.Apply([Remove("/endDate")], state).IsSuccess);
        Assert.Null(state.EndDate);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveOptionalPositionSlotId_ClearsValue()
    {
        var state = PersonnelFileEmploymentAssignmentPatchState.From(Baseline());

        Assert.True(PersonnelFileEmploymentAssignmentPatchApplier.Apply([Remove("/positionSlotId")], state).IsSuccess);
        Assert.Null(state.PositionSlotId);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveOptionalNotes_ClearsValue()
    {
        var state = PersonnelFileEmploymentAssignmentPatchState.From(Baseline());

        Assert.True(PersonnelFileEmploymentAssignmentPatchApplier.Apply([Remove("/notes")], state).IsSuccess);
        Assert.Null(state.Notes);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveRequiredAssignmentTypeCode_FailsValidation()
    {
        var state = PersonnelFileEmploymentAssignmentPatchState.From(Baseline());

        Assert.True(PersonnelFileEmploymentAssignmentPatchApplier.Apply([Remove("/assignmentTypeCode")], state).IsSuccess);
        Assert.True(PersonnelFileEmploymentAssignmentPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveRequiredStartDate_Fails()
    {
        var state = PersonnelFileEmploymentAssignmentPatchState.From(Baseline());

        Assert.True(PersonnelFileEmploymentAssignmentPatchApplier.Apply([Remove("/startDate")], state).IsFailure);
    }

    [Fact]
    public void Apply_NonGuidForPositionSlotId_Fails()
    {
        var state = PersonnelFileEmploymentAssignmentPatchState.From(Baseline());

        Assert.True(PersonnelFileEmploymentAssignmentPatchApplier.Apply([Replace("/positionSlotId", "not-a-guid")], state).IsFailure);
    }

    [Fact]
    public void Apply_NonDateForStartDate_Fails()
    {
        var state = PersonnelFileEmploymentAssignmentPatchState.From(Baseline());

        Assert.True(PersonnelFileEmploymentAssignmentPatchApplier.Apply([Replace("/startDate", "not-a-date")], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFileEmploymentAssignmentPatchState.From(Baseline());

        Assert.True(PersonnelFileEmploymentAssignmentPatchApplier.Apply(
            [new PersonnelFileEmploymentAssignmentPatchOperation("copy", "/assignmentTypeCode", "/assignmentTypeCode", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFileEmploymentAssignmentPatchState.From(Baseline());

        Assert.True(PersonnelFileEmploymentAssignmentPatchApplier.Apply([Replace("/unknown", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_Fails()
    {
        var state = PersonnelFileEmploymentAssignmentPatchState.From(Baseline());

        Assert.True(PersonnelFileEmploymentAssignmentPatchApplier.Apply([Replace("/assignmentTypeCode/0", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFileEmploymentAssignmentPatchState.From(Baseline());

        Assert.True(PersonnelFileEmploymentAssignmentPatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_Baseline_Succeeds()
    {
        Assert.True(PersonnelFileEmploymentAssignmentPatchApplier.Validate(PersonnelFileEmploymentAssignmentPatchState.From(Baseline())).IsSuccess);
    }

    [Fact]
    public void Validate_BlankAssignmentTypeCode_Fails()
    {
        var state = PersonnelFileEmploymentAssignmentPatchState.From(Baseline());
        state.AssignmentTypeCode = " ";

        Assert.True(PersonnelFileEmploymentAssignmentPatchApplier.Validate(state).IsFailure);
    }
}
