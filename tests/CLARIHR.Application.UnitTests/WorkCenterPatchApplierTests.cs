using System.Text.Json;
using CLARIHR.Application.Features.Locations.WorkCenters;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the scalar-only JSON Patch allow-list of <see cref="WorkCenterPatchApplier"/>
/// (the novel logic added when WorkCenters was aligned to the canonical RFC-6902 PATCH). Locks in
/// the scalar-only decision (no <c>/isActive</c>, no <c>/workCenterTypePublicId</c> /
/// <c>/locationGroupPublicId</c> — those go through PUT / reassign-group / activate-inactivate), the
/// If-Match-only concurrency contract, and the decimal/remove handling for the geo coordinates.
/// </summary>
public sealed class WorkCenterPatchApplierTests
{
    private static WorkCenterPatchState NewState() =>
        WorkCenterPatchState.From(new WorkCenterResponse(
            Id: Guid.NewGuid(),
            Code: "WC-001",
            Name: "Centro",
            WorkCenterTypeId: Guid.NewGuid(),
            WorkCenterTypeCode: "T1",
            WorkCenterTypeName: "Tipo",
            LocationGroupId: Guid.NewGuid(),
            LocationGroupCode: "G1",
            LocationGroupName: "Grupo",
            LocationGroupLevelOrder: 1,
            Address: "Calle 1",
            GeoLat: 13.7m,
            GeoLong: -89.2m,
            Phone: "1234",
            Email: "a@b.com",
            Notes: "Nota",
            IsActive: true,
            ConcurrencyToken: Guid.NewGuid(),
            CreatedAtUtc: DateTime.UtcNow,
            ModifiedAtUtc: null));

    private static WorkCenterPatchOperation Op(string op, string path, object? value) =>
        new(op, path, null, value is null ? null : JsonSerializer.SerializeToElement(value));

    [Fact]
    public void Apply_ReplaceName_MutatesStateAndValidates()
    {
        var state = NewState();

        var result = WorkCenterPatchApplier.Apply(new[] { Op("replace", "/name", "Nuevo Centro") }, state);

        Assert.True(result.IsSuccess);
        Assert.True(state.HasMutation);
        Assert.Equal("Nuevo Centro", state.Name);
        Assert.True(WorkCenterPatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_ReplaceGeoLatNumber_SetsDecimal()
    {
        var state = NewState();

        var result = WorkCenterPatchApplier.Apply(new[] { Op("replace", "/geoLat", 14.5m) }, state);

        Assert.True(result.IsSuccess);
        Assert.Equal(14.5m, state.GeoLat);
    }

    [Fact]
    public void Apply_GeoLatAsString_IsRejected()
    {
        var state = NewState();

        var result = WorkCenterPatchApplier.Apply(new[] { Op("replace", "/geoLat", "not-a-number") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_RemoveGeoLong_SetsNull()
    {
        var state = NewState();

        var result = WorkCenterPatchApplier.Apply(new[] { Op("remove", "/geoLong", null) }, state);

        Assert.True(result.IsSuccess);
        Assert.Null(state.GeoLong);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_IsActivePath_IsRejected()
    {
        var state = NewState();

        var result = WorkCenterPatchApplier.Apply(new[] { Op("replace", "/isActive", false) }, state);

        Assert.True(result.IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_TypeOrGroupPath_IsRejected()
    {
        var state = NewState();

        Assert.True(WorkCenterPatchApplier.Apply(new[] { Op("replace", "/workCenterTypePublicId", Guid.NewGuid().ToString()) }, state).IsFailure);
        Assert.True(WorkCenterPatchApplier.Apply(new[] { Op("replace", "/locationGroupPublicId", Guid.NewGuid().ToString()) }, state).IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_ConcurrencyTokenPath_IsRejected()
    {
        var state = NewState();

        var result = WorkCenterPatchApplier.Apply(new[] { Op("replace", "/concurrencyToken", Guid.NewGuid().ToString()) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_IsRejected()
    {
        var state = NewState();

        var result = WorkCenterPatchApplier.Apply(new[] { Op("move", "/name", "X") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_IsRejected()
    {
        var state = NewState();

        var result = WorkCenterPatchApplier.Apply(new[] { Op("replace", "/name/sub", "X") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_UnknownPath_IsRejected()
    {
        var state = NewState();

        var result = WorkCenterPatchApplier.Apply(new[] { Op("replace", "/unknownField", "X") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_RemoveRequiredCode_IsRejected()
    {
        var state = NewState();

        var result = WorkCenterPatchApplier.Apply(new[] { Op("remove", "/code", null) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Validate_InvalidEmail_Fails()
    {
        var state = NewState();

        var apply = WorkCenterPatchApplier.Apply(new[] { Op("replace", "/email", "not-an-email") }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(WorkCenterPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_GeoLatOutOfRange_Fails()
    {
        var state = NewState();

        var apply = WorkCenterPatchApplier.Apply(new[] { Op("replace", "/geoLat", 200m) }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(WorkCenterPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = NewState();

        var result = WorkCenterPatchApplier.Apply(Array.Empty<WorkCenterPatchOperation>(), state);

        Assert.True(result.IsSuccess);
        Assert.False(state.HasMutation);
    }
}
