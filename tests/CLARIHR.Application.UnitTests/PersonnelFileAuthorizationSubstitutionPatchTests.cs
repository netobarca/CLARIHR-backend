using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical authorization-substitution JSON Patch surface: the pure
/// <see cref="PersonnelFileAuthorizationSubstitutionPatchApplier"/> and the
/// <see cref="PersonnelFileAuthorizationSubstitutionPatchState"/> projection. After the D-01…D-12 hardening
/// the substitute's position is a reference to one of the substitute's active slots
/// (<c>substitutePositionSlotId</c>, required, not removable) and the end date is mandatory (D-03, not
/// removable). The <c>isActive</c> flag stays patchable (replacing the former dedicated <c>/deactivate</c>
/// endpoint), and the <c>substitutePersonnelFileId</c> target stays patchable too (the handler re-runs the
/// self-substitution guard against the patched state).
/// </summary>
public sealed class PersonnelFileAuthorizationSubstitutionPatchTests
{
    private static PersonnelFileAuthorizationSubstitutionResponse Baseline() =>
        new(
            Guid.NewGuid(),
            "VACATION",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Acting supervisor",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            true,
            "Covers vacation.",
            Guid.NewGuid());

    private static PersonnelFileAuthorizationSubstitutionPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFileAuthorizationSubstitutionPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var baseline = Baseline();
        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(baseline);

        Assert.Equal("VACATION", state.SubstitutionTypeCode);
        Assert.Equal(baseline.SubstitutePersonnelFileId, state.SubstitutePersonnelFileId);
        Assert.Equal(baseline.SubstitutePositionSlotPublicId, state.SubstitutePositionSlotId);
        Assert.Equal(baseline.EndDate, state.EndDate);
        Assert.Equal("Covers vacation.", state.Notes);
        Assert.True(state.IsActive);
        Assert.False(state.IsActiveMutated);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var baseline = Baseline();
        var input = PersonnelFileAuthorizationSubstitutionPatchState.From(baseline).ToInput();

        Assert.Equal("VACATION", input.SubstitutionTypeCode);
        Assert.Equal(baseline.SubstitutePersonnelFileId, input.SubstitutePersonnelFileId);
        Assert.Equal(baseline.SubstitutePositionSlotPublicId, input.SubstitutePositionSlotPublicId);
        Assert.Equal(baseline.EndDate, input.EndDate);
        Assert.True(input.IsActive);
    }

    [Fact]
    public void Apply_ReplaceSubstitutionTypeCode_Mutates()
    {
        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(Baseline());

        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Apply([Replace("/substitutionTypeCode", "LEAVE")], state).IsSuccess);
        Assert.Equal("LEAVE", state.SubstitutionTypeCode);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceSubstitutePersonnelFileId_Mutates()
    {
        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(Baseline());
        var newTarget = Guid.NewGuid();

        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Apply([Replace("/substitutePersonnelFileId", newTarget)], state).IsSuccess);
        Assert.Equal(newTarget, state.SubstitutePersonnelFileId);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveSubstitutePersonnelFileId_Fails()
    {
        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(Baseline());

        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Apply([Remove("/substitutePersonnelFileId")], state).IsFailure);
    }

    [Fact]
    public void Apply_NonGuidForSubstitutePersonnelFileId_Fails()
    {
        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(Baseline());

        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Apply([Replace("/substitutePersonnelFileId", "not-a-guid")], state).IsFailure);
    }

    [Fact]
    public void Apply_ReplaceSubstitutePositionSlotId_Mutates()
    {
        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(Baseline());
        var newSlot = Guid.NewGuid();

        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Apply([Replace("/substitutePositionSlotId", newSlot)], state).IsSuccess);
        Assert.Equal(newSlot, state.SubstitutePositionSlotId);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveSubstitutePositionSlotId_Fails()
    {
        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(Baseline());

        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Apply([Remove("/substitutePositionSlotId")], state).IsFailure);
    }

    [Fact]
    public void Apply_NonGuidForSubstitutePositionSlotId_Fails()
    {
        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(Baseline());

        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Apply([Replace("/substitutePositionSlotId", "not-a-guid")], state).IsFailure);
    }

    [Fact]
    public void Apply_ReplaceIsActiveFalse_MutatesAndFlagsActiveChange()
    {
        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(Baseline());

        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Apply([Replace("/isActive", false)], state).IsSuccess);
        Assert.False(state.IsActive);
        Assert.True(state.IsActiveMutated);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceIsActiveTrue_MutatesAndFlagsActiveChange()
    {
        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(Baseline() with { IsActive = false });

        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Apply([Replace("/isActive", true)], state).IsSuccess);
        Assert.True(state.IsActive);
        Assert.True(state.IsActiveMutated);
    }

    [Fact]
    public void Apply_BusinessFieldOnly_DoesNotFlagActiveChange()
    {
        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(Baseline());

        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Apply([Replace("/substitutionTypeCode", "LEAVE")], state).IsSuccess);
        Assert.False(state.IsActiveMutated);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_NonBooleanForIsActive_Fails()
    {
        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(Baseline());

        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Apply([Replace("/isActive", "yes")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveIsActive_Fails()
    {
        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(Baseline());

        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Apply([Remove("/isActive")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveEndDate_Fails()
    {
        // D-03: the end date is mandatory and cannot be removed.
        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(Baseline());

        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Apply([Remove("/endDate")], state).IsFailure);
    }

    [Fact]
    public void Apply_ReplaceEndDate_Mutates()
    {
        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(Baseline());
        var newEnd = new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc);

        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Apply([Replace("/endDate", newEnd)], state).IsSuccess);
        Assert.Equal(newEnd, state.EndDate);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveOptionalNotes_ClearsValue()
    {
        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(Baseline());

        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Apply([Remove("/notes")], state).IsSuccess);
        Assert.Null(state.Notes);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveRequiredSubstitutionTypeCode_FailsValidation()
    {
        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(Baseline());

        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Apply([Remove("/substitutionTypeCode")], state).IsSuccess);
        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveRequiredStartDate_Fails()
    {
        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(Baseline());

        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Apply([Remove("/startDate")], state).IsFailure);
    }

    [Fact]
    public void Apply_NonDateForStartDate_Fails()
    {
        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(Baseline());

        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Apply([Replace("/startDate", "not-a-date")], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(Baseline());

        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Apply(
            [new PersonnelFileAuthorizationSubstitutionPatchOperation("copy", "/substitutionTypeCode", "/substitutionTypeCode", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(Baseline());

        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Apply([Replace("/unknown", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_Fails()
    {
        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(Baseline());

        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Apply([Replace("/substitutionTypeCode/0", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(Baseline());

        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_Baseline_Succeeds()
    {
        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Validate(PersonnelFileAuthorizationSubstitutionPatchState.From(Baseline())).IsSuccess);
    }

    [Fact]
    public void Validate_BlankSubstitutionTypeCode_Fails()
    {
        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(Baseline());
        state.SubstitutionTypeCode = " ";

        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_EmptySubstitutePersonnelFileId_Fails()
    {
        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(Baseline());
        state.SubstitutePersonnelFileId = Guid.Empty;

        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_EmptySubstitutePositionSlotId_Fails()
    {
        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(Baseline());
        state.SubstitutePositionSlotId = Guid.Empty;

        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_MissingEndDate_Fails()
    {
        // D-03: a substitution without an end date is invalid (the applier blocks removal; this guards
        // the state directly in case a caller never set it).
        var state = PersonnelFileAuthorizationSubstitutionPatchState.From(Baseline());
        state.EndDate = null;

        Assert.True(PersonnelFileAuthorizationSubstitutionPatchApplier.Validate(state).IsFailure);
    }
}
