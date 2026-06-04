using System.Text.Json;
using CLARIHR.Application.Features.Locations.WorkCenterTypes;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the scalar-only JSON Patch allow-list of <see cref="WorkCenterTypePatchApplier"/>
/// (the novel logic added when WorkCenterTypes was aligned to the canonical RFC-6902 PATCH). Locks in
/// the scalar-only decision (no <c>/isActive</c> — that goes through activate/inactivate), the
/// If-Match-only concurrency contract, and the boolean-flag handling.
/// </summary>
public sealed class WorkCenterTypePatchApplierTests
{
    private static WorkCenterTypePatchState NewState() =>
        WorkCenterTypePatchState.From(new WorkCenterTypeResponse(
            Id: Guid.NewGuid(),
            Code: "WCT-001",
            Name: "Agencia",
            RequiresAddress: true,
            RequiresGeo: false,
            AllowsBiometric: true,
            IsActive: true,
            ConcurrencyToken: Guid.NewGuid(),
            CreatedAtUtc: DateTime.UtcNow,
            ModifiedAtUtc: null));

    private static WorkCenterTypePatchOperation Op(string op, string path, object? value) =>
        new(op, path, null, value is null ? null : JsonSerializer.SerializeToElement(value));

    [Fact]
    public void Apply_ReplaceName_MutatesStateAndValidates()
    {
        var state = NewState();

        var result = WorkCenterTypePatchApplier.Apply(new[] { Op("replace", "/name", "Sucursal") }, state);

        Assert.True(result.IsSuccess);
        Assert.True(state.HasMutation);
        Assert.Equal("Sucursal", state.Name);
        Assert.True(WorkCenterTypePatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_ReplaceRequiresAddressBool_SetsFlag()
    {
        var state = NewState();

        var result = WorkCenterTypePatchApplier.Apply(new[] { Op("replace", "/requiresAddress", false) }, state);

        Assert.True(result.IsSuccess);
        Assert.False(state.RequiresAddress);
    }

    [Fact]
    public void Apply_ReplaceRequiresGeoBoolString_SetsFlag()
    {
        var state = NewState();

        var result = WorkCenterTypePatchApplier.Apply(new[] { Op("replace", "/requiresGeo", "true") }, state);

        Assert.True(result.IsSuccess);
        Assert.True(state.RequiresGeo);
    }

    [Fact]
    public void Apply_RequiresAddressAsNumber_IsRejected()
    {
        var state = NewState();

        var result = WorkCenterTypePatchApplier.Apply(new[] { Op("replace", "/requiresAddress", 1) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_IsActivePath_IsRejected()
    {
        var state = NewState();

        var result = WorkCenterTypePatchApplier.Apply(new[] { Op("replace", "/isActive", false) }, state);

        Assert.True(result.IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_ConcurrencyTokenPath_IsRejected()
    {
        var state = NewState();

        var result = WorkCenterTypePatchApplier.Apply(new[] { Op("replace", "/concurrencyToken", Guid.NewGuid().ToString()) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_IsRejected()
    {
        var state = NewState();

        var result = WorkCenterTypePatchApplier.Apply(new[] { Op("move", "/name", "X") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_IsRejected()
    {
        var state = NewState();

        var result = WorkCenterTypePatchApplier.Apply(new[] { Op("replace", "/name/sub", "X") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_UnknownPath_IsRejected()
    {
        var state = NewState();

        var result = WorkCenterTypePatchApplier.Apply(new[] { Op("replace", "/unknownField", "X") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_RemoveRequiredCode_IsRejected()
    {
        var state = NewState();

        var result = WorkCenterTypePatchApplier.Apply(new[] { Op("remove", "/code", null) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Validate_BlankNameAfterReplace_Fails()
    {
        var state = NewState();

        var apply = WorkCenterTypePatchApplier.Apply(new[] { Op("replace", "/name", "   ") }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(WorkCenterTypePatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = NewState();

        var result = WorkCenterTypePatchApplier.Apply(Array.Empty<WorkCenterTypePatchOperation>(), state);

        Assert.True(result.IsSuccess);
        Assert.False(state.HasMutation);
    }
}
