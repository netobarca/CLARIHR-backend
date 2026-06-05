using System.Text.Json;
using CLARIHR.Application.Features.CompetencyFramework;
using CLARIHR.Domain.CompetencyFramework;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the scalar JSON Patch allow-list of <see cref="OccupationalPyramidLevelPatchApplier"/>
/// (CompetencyFramework PR1). Patchable = <c>/code</c>, <c>/name</c>, <c>/levelOrder</c> and the optional
/// <c>/description</c> (string or null/remove to clear). Activation state goes through <c>/activate</c> and
/// <c>/inactivate</c>; the concurrency token travels in <c>If-Match</c>; id/companyId/timestamps are
/// read-only. Validate mirrors the domain normalizer + validators to avoid 500s.
/// </summary>
public sealed class OccupationalPyramidLevelPatchApplierTests
{
    private static OccupationalPyramidLevelPatchState NewState() =>
        OccupationalPyramidLevelPatchState.From(
            OccupationalPyramidLevel.Create("OPL-1", "Nivel Estrategico", 1, "Descripcion base"));

    private static OccupationalPyramidLevelPatchOperation Op(string op, string path, object? value) =>
        new(op, path, null, value is null ? null : JsonSerializer.SerializeToElement(value));

    [Fact]
    public void Apply_ReplaceCode_MutatesStateAndValidates()
    {
        var state = NewState();

        var result = OccupationalPyramidLevelPatchApplier.Apply(new[] { Op("replace", "/code", "OPL-2") }, state);

        Assert.True(result.IsSuccess);
        Assert.True(state.HasMutation);
        Assert.Equal("OPL-2", state.Code);
        Assert.True(OccupationalPyramidLevelPatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_ReplaceName_IsSupported()
    {
        var state = NewState();

        var result = OccupationalPyramidLevelPatchApplier.Apply(new[] { Op("replace", "/name", "Nivel Nuevo") }, state);

        Assert.True(result.IsSuccess);
        Assert.Equal("Nivel Nuevo", state.Name);
    }

    [Fact]
    public void Apply_ReplaceLevelOrder_AsNumber_IsSupported()
    {
        var state = NewState();

        var result = OccupationalPyramidLevelPatchApplier.Apply(new[] { Op("replace", "/levelOrder", 5) }, state);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, state.LevelOrder);
        Assert.True(OccupationalPyramidLevelPatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_ReplaceDescription_WithNull_ClearsIt()
    {
        var state = NewState();

        var result = OccupationalPyramidLevelPatchApplier.Apply(new[] { Op("replace", "/description", null) }, state);

        Assert.True(result.IsSuccess);
        Assert.True(state.HasMutation);
        Assert.Null(state.Description);
    }

    [Fact]
    public void Apply_RemoveDescription_ClearsIt()
    {
        var state = NewState();

        var result = OccupationalPyramidLevelPatchApplier.Apply(new[] { Op("remove", "/description", null) }, state);

        Assert.True(result.IsSuccess);
        Assert.True(state.HasMutation);
        Assert.Null(state.Description);
    }

    [Fact]
    public void Apply_IsActivePath_IsRejected()
    {
        var state = NewState();

        var result = OccupationalPyramidLevelPatchApplier.Apply(new[] { Op("replace", "/isActive", false) }, state);

        Assert.True(result.IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_ConcurrencyTokenPath_IsRejected()
    {
        var state = NewState();

        var result = OccupationalPyramidLevelPatchApplier.Apply(new[] { Op("replace", "/concurrencyToken", Guid.NewGuid().ToString()) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_RemoveCode_IsRejected()
    {
        var state = NewState();

        var result = OccupationalPyramidLevelPatchApplier.Apply(new[] { Op("remove", "/code", null) }, state);

        Assert.True(result.IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_LevelOrderAsString_IsRejected()
    {
        var state = NewState();

        var result = OccupationalPyramidLevelPatchApplier.Apply(new[] { Op("replace", "/levelOrder", "5") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_DescriptionAsNumber_IsRejected()
    {
        var state = NewState();

        var result = OccupationalPyramidLevelPatchApplier.Apply(new[] { Op("replace", "/description", 42) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_ReadOnlyFields_AreRejected()
    {
        var state = NewState();

        Assert.True(OccupationalPyramidLevelPatchApplier.Apply(new[] { Op("replace", "/id", Guid.NewGuid().ToString()) }, state).IsFailure);
        Assert.True(OccupationalPyramidLevelPatchApplier.Apply(new[] { Op("replace", "/companyId", Guid.NewGuid().ToString()) }, state).IsFailure);
        Assert.True(OccupationalPyramidLevelPatchApplier.Apply(new[] { Op("replace", "/createdAtUtc", DateTime.UtcNow.ToString("O")) }, state).IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_UnsupportedOperation_IsRejected()
    {
        var state = NewState();

        var result = OccupationalPyramidLevelPatchApplier.Apply(new[] { Op("move", "/name", "x") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_IsRejected()
    {
        var state = NewState();

        var result = OccupationalPyramidLevelPatchApplier.Apply(new[] { Op("replace", "/name/sub", "x") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_UnknownPath_IsRejected()
    {
        var state = NewState();

        var result = OccupationalPyramidLevelPatchApplier.Apply(new[] { Op("replace", "/levelName", "x") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Validate_CodeWithInvalidFormat_Fails()
    {
        var state = NewState();

        var apply = OccupationalPyramidLevelPatchApplier.Apply(new[] { Op("replace", "/code", "bad code!") }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(OccupationalPyramidLevelPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_NameEmpty_Fails()
    {
        var state = NewState();

        var apply = OccupationalPyramidLevelPatchApplier.Apply(new[] { Op("replace", "/name", "   ") }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(OccupationalPyramidLevelPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_NameTooLong_Fails()
    {
        var state = NewState();

        var apply = OccupationalPyramidLevelPatchApplier.Apply(new[] { Op("replace", "/name", new string('z', 121)) }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(OccupationalPyramidLevelPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_LevelOrderZeroOrNegative_Fails()
    {
        var state = NewState();

        var apply = OccupationalPyramidLevelPatchApplier.Apply(new[] { Op("replace", "/levelOrder", 0) }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(OccupationalPyramidLevelPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_DescriptionTooLong_Fails()
    {
        var state = NewState();

        var apply = OccupationalPyramidLevelPatchApplier.Apply(new[] { Op("replace", "/description", new string('z', 501)) }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(OccupationalPyramidLevelPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = NewState();

        var result = OccupationalPyramidLevelPatchApplier.Apply(Array.Empty<OccupationalPyramidLevelPatchOperation>(), state);

        Assert.True(result.IsSuccess);
        Assert.False(state.HasMutation);
    }
}
