using System.Text.Json;
using CLARIHR.Application.Features.Locations.Groups;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the scalar-only JSON Patch allow-list of <see cref="LocationGroupPatchApplier"/>
/// (the novel logic added when LocationGroups was aligned to the canonical RFC-6902 PATCH). Locks in
/// the scalar-only decision: only <c>/code</c>, <c>/name</c>, <c>/description</c> are patchable —
/// the level is immutable, the parent moves via <c>/move</c>, activation via activate/inactivate,
/// and the token only via If-Match. (Default-group protection is enforced in the handler, not here.)
/// </summary>
public sealed class LocationGroupPatchApplierTests
{
    private static LocationGroupPatchState NewState() =>
        LocationGroupPatchState.From(new LocationGroupResponse(
            Id: Guid.NewGuid(),
            LevelOrder: 2,
            Code: "LG-001",
            Name: "Zona Centro",
            ParentId: Guid.NewGuid(),
            Description: "Desc",
            IsActive: true,
            IsDefault: false,
            ConcurrencyToken: Guid.NewGuid(),
            CreatedAtUtc: DateTime.UtcNow,
            ModifiedAtUtc: null));

    private static LocationGroupPatchOperation Op(string op, string path, object? value) =>
        new(op, path, null, value is null ? null : JsonSerializer.SerializeToElement(value));

    [Fact]
    public void Apply_ReplaceName_MutatesStateAndValidates()
    {
        var state = NewState();

        var result = LocationGroupPatchApplier.Apply(new[] { Op("replace", "/name", "Zona Norte") }, state);

        Assert.True(result.IsSuccess);
        Assert.True(state.HasMutation);
        Assert.Equal("Zona Norte", state.Name);
        Assert.True(LocationGroupPatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_RemoveDescription_SetsNull()
    {
        var state = NewState();

        var result = LocationGroupPatchApplier.Apply(new[] { Op("remove", "/description", null) }, state);

        Assert.True(result.IsSuccess);
        Assert.Null(state.Description);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_IsActivePath_IsRejected()
    {
        var state = NewState();

        var result = LocationGroupPatchApplier.Apply(new[] { Op("replace", "/isActive", false) }, state);

        Assert.True(result.IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_LevelOrderPath_IsRejected()
    {
        var state = NewState();

        var result = LocationGroupPatchApplier.Apply(new[] { Op("replace", "/levelOrder", 3) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_ParentPath_IsRejected()
    {
        var state = NewState();

        Assert.True(LocationGroupPatchApplier.Apply(new[] { Op("replace", "/parentPublicId", Guid.NewGuid().ToString()) }, state).IsFailure);
        Assert.True(LocationGroupPatchApplier.Apply(new[] { Op("replace", "/parentId", Guid.NewGuid().ToString()) }, state).IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_ConcurrencyTokenPath_IsRejected()
    {
        var state = NewState();

        var result = LocationGroupPatchApplier.Apply(new[] { Op("replace", "/concurrencyToken", Guid.NewGuid().ToString()) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_IsRejected()
    {
        var state = NewState();

        var result = LocationGroupPatchApplier.Apply(new[] { Op("move", "/name", "X") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_IsRejected()
    {
        var state = NewState();

        var result = LocationGroupPatchApplier.Apply(new[] { Op("replace", "/name/sub", "X") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_UnknownPath_IsRejected()
    {
        var state = NewState();

        var result = LocationGroupPatchApplier.Apply(new[] { Op("replace", "/unknownField", "X") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_RemoveRequiredCode_IsRejected()
    {
        var state = NewState();

        var result = LocationGroupPatchApplier.Apply(new[] { Op("remove", "/code", null) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Validate_BlankNameAfterReplace_Fails()
    {
        var state = NewState();

        var apply = LocationGroupPatchApplier.Apply(new[] { Op("replace", "/name", "   ") }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(LocationGroupPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = NewState();

        var result = LocationGroupPatchApplier.Apply(Array.Empty<LocationGroupPatchOperation>(), state);

        Assert.True(result.IsSuccess);
        Assert.False(state.HasMutation);
    }
}
