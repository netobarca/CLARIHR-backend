using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical EmployeeRelation JSON Patch surface (PersonnelFileInterests
/// remediation): the pure <see cref="PersonnelFileEmployeeRelationPatchApplier"/> and the
/// <see cref="PersonnelFileEmployeeRelationPatchState"/> projection. Cross-file resolution
/// (self-reference, existing-employee, duplicate) is enforced in the command handler, not the
/// pure applier, so it is covered by the command tests.
/// </summary>
public sealed class PersonnelFileEmployeeRelationPatchTests
{
    private static readonly Guid RelatedEmployeeId = Guid.NewGuid();

    private static PersonnelFileEmployeeRelationResponse Baseline() =>
        new(Guid.NewGuid(), RelatedEmployeeId, "Luis Related", "Sibling", Guid.NewGuid());

    private static PersonnelFileEmployeeRelationPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFileEmployeeRelationPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var state = PersonnelFileEmployeeRelationPatchState.From(Baseline());

        Assert.Equal(RelatedEmployeeId, state.RelatedEmployeePublicId);
        Assert.Equal("Sibling", state.Relationship);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var input = PersonnelFileEmployeeRelationPatchState.From(Baseline()).ToInput();

        Assert.Equal(RelatedEmployeeId, input.RelatedEmployeePublicId);
        Assert.Equal("Sibling", input.Relationship);
    }

    [Fact]
    public void Apply_ReplaceRelationship_Mutates()
    {
        var state = PersonnelFileEmployeeRelationPatchState.From(Baseline());

        Assert.True(PersonnelFileEmployeeRelationPatchApplier.Apply([Replace("/relationship", "Cousin")], state).IsSuccess);
        Assert.Equal("Cousin", state.Relationship);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceRelatedEmployeePublicId_Mutates()
    {
        var state = PersonnelFileEmployeeRelationPatchState.From(Baseline());
        var newId = Guid.NewGuid();

        Assert.True(PersonnelFileEmployeeRelationPatchApplier.Apply([Replace("/relatedEmployeePublicId", newId)], state).IsSuccess);
        Assert.Equal(newId, state.RelatedEmployeePublicId);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveRelationship_Fails()
    {
        var state = PersonnelFileEmployeeRelationPatchState.From(Baseline());

        Assert.True(PersonnelFileEmployeeRelationPatchApplier.Apply([Remove("/relationship")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveRelatedEmployeePublicId_Fails()
    {
        var state = PersonnelFileEmployeeRelationPatchState.From(Baseline());

        Assert.True(PersonnelFileEmployeeRelationPatchApplier.Apply([Remove("/relatedEmployeePublicId")], state).IsFailure);
    }

    [Fact]
    public void Apply_NonGuidForRelatedEmployee_Fails()
    {
        var state = PersonnelFileEmployeeRelationPatchState.From(Baseline());

        Assert.True(PersonnelFileEmployeeRelationPatchApplier.Apply([Replace("/relatedEmployeePublicId", "not-a-guid")], state).IsFailure);
    }

    [Fact]
    public void Apply_NonStringForRelationship_Fails()
    {
        var state = PersonnelFileEmployeeRelationPatchState.From(Baseline());

        Assert.True(PersonnelFileEmployeeRelationPatchApplier.Apply([Replace("/relationship", 42)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFileEmployeeRelationPatchState.From(Baseline());

        Assert.True(PersonnelFileEmployeeRelationPatchApplier.Apply(
            [new PersonnelFileEmployeeRelationPatchOperation("copy", "/relationship", "/relationship", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFileEmployeeRelationPatchState.From(Baseline());

        Assert.True(PersonnelFileEmployeeRelationPatchApplier.Apply([Replace("/notes", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFileEmployeeRelationPatchState.From(Baseline());

        Assert.True(PersonnelFileEmployeeRelationPatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_Baseline_Succeeds()
    {
        Assert.True(PersonnelFileEmployeeRelationPatchApplier.Validate(PersonnelFileEmployeeRelationPatchState.From(Baseline())).IsSuccess);
    }

    [Fact]
    public void Validate_EmptyRelatedEmployeePublicId_Fails()
    {
        var state = PersonnelFileEmployeeRelationPatchState.From(Baseline());
        state.RelatedEmployeePublicId = Guid.Empty;

        Assert.True(PersonnelFileEmployeeRelationPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_BlankRelationship_Fails()
    {
        var state = PersonnelFileEmployeeRelationPatchState.From(Baseline());
        state.Relationship = " ";

        Assert.True(PersonnelFileEmployeeRelationPatchApplier.Validate(state).IsFailure);
    }
}
