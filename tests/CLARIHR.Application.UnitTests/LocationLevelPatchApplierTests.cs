using System.Text.Json;
using CLARIHR.Application.Features.Locations.Hierarchy;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the display-name-only JSON Patch allow-list of
/// <see cref="LocationLevelPatchApplier"/> (the novel logic added when LocationLevels was aligned to
/// the canonical RFC-6902 PATCH). The structural flags (isActive/isRequired/allowsWorkCenters) are
/// interdependent and validated as a unit by PUT, so they are NOT patchable here — only the display
/// name is. Locks that decision in so a mechanical replication cannot silently widen the surface.
/// </summary>
public sealed class LocationLevelPatchApplierTests
{
    private static LocationLevelPatchState NewState() =>
        LocationLevelPatchState.From(new LocationLevelResponse(
            Id: Guid.NewGuid(),
            LevelOrder: 2,
            DisplayName: "Departamento",
            IsActive: true,
            IsRequired: false,
            AllowsWorkCenters: false,
            ConcurrencyToken: Guid.NewGuid()));

    private static LocationLevelPatchOperation Op(string op, string path, object? value) =>
        new(op, path, null, value is null ? null : JsonSerializer.SerializeToElement(value));

    [Fact]
    public void Apply_ReplaceDisplayName_MutatesStateAndValidates()
    {
        var state = NewState();

        var result = LocationLevelPatchApplier.Apply(new[] { Op("replace", "/displayName", "Region") }, state);

        Assert.True(result.IsSuccess);
        Assert.True(state.HasMutation);
        Assert.Equal("Region", state.DisplayName);
        Assert.True(LocationLevelPatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_StructuralFlags_AreRejected()
    {
        var state = NewState();

        Assert.True(LocationLevelPatchApplier.Apply(new[] { Op("replace", "/isActive", false) }, state).IsFailure);
        Assert.True(LocationLevelPatchApplier.Apply(new[] { Op("replace", "/isRequired", true) }, state).IsFailure);
        Assert.True(LocationLevelPatchApplier.Apply(new[] { Op("replace", "/allowsWorkCenters", true) }, state).IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_LevelOrderPath_IsRejected()
    {
        var state = NewState();

        var result = LocationLevelPatchApplier.Apply(new[] { Op("replace", "/levelOrder", 3) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_ConcurrencyTokenPath_IsRejected()
    {
        var state = NewState();

        var result = LocationLevelPatchApplier.Apply(new[] { Op("replace", "/concurrencyToken", Guid.NewGuid().ToString()) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_IsRejected()
    {
        var state = NewState();

        var result = LocationLevelPatchApplier.Apply(new[] { Op("move", "/displayName", "X") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_IsRejected()
    {
        var state = NewState();

        var result = LocationLevelPatchApplier.Apply(new[] { Op("replace", "/displayName/sub", "X") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_UnknownPath_IsRejected()
    {
        var state = NewState();

        var result = LocationLevelPatchApplier.Apply(new[] { Op("replace", "/unknownField", "X") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_RemoveDisplayName_IsRejected()
    {
        var state = NewState();

        var result = LocationLevelPatchApplier.Apply(new[] { Op("remove", "/displayName", null) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Validate_BlankDisplayNameAfterReplace_Fails()
    {
        var state = NewState();

        var apply = LocationLevelPatchApplier.Apply(new[] { Op("replace", "/displayName", "   ") }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(LocationLevelPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = NewState();

        var result = LocationLevelPatchApplier.Apply(Array.Empty<LocationLevelPatchOperation>(), state);

        Assert.True(result.IsSuccess);
        Assert.False(state.HasMutation);
    }
}
