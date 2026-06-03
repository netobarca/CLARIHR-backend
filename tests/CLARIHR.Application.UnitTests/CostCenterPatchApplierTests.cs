using System.Text.Json;
using CLARIHR.Application.Features.CostCenters;
using CLARIHR.Domain.CostCenters;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the scalar-only JSON Patch allow-list of <see cref="CostCenterPatchApplier"/>
/// (the novel logic added when CostCenters was aligned to the canonical RFC-6902 PATCH). Locks in
/// the scalar-only decision (no <c>/isActive</c>), the If-Match-only concurrency contract
/// (<c>/concurrencyToken</c> rejected in the body), and the enum/remove handling — so a mechanical
/// replication of the Ola 0 recipe cannot silently regress these.
/// </summary>
public sealed class CostCenterPatchApplierTests
{
    private static CostCenterPatchState NewState() =>
        CostCenterPatchState.From(new CostCenterResponse(
            Id: Guid.NewGuid(),
            CompanyId: Guid.NewGuid(),
            Code: "CC-001",
            Name: "Centro",
            Type: CostCenterType.Mixed,
            PayrollExpenseAccountCode: "5101",
            EmployerContributionAccountCode: null,
            ProvisionAccountCode: null,
            Description: "Desc",
            IsActive: true,
            ConcurrencyToken: Guid.NewGuid(),
            CreatedAtUtc: DateTime.UtcNow,
            ModifiedAtUtc: null));

    private static CostCenterPatchOperation Op(string op, string path, object? value) =>
        new(op, path, null, value is null ? null : JsonSerializer.SerializeToElement(value));

    [Fact]
    public void Apply_ReplaceName_MutatesStateAndValidates()
    {
        var state = NewState();

        var result = CostCenterPatchApplier.Apply(new[] { Op("replace", "/name", "Nuevo Centro") }, state);

        Assert.True(result.IsSuccess);
        Assert.True(state.HasMutation);
        Assert.Equal("Nuevo Centro", state.Name);
        Assert.True(CostCenterPatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_ReplaceType_ParsesEnumCaseInsensitively()
    {
        var state = NewState();

        var result = CostCenterPatchApplier.Apply(new[] { Op("replace", "/type", "salaryexpense") }, state);

        Assert.True(result.IsSuccess);
        Assert.Equal(CostCenterType.SalaryExpense, state.Type);
    }

    [Fact]
    public void Apply_InvalidTypeValue_IsRejected()
    {
        var state = NewState();

        var result = CostCenterPatchApplier.Apply(new[] { Op("replace", "/type", "NotARealType") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_IsActivePath_IsRejectedAndDoesNotMutate()
    {
        var state = NewState();

        var result = CostCenterPatchApplier.Apply(new[] { Op("replace", "/isActive", false) }, state);

        Assert.True(result.IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_ConcurrencyTokenPath_IsRejected()
    {
        var state = NewState();

        var result = CostCenterPatchApplier.Apply(new[] { Op("replace", "/concurrencyToken", Guid.NewGuid().ToString()) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_IsRejected()
    {
        var state = NewState();

        var result = CostCenterPatchApplier.Apply(new[] { Op("move", "/name", "X") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_IsRejected()
    {
        var state = NewState();

        var result = CostCenterPatchApplier.Apply(new[] { Op("replace", "/name/sub", "X") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_UnknownPath_IsRejected()
    {
        var state = NewState();

        var result = CostCenterPatchApplier.Apply(new[] { Op("replace", "/unknownField", "X") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_RemoveRequiredCode_IsRejected()
    {
        var state = NewState();

        var result = CostCenterPatchApplier.Apply(new[] { Op("remove", "/code", null) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_RemoveNullableDescription_SetsNull()
    {
        var state = NewState();

        var result = CostCenterPatchApplier.Apply(new[] { Op("remove", "/description", null) }, state);

        Assert.True(result.IsSuccess);
        Assert.Null(state.Description);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Validate_BlankNameAfterReplace_Fails()
    {
        var state = NewState();

        var apply = CostCenterPatchApplier.Apply(new[] { Op("replace", "/name", "   ") }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(CostCenterPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = NewState();

        var result = CostCenterPatchApplier.Apply(Array.Empty<CostCenterPatchOperation>(), state);

        Assert.True(result.IsSuccess);
        Assert.False(state.HasMutation);
    }
}
