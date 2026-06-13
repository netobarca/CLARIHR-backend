using System.Text.Json;
using CLARIHR.Application.Features.CostCenters.Types;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the scalar-only JSON Patch allow-list of <see cref="CostCenterTypePatchApplier"/>
/// (mirror of <see cref="WorkCenterTypePatchApplierTests"/>). Locks in the scalar-only decision
/// (no <c>/isActive</c> — that goes through activate/inactivate), the If-Match-only concurrency
/// contract, and the nullable-description handling.
/// </summary>
public sealed class CostCenterTypePatchApplierTests
{
    private static CostCenterTypePatchState NewState() =>
        CostCenterTypePatchState.From(new CostCenterTypeResponse(
            Id: Guid.NewGuid(),
            Code: "SALARY-EXPENSE",
            Name: "Gasto salarial",
            Description: "Gasto de planilla",
            IsActive: true,
            ConcurrencyToken: Guid.NewGuid(),
            CreatedAtUtc: DateTime.UtcNow,
            ModifiedAtUtc: null));

    private static CostCenterTypePatchOperation Op(string op, string path, object? value) =>
        new(op, path, null, value is null ? null : JsonSerializer.SerializeToElement(value));

    [Fact]
    public void Apply_ReplaceName_MutatesStateAndValidates()
    {
        var state = NewState();

        var result = CostCenterTypePatchApplier.Apply(new[] { Op("replace", "/name", "Gasto operativo") }, state);

        Assert.True(result.IsSuccess);
        Assert.True(state.HasMutation);
        Assert.Equal("Gasto operativo", state.Name);
        Assert.True(CostCenterTypePatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_RemoveNullableDescription_SetsNull()
    {
        var state = NewState();

        var result = CostCenterTypePatchApplier.Apply(new[] { Op("remove", "/description", null) }, state);

        Assert.True(result.IsSuccess);
        Assert.Null(state.Description);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_IsActivePath_IsRejected()
    {
        var state = NewState();

        var result = CostCenterTypePatchApplier.Apply(new[] { Op("replace", "/isActive", false) }, state);

        Assert.True(result.IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_ConcurrencyTokenPath_IsRejected()
    {
        var state = NewState();

        var result = CostCenterTypePatchApplier.Apply(new[] { Op("replace", "/concurrencyToken", Guid.NewGuid().ToString()) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_IsRejected()
    {
        var state = NewState();

        var result = CostCenterTypePatchApplier.Apply(new[] { Op("move", "/name", "X") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_IsRejected()
    {
        var state = NewState();

        var result = CostCenterTypePatchApplier.Apply(new[] { Op("replace", "/name/sub", "X") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_UnknownPath_IsRejected()
    {
        var state = NewState();

        var result = CostCenterTypePatchApplier.Apply(new[] { Op("replace", "/unknownField", "X") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_RemoveRequiredCode_IsRejected()
    {
        var state = NewState();

        var result = CostCenterTypePatchApplier.Apply(new[] { Op("remove", "/code", null) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Validate_BlankNameAfterReplace_Fails()
    {
        var state = NewState();

        var apply = CostCenterTypePatchApplier.Apply(new[] { Op("replace", "/name", "   ") }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(CostCenterTypePatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = NewState();

        var result = CostCenterTypePatchApplier.Apply(Array.Empty<CostCenterTypePatchOperation>(), state);

        Assert.True(result.IsSuccess);
        Assert.False(state.HasMutation);
    }
}
