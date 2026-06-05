using System.Text.Json;
using CLARIHR.Application.Features.Banks;
using CLARIHR.Domain.Banks;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the scalar JSON Patch allow-list of <see cref="BankCatalogItemPatchApplier"/>
/// (Ola 3 backoffice — the last catalog). Patchable = <c>/code</c>, <c>/name</c>, and the optional
/// <c>/alias</c>, <c>/swiftCode</c>, <c>/routingCode</c> (string or null/remove to clear), plus
/// <c>/sortOrder</c>. The <c>/countryCode</c> scope is immutable (rejected); activation state goes
/// through <c>/activate</c> and <c>/inactivate</c>; the concurrency token travels in <c>If-Match</c>;
/// id/timestamps are read-only. Validate mirrors the domain normalizer to avoid 500s.
/// </summary>
public sealed class BankCatalogItemPatchApplierTests
{
    private static BankCatalogItemPatchState NewState() =>
        BankCatalogItemPatchState.From(
            BankCatalogItem.Create(1, "SV", "BANCO", "Banco", "Ali", "SWIFT01", "ROUT01", isActive: true, sortOrder: 10));

    private static BankCatalogItemPatchOperation Op(string op, string path, object? value) =>
        new(op, path, null, value is null ? null : JsonSerializer.SerializeToElement(value));

    [Fact]
    public void Apply_ReplaceCode_MutatesStateAndValidates()
    {
        var state = NewState();

        var result = BankCatalogItemPatchApplier.Apply(new[] { Op("replace", "/code", "BANCO_2") }, state);

        Assert.True(result.IsSuccess);
        Assert.True(state.HasMutation);
        Assert.Equal("BANCO_2", state.Code);
        Assert.True(BankCatalogItemPatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_ReplaceName_IsSupported()
    {
        var state = NewState();

        var result = BankCatalogItemPatchApplier.Apply(new[] { Op("replace", "/name", "Banco Nuevo") }, state);

        Assert.True(result.IsSuccess);
        Assert.Equal("Banco Nuevo", state.Name);
    }

    [Fact]
    public void Apply_ReplaceSortOrder_AsNumber_IsSupported()
    {
        var state = NewState();

        var result = BankCatalogItemPatchApplier.Apply(new[] { Op("replace", "/sortOrder", 42) }, state);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, state.SortOrder);
        Assert.True(BankCatalogItemPatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_ReplaceAlias_WithString_IsSupported()
    {
        var state = NewState();

        var result = BankCatalogItemPatchApplier.Apply(new[] { Op("replace", "/alias", "Nuevo Alias") }, state);

        Assert.True(result.IsSuccess);
        Assert.Equal("Nuevo Alias", state.Alias);
    }

    [Fact]
    public void Apply_ReplaceSwiftCode_WithNull_ClearsIt()
    {
        var state = NewState();

        var result = BankCatalogItemPatchApplier.Apply(new[] { Op("replace", "/swiftCode", null) }, state);

        Assert.True(result.IsSuccess);
        Assert.True(state.HasMutation);
        Assert.Null(state.SwiftCode);
    }

    [Fact]
    public void Apply_RemoveRoutingCode_ClearsIt()
    {
        var state = NewState();

        var result = BankCatalogItemPatchApplier.Apply(new[] { Op("remove", "/routingCode", null) }, state);

        Assert.True(result.IsSuccess);
        Assert.True(state.HasMutation);
        Assert.Null(state.RoutingCode);
    }

    [Fact]
    public void Apply_CountryCodePath_IsRejectedAsImmutable()
    {
        var state = NewState();

        var result = BankCatalogItemPatchApplier.Apply(new[] { Op("replace", "/countryCode", "US") }, state);

        Assert.True(result.IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveCode_IsRejected()
    {
        var state = NewState();

        var result = BankCatalogItemPatchApplier.Apply(new[] { Op("remove", "/code", null) }, state);

        Assert.True(result.IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_IsActivePath_IsRejected()
    {
        var state = NewState();

        var result = BankCatalogItemPatchApplier.Apply(new[] { Op("replace", "/isActive", false) }, state);

        Assert.True(result.IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_ConcurrencyTokenPath_IsRejected()
    {
        var state = NewState();

        var result = BankCatalogItemPatchApplier.Apply(new[] { Op("replace", "/concurrencyToken", Guid.NewGuid().ToString()) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_SortOrderAsString_IsRejected()
    {
        var state = NewState();

        var result = BankCatalogItemPatchApplier.Apply(new[] { Op("replace", "/sortOrder", "5") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_AliasAsNumber_IsRejected()
    {
        var state = NewState();

        var result = BankCatalogItemPatchApplier.Apply(new[] { Op("replace", "/alias", 42) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_ReadOnlyFields_AreRejected()
    {
        var state = NewState();

        Assert.True(BankCatalogItemPatchApplier.Apply(new[] { Op("replace", "/id", Guid.NewGuid().ToString()) }, state).IsFailure);
        Assert.True(BankCatalogItemPatchApplier.Apply(new[] { Op("replace", "/createdAtUtc", DateTime.UtcNow.ToString("O")) }, state).IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_UnsupportedOperation_IsRejected()
    {
        var state = NewState();

        var result = BankCatalogItemPatchApplier.Apply(new[] { Op("move", "/name", "x") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_IsRejected()
    {
        var state = NewState();

        var result = BankCatalogItemPatchApplier.Apply(new[] { Op("replace", "/name/sub", "x") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_UnknownPath_IsRejected()
    {
        var state = NewState();

        var result = BankCatalogItemPatchApplier.Apply(new[] { Op("replace", "/description", "x") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Validate_CodeWithInvalidFormat_Fails()
    {
        var state = NewState();

        var apply = BankCatalogItemPatchApplier.Apply(new[] { Op("replace", "/code", "bad code!") }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(BankCatalogItemPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_NameEmpty_Fails()
    {
        var state = NewState();

        var apply = BankCatalogItemPatchApplier.Apply(new[] { Op("replace", "/name", "   ") }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(BankCatalogItemPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_AliasTooLong_Fails()
    {
        var state = NewState();

        var apply = BankCatalogItemPatchApplier.Apply(new[] { Op("replace", "/alias", new string('z', 121)) }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(BankCatalogItemPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_SwiftCodeTooLong_Fails()
    {
        var state = NewState();

        var apply = BankCatalogItemPatchApplier.Apply(new[] { Op("replace", "/swiftCode", new string('z', 41)) }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(BankCatalogItemPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_SortOrderNegative_Fails()
    {
        var state = NewState();

        var apply = BankCatalogItemPatchApplier.Apply(new[] { Op("replace", "/sortOrder", -1) }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(BankCatalogItemPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = NewState();

        var result = BankCatalogItemPatchApplier.Apply(Array.Empty<BankCatalogItemPatchOperation>(), state);

        Assert.True(result.IsSuccess);
        Assert.False(state.HasMutation);
    }
}
