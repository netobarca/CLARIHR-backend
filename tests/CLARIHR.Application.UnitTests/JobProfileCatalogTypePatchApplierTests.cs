using System.Text.Json;
using CLARIHR.Application.Features.JobProfileCatalogTypes;
using CLARIHR.Domain.CatalogTypes;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the scalar JSON Patch allow-list of <see cref="JobProfileCatalogTypePatchApplier"/>
/// (Ola 3 backoffice). Patchable = <c>/name</c>, <c>/sortOrder</c>. The <c>/code</c> key is IMMUTABLE
/// (rejected); activation state changes go through <c>/activate</c> and <c>/inactivate</c>; the
/// concurrency token travels in the <c>If-Match</c> header; id/timestamps are read-only. Validate
/// mirrors the domain normalizer (trim + max length) so an invalid patched value is a 400, never a 500.
/// </summary>
public sealed class JobProfileCatalogTypePatchApplierTests
{
    private static JobProfileCatalogTypePatchState NewState() =>
        JobProfileCatalogTypePatchState.From(CatalogTypeDescriptor.Create("DOC", "Document", 10));

    private static JobProfileCatalogTypePatchOperation Op(string op, string path, object? value) =>
        new(op, path, null, value is null ? null : JsonSerializer.SerializeToElement(value));

    [Fact]
    public void Apply_ReplaceName_MutatesStateAndValidates()
    {
        var state = NewState();

        var result = JobProfileCatalogTypePatchApplier.Apply(new[] { Op("replace", "/name", "Renamed") }, state);

        Assert.True(result.IsSuccess);
        Assert.True(state.HasMutation);
        Assert.Equal("Renamed", state.Name);
        Assert.True(JobProfileCatalogTypePatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_ReplaceSortOrder_AsNumber_IsSupported()
    {
        var state = NewState();

        var result = JobProfileCatalogTypePatchApplier.Apply(new[] { Op("replace", "/sortOrder", 42) }, state);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, state.SortOrder);
        Assert.True(JobProfileCatalogTypePatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_AddName_IsSupported()
    {
        var state = NewState();

        var result = JobProfileCatalogTypePatchApplier.Apply(new[] { Op("add", "/name", "Added") }, state);

        Assert.True(result.IsSuccess);
        Assert.Equal("Added", state.Name);
    }

    [Fact]
    public void Apply_CodePath_IsRejectedAsImmutable()
    {
        var state = NewState();

        // The Code key is immutable once created.
        var result = JobProfileCatalogTypePatchApplier.Apply(new[] { Op("replace", "/code", "NEW_CODE") }, state);

        Assert.True(result.IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_SortOrderAsString_IsRejected()
    {
        var state = NewState();

        var result = JobProfileCatalogTypePatchApplier.Apply(new[] { Op("replace", "/sortOrder", "5") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_NameAsNumber_IsRejected()
    {
        var state = NewState();

        var result = JobProfileCatalogTypePatchApplier.Apply(new[] { Op("replace", "/name", 42) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_RemoveName_IsRejected()
    {
        var state = NewState();

        var result = JobProfileCatalogTypePatchApplier.Apply(new[] { Op("remove", "/name", null) }, state);

        Assert.True(result.IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_IsActivePath_IsRejected()
    {
        var state = NewState();

        var result = JobProfileCatalogTypePatchApplier.Apply(new[] { Op("replace", "/isActive", false) }, state);

        Assert.True(result.IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_ConcurrencyTokenPath_IsRejected()
    {
        var state = NewState();

        var result = JobProfileCatalogTypePatchApplier.Apply(new[] { Op("replace", "/concurrencyToken", Guid.NewGuid().ToString()) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_ReadOnlyFields_AreRejected()
    {
        var state = NewState();

        Assert.True(JobProfileCatalogTypePatchApplier.Apply(new[] { Op("replace", "/id", Guid.NewGuid().ToString()) }, state).IsFailure);
        Assert.True(JobProfileCatalogTypePatchApplier.Apply(new[] { Op("replace", "/createdAtUtc", DateTime.UtcNow.ToString("O")) }, state).IsFailure);
        Assert.True(JobProfileCatalogTypePatchApplier.Apply(new[] { Op("replace", "/modifiedAtUtc", DateTime.UtcNow.ToString("O")) }, state).IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_UnsupportedOperation_IsRejected()
    {
        var state = NewState();

        var result = JobProfileCatalogTypePatchApplier.Apply(new[] { Op("move", "/name", "x") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_IsRejected()
    {
        var state = NewState();

        var result = JobProfileCatalogTypePatchApplier.Apply(new[] { Op("replace", "/name/sub", "x") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_UnknownPath_IsRejected()
    {
        var state = NewState();

        var result = JobProfileCatalogTypePatchApplier.Apply(new[] { Op("replace", "/description", "x") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Validate_NameEmpty_Fails()
    {
        var state = NewState();

        var apply = JobProfileCatalogTypePatchApplier.Apply(new[] { Op("replace", "/name", "   ") }, state);
        Assert.True(apply.IsSuccess);

        // Whitespace-only name trims to empty in the domain normalizer; must be a 400, not a 500.
        Assert.True(JobProfileCatalogTypePatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_NameTooLong_Fails()
    {
        var state = NewState();

        var apply = JobProfileCatalogTypePatchApplier.Apply(new[] { Op("replace", "/name", new string('z', 201)) }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(JobProfileCatalogTypePatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_SortOrderNegative_Fails()
    {
        var state = NewState();

        var apply = JobProfileCatalogTypePatchApplier.Apply(new[] { Op("replace", "/sortOrder", -1) }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(JobProfileCatalogTypePatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = NewState();

        var result = JobProfileCatalogTypePatchApplier.Apply(Array.Empty<JobProfileCatalogTypePatchOperation>(), state);

        Assert.True(result.IsSuccess);
        Assert.False(state.HasMutation);
    }
}
