using System.Text.Json;
using CLARIHR.Application.Features.OrgUnits;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the descriptive-only JSON Patch allow-list of <see cref="OrgUnitPatchApplier"/>
/// (the novel logic added when OrgUnits was aligned to the canonical RFC-6902 PATCH). Patchable =
/// <c>/name</c>, <c>/sortOrder</c>, <c>/description</c>. The code (uniqueness), the
/// type/functional-area/manager references and the cost center (resolved/validated) are changed via
/// PUT; the parent via <c>/move</c>; activation via activate/inactivate. Locks that boundary in.
/// </summary>
public sealed class OrgUnitPatchApplierTests
{
    private static OrgUnitPatchState NewState() =>
        OrgUnitPatchState.From(new OrgUnitResponse(
            Id: Guid.NewGuid(),
            Code: "OU-001",
            Name: "Direccion",
            OrgUnitType: new OrgUnitCatalogReferenceResponse(Guid.NewGuid(), "DIR", "Direccion"),
            FunctionalArea: null,
            Parent: null,
            SortOrder: 1,
            Description: "Desc",
            CostCenterCode: "CC-001",
            ManagerEmployeeId: null,
            IsActive: true,
            ConcurrencyToken: Guid.NewGuid(),
            CreatedAtUtc: DateTime.UtcNow,
            ModifiedAtUtc: null));

    private static OrgUnitPatchOperation Op(string op, string path, object? value) =>
        new(op, path, null, value is null ? null : JsonSerializer.SerializeToElement(value));

    [Fact]
    public void Apply_ReplaceName_MutatesStateAndValidates()
    {
        var state = NewState();

        var result = OrgUnitPatchApplier.Apply(new[] { Op("replace", "/name", "Gerencia") }, state);

        Assert.True(result.IsSuccess);
        Assert.True(state.HasMutation);
        Assert.Equal("Gerencia", state.Name);
        Assert.True(OrgUnitPatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_ReplaceSortOrderNumber_SetsValue()
    {
        var state = NewState();

        var result = OrgUnitPatchApplier.Apply(new[] { Op("replace", "/sortOrder", 5) }, state);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, state.SortOrder);
    }

    [Fact]
    public void Apply_SortOrderAsString_IsRejected()
    {
        var state = NewState();

        var result = OrgUnitPatchApplier.Apply(new[] { Op("replace", "/sortOrder", "five") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_RemoveDescription_SetsNull()
    {
        var state = NewState();

        var result = OrgUnitPatchApplier.Apply(new[] { Op("remove", "/description", null) }, state);

        Assert.True(result.IsSuccess);
        Assert.Null(state.Description);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_NonPatchableFields_AreRejected()
    {
        var state = NewState();

        Assert.True(OrgUnitPatchApplier.Apply(new[] { Op("replace", "/code", "X") }, state).IsFailure);
        Assert.True(OrgUnitPatchApplier.Apply(new[] { Op("replace", "/orgUnitTypePublicId", Guid.NewGuid().ToString()) }, state).IsFailure);
        Assert.True(OrgUnitPatchApplier.Apply(new[] { Op("replace", "/costCenterCode", "CC-9") }, state).IsFailure);
        Assert.True(OrgUnitPatchApplier.Apply(new[] { Op("replace", "/parentPublicId", Guid.NewGuid().ToString()) }, state).IsFailure);
        Assert.True(OrgUnitPatchApplier.Apply(new[] { Op("replace", "/isActive", false) }, state).IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_ConcurrencyTokenPath_IsRejected()
    {
        var state = NewState();

        var result = OrgUnitPatchApplier.Apply(new[] { Op("replace", "/concurrencyToken", Guid.NewGuid().ToString()) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_IsRejected()
    {
        var state = NewState();

        var result = OrgUnitPatchApplier.Apply(new[] { Op("move", "/name", "X") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_IsRejected()
    {
        var state = NewState();

        var result = OrgUnitPatchApplier.Apply(new[] { Op("replace", "/name/sub", "X") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_UnknownPath_IsRejected()
    {
        var state = NewState();

        var result = OrgUnitPatchApplier.Apply(new[] { Op("replace", "/unknownField", "X") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_RemoveRequiredName_IsRejected()
    {
        var state = NewState();

        var result = OrgUnitPatchApplier.Apply(new[] { Op("remove", "/name", null) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Validate_NegativeSortOrder_Fails()
    {
        var state = NewState();

        var apply = OrgUnitPatchApplier.Apply(new[] { Op("replace", "/sortOrder", -1) }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(OrgUnitPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = NewState();

        var result = OrgUnitPatchApplier.Apply(Array.Empty<OrgUnitPatchOperation>(), state);

        Assert.True(result.IsSuccess);
        Assert.False(state.HasMutation);
    }
}
