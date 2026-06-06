using System.Text.Json;
using CLARIHR.Application.Features.CompetencyFramework;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the scalar JSON Patch allow-list of <see cref="CompetencyConductPatchApplier"/>
/// (CompetencyFramework canonical v2). Patchable = <c>/competencyPublicId</c>, <c>/competencyTypePublicId</c>,
/// <c>/behaviorLevelPublicId</c> (GUIDs), <c>/description</c> (required string) and <c>/sortOrder</c> (int).
/// Activation state goes through <c>/activate</c> and <c>/inactivate</c>; behaviors through the behaviors
/// endpoint; the concurrency token travels in <c>If-Match</c>; id/companyId/timestamps are read-only.
/// Validate mirrors the domain normalizer + validators to avoid 500s. The state is seeded from the
/// response (which carries the public catalog ids) rather than the entity (which holds internal ids).
/// </summary>
public sealed class CompetencyConductPatchApplierTests
{
    private static readonly Guid Competency = Guid.NewGuid();
    private static readonly Guid CompetencyType = Guid.NewGuid();
    private static readonly Guid BehaviorLevel = Guid.NewGuid();

    private static CompetencyConductPatchState NewState() =>
        CompetencyConductPatchState.From(new CompetencyConductResponse(
            Id: Guid.NewGuid(),
            CompanyId: Guid.NewGuid(),
            CompetencyId: Competency,
            CompetencyCode: "COMP-1",
            CompetencyName: "Competency One",
            CompetencyTypeId: CompetencyType,
            CompetencyTypeCode: "TYPE-1",
            CompetencyTypeName: "Type One",
            BehaviorLevelId: BehaviorLevel,
            BehaviorLevelCode: "LVL-1",
            BehaviorLevelName: "Level One",
            Description: "Conducta base",
            SortOrder: 1,
            IsActive: true,
            Behaviors: Array.Empty<CompetencyConductBehaviorResponse>(),
            ConcurrencyToken: Guid.NewGuid(),
            CreatedAtUtc: DateTime.UtcNow,
            ModifiedAtUtc: null));

    private static CompetencyConductPatchOperation Op(string op, string path, object? value) =>
        new(op, path, null, value is null ? null : JsonSerializer.SerializeToElement(value));

    [Fact]
    public void Apply_ReplaceDescription_MutatesStateAndValidates()
    {
        var state = NewState();

        var result = CompetencyConductPatchApplier.Apply(new[] { Op("replace", "/description", "Conducta nueva") }, state);

        Assert.True(result.IsSuccess);
        Assert.True(state.HasMutation);
        Assert.Equal("Conducta nueva", state.Description);
        Assert.True(CompetencyConductPatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_ReplaceSortOrder_AsNumber_IsSupported()
    {
        var state = NewState();

        var result = CompetencyConductPatchApplier.Apply(new[] { Op("replace", "/sortOrder", 7) }, state);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, state.SortOrder);
        Assert.True(CompetencyConductPatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_ReplaceCompetencyPublicId_AsGuid_IsSupported()
    {
        var state = NewState();
        var replacement = Guid.NewGuid();

        var result = CompetencyConductPatchApplier.Apply(new[] { Op("replace", "/competencyPublicId", replacement) }, state);

        Assert.True(result.IsSuccess);
        Assert.True(state.HasMutation);
        Assert.Equal(replacement, state.CompetencyId);
        Assert.True(CompetencyConductPatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_ReplaceCompetencyTypePublicId_IsSupported()
    {
        var state = NewState();
        var replacement = Guid.NewGuid();

        var result = CompetencyConductPatchApplier.Apply(new[] { Op("replace", "/competencyTypePublicId", replacement) }, state);

        Assert.True(result.IsSuccess);
        Assert.Equal(replacement, state.CompetencyTypeId);
    }

    [Fact]
    public void Apply_ReplaceBehaviorLevelPublicId_IsSupported()
    {
        var state = NewState();
        var replacement = Guid.NewGuid();

        var result = CompetencyConductPatchApplier.Apply(new[] { Op("replace", "/behaviorLevelPublicId", replacement) }, state);

        Assert.True(result.IsSuccess);
        Assert.Equal(replacement, state.BehaviorLevelId);
    }

    [Fact]
    public void Apply_IsActivePath_IsRejected()
    {
        var state = NewState();

        var result = CompetencyConductPatchApplier.Apply(new[] { Op("replace", "/isActive", false) }, state);

        Assert.True(result.IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_ConcurrencyTokenPath_IsRejected()
    {
        var state = NewState();

        var result = CompetencyConductPatchApplier.Apply(new[] { Op("replace", "/concurrencyToken", Guid.NewGuid().ToString()) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_BehaviorsPath_IsRejected()
    {
        var state = NewState();

        var result = CompetencyConductPatchApplier.Apply(new[] { Op("replace", "/behaviors", Array.Empty<object>()) }, state);

        Assert.True(result.IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveDescription_IsRejected()
    {
        var state = NewState();

        var result = CompetencyConductPatchApplier.Apply(new[] { Op("remove", "/description", null) }, state);

        Assert.True(result.IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveCompetencyPublicId_IsRejected()
    {
        var state = NewState();

        var result = CompetencyConductPatchApplier.Apply(new[] { Op("remove", "/competencyPublicId", null) }, state);

        Assert.True(result.IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_CompetencyPublicIdAsNumber_IsRejected()
    {
        var state = NewState();

        var result = CompetencyConductPatchApplier.Apply(new[] { Op("replace", "/competencyPublicId", 42) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_CompetencyPublicIdAsNonGuidString_IsRejected()
    {
        var state = NewState();

        var result = CompetencyConductPatchApplier.Apply(new[] { Op("replace", "/competencyPublicId", "not-a-guid") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_SortOrderAsString_IsRejected()
    {
        var state = NewState();

        var result = CompetencyConductPatchApplier.Apply(new[] { Op("replace", "/sortOrder", "7") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_DescriptionAsNumber_IsRejected()
    {
        var state = NewState();

        var result = CompetencyConductPatchApplier.Apply(new[] { Op("replace", "/description", 42) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_ReadOnlyFields_AreRejected()
    {
        var state = NewState();

        Assert.True(CompetencyConductPatchApplier.Apply(new[] { Op("replace", "/id", Guid.NewGuid().ToString()) }, state).IsFailure);
        Assert.True(CompetencyConductPatchApplier.Apply(new[] { Op("replace", "/companyId", Guid.NewGuid().ToString()) }, state).IsFailure);
        Assert.True(CompetencyConductPatchApplier.Apply(new[] { Op("replace", "/createdAtUtc", DateTime.UtcNow.ToString("O")) }, state).IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_UnsupportedOperation_IsRejected()
    {
        var state = NewState();

        var result = CompetencyConductPatchApplier.Apply(new[] { Op("move", "/description", "x") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_IsRejected()
    {
        var state = NewState();

        var result = CompetencyConductPatchApplier.Apply(new[] { Op("replace", "/description/sub", "x") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_UnknownPath_IsRejected()
    {
        var state = NewState();

        var result = CompetencyConductPatchApplier.Apply(new[] { Op("replace", "/competencyCode", "x") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Validate_DescriptionEmpty_Fails()
    {
        var state = NewState();

        var apply = CompetencyConductPatchApplier.Apply(new[] { Op("replace", "/description", "   ") }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(CompetencyConductPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_DescriptionTooLong_Fails()
    {
        var state = NewState();

        var apply = CompetencyConductPatchApplier.Apply(new[] { Op("replace", "/description", new string('z', 1001)) }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(CompetencyConductPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_SortOrderNegative_Fails()
    {
        var state = NewState();

        var apply = CompetencyConductPatchApplier.Apply(new[] { Op("replace", "/sortOrder", -1) }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(CompetencyConductPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_CompetencyEmptyGuid_Fails()
    {
        var state = NewState();

        var apply = CompetencyConductPatchApplier.Apply(new[] { Op("replace", "/competencyPublicId", Guid.Empty) }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(CompetencyConductPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = NewState();

        var result = CompetencyConductPatchApplier.Apply(Array.Empty<CompetencyConductPatchOperation>(), state);

        Assert.True(result.IsSuccess);
        Assert.False(state.HasMutation);
    }
}
