using System.Text.Json;
using CLARIHR.Application.Features.DocumentTypeCatalogs;
using CLARIHR.Domain.DocumentTypeCatalogs;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the scalar JSON Patch allow-list of <see cref="DocumentTypeCatalogItemPatchApplier"/>
/// (the novel logic added when DocumentTypeCatalogs was aligned to the canonical RFC-6902 PATCH — the
/// Ola 3 backoffice pilot). Patchable = <c>/code</c>, <c>/name</c>, <c>/sortOrder</c>. Activation state
/// changes go through <c>/activate</c> and <c>/inactivate</c>; the concurrency token travels in the
/// <c>If-Match</c> header; id/timestamps are read-only. Validate mirrors the domain normalizer (trim +
/// max length) so an invalid patched value is a 400, never a 500.
/// </summary>
public sealed class DocumentTypeCatalogItemPatchApplierTests
{
    private static DocumentTypeCatalogItemPatchState NewState() =>
        DocumentTypeCatalogItemPatchState.From(DocumentTypeCatalogItem.Create("DOC", "Document", 10));

    private static DocumentTypeCatalogItemPatchOperation Op(string op, string path, object? value) =>
        new(op, path, null, value is null ? null : JsonSerializer.SerializeToElement(value));

    [Fact]
    public void Apply_ReplaceName_MutatesStateAndValidates()
    {
        var state = NewState();

        var result = DocumentTypeCatalogItemPatchApplier.Apply(new[] { Op("replace", "/name", "Renamed") }, state);

        Assert.True(result.IsSuccess);
        Assert.True(state.HasMutation);
        Assert.Equal("Renamed", state.Name);
        Assert.True(DocumentTypeCatalogItemPatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_ReplaceCode_MutatesStateAndValidates()
    {
        var state = NewState();

        var result = DocumentTypeCatalogItemPatchApplier.Apply(new[] { Op("replace", "/code", "DOC_2") }, state);

        Assert.True(result.IsSuccess);
        Assert.Equal("DOC_2", state.Code);
        Assert.True(DocumentTypeCatalogItemPatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_ReplaceSortOrder_AsNumber_IsSupported()
    {
        var state = NewState();

        var result = DocumentTypeCatalogItemPatchApplier.Apply(new[] { Op("replace", "/sortOrder", 42) }, state);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, state.SortOrder);
        Assert.True(DocumentTypeCatalogItemPatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_AddName_IsSupported()
    {
        var state = NewState();

        var result = DocumentTypeCatalogItemPatchApplier.Apply(new[] { Op("add", "/name", "Added") }, state);

        Assert.True(result.IsSuccess);
        Assert.Equal("Added", state.Name);
    }

    [Fact]
    public void Apply_SortOrderAsString_IsRejected()
    {
        var state = NewState();

        var result = DocumentTypeCatalogItemPatchApplier.Apply(new[] { Op("replace", "/sortOrder", "5") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_NameAsNumber_IsRejected()
    {
        var state = NewState();

        var result = DocumentTypeCatalogItemPatchApplier.Apply(new[] { Op("replace", "/name", 42) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_RemoveCode_IsRejected()
    {
        var state = NewState();

        var result = DocumentTypeCatalogItemPatchApplier.Apply(new[] { Op("remove", "/code", null) }, state);

        Assert.True(result.IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_IsActivePath_IsRejected()
    {
        var state = NewState();

        // Activation state goes through /activate and /inactivate, never this patch.
        var result = DocumentTypeCatalogItemPatchApplier.Apply(new[] { Op("replace", "/isActive", false) }, state);

        Assert.True(result.IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_ConcurrencyTokenPath_IsRejected()
    {
        var state = NewState();

        var result = DocumentTypeCatalogItemPatchApplier.Apply(new[] { Op("replace", "/concurrencyToken", Guid.NewGuid().ToString()) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_ReadOnlyFields_AreRejected()
    {
        var state = NewState();

        Assert.True(DocumentTypeCatalogItemPatchApplier.Apply(new[] { Op("replace", "/id", Guid.NewGuid().ToString()) }, state).IsFailure);
        Assert.True(DocumentTypeCatalogItemPatchApplier.Apply(new[] { Op("replace", "/createdAtUtc", DateTime.UtcNow.ToString("O")) }, state).IsFailure);
        Assert.True(DocumentTypeCatalogItemPatchApplier.Apply(new[] { Op("replace", "/modifiedAtUtc", DateTime.UtcNow.ToString("O")) }, state).IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_UnsupportedOperation_IsRejected()
    {
        var state = NewState();

        var result = DocumentTypeCatalogItemPatchApplier.Apply(new[] { Op("move", "/name", "x") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_IsRejected()
    {
        var state = NewState();

        var result = DocumentTypeCatalogItemPatchApplier.Apply(new[] { Op("replace", "/name/sub", "x") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_UnknownPath_IsRejected()
    {
        var state = NewState();

        var result = DocumentTypeCatalogItemPatchApplier.Apply(new[] { Op("replace", "/description", "x") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Validate_CodeWithInvalidFormat_Fails()
    {
        var state = NewState();

        var apply = DocumentTypeCatalogItemPatchApplier.Apply(new[] { Op("replace", "/code", "bad code!") }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(DocumentTypeCatalogItemPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_NameEmpty_Fails()
    {
        var state = NewState();

        var apply = DocumentTypeCatalogItemPatchApplier.Apply(new[] { Op("replace", "/name", "   ") }, state);
        Assert.True(apply.IsSuccess);

        // Whitespace-only name trims to empty in the domain normalizer; must be a 400, not a 500.
        Assert.True(DocumentTypeCatalogItemPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_NameTooLong_Fails()
    {
        var state = NewState();

        var apply = DocumentTypeCatalogItemPatchApplier.Apply(new[] { Op("replace", "/name", new string('z', 201)) }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(DocumentTypeCatalogItemPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_SortOrderNegative_Fails()
    {
        var state = NewState();

        var apply = DocumentTypeCatalogItemPatchApplier.Apply(new[] { Op("replace", "/sortOrder", -1) }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(DocumentTypeCatalogItemPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = NewState();

        var result = DocumentTypeCatalogItemPatchApplier.Apply(Array.Empty<DocumentTypeCatalogItemPatchOperation>(), state);

        Assert.True(result.IsSuccess);
        Assert.False(state.HasMutation);
    }
}
