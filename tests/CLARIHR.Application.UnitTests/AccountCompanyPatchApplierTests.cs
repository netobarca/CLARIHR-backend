using System.Text.Json;
using CLARIHR.Application.Features.AccountCompanies;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the scalar JSON Patch allow-list of <see cref="AccountCompanyPatchApplier"/>
/// (the novel logic added when AccountCompanies was aligned to the canonical RFC-6902 PATCH).
/// Patchable = <c>/name</c> (max 150 chars, required, cannot be removed) and
/// <c>/companyTypePublicId</c> (a GUID, or null/remove to clear). Status transitions go through the
/// dedicated <c>/archive</c> and <c>/reactivate</c> actions; the concurrency token travels in the
/// <c>If-Match</c> header; id/slug/country/timestamps are read-only. Locks that boundary in.
/// </summary>
public sealed class AccountCompanyPatchApplierTests
{
    private static AccountCompanyPatchOperation Op(string op, string path, object? value) =>
        new(op, path, null, value is null ? null : JsonSerializer.SerializeToElement(value));

    [Fact]
    public void Apply_ReplaceName_MutatesStateAndValidates()
    {
        var state = new AccountCompanyPatchState();

        var result = AccountCompanyPatchApplier.Apply(new[] { Op("replace", "/name", "Acme Group") }, state);

        Assert.True(result.IsSuccess);
        Assert.True(state.NameSet);
        Assert.True(state.HasMutation);
        Assert.Equal("Acme Group", state.Name);
        Assert.True(AccountCompanyPatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_AddName_IsSupported()
    {
        var state = new AccountCompanyPatchState();

        var result = AccountCompanyPatchApplier.Apply(new[] { Op("add", "/name", "Acme Group") }, state);

        Assert.True(result.IsSuccess);
        Assert.Equal("Acme Group", state.Name);
    }

    [Fact]
    public void Apply_ReplaceCompanyTypePublicId_WithGuid_IsSupported()
    {
        var state = new AccountCompanyPatchState();
        var companyTypeId = Guid.NewGuid();

        var result = AccountCompanyPatchApplier.Apply(new[] { Op("replace", "/companyTypePublicId", companyTypeId.ToString()) }, state);

        Assert.True(result.IsSuccess);
        Assert.True(state.CompanyTypeSet);
        Assert.Equal(companyTypeId, state.CompanyTypePublicId);
    }

    [Fact]
    public void Apply_ReplaceCompanyTypePublicId_WithNull_ClearsIt()
    {
        var state = new AccountCompanyPatchState();

        var result = AccountCompanyPatchApplier.Apply(new[] { Op("replace", "/companyTypePublicId", null) }, state);

        Assert.True(result.IsSuccess);
        Assert.True(state.CompanyTypeSet);
        Assert.Null(state.CompanyTypePublicId);
    }

    [Fact]
    public void Apply_RemoveCompanyTypePublicId_ClearsIt()
    {
        var state = new AccountCompanyPatchState();

        var result = AccountCompanyPatchApplier.Apply(new[] { Op("remove", "/companyTypePublicId", null) }, state);

        Assert.True(result.IsSuccess);
        Assert.True(state.CompanyTypeSet);
        Assert.Null(state.CompanyTypePublicId);
    }

    [Fact]
    public void Apply_CompanyTypePublicId_WithNonGuidString_IsRejected()
    {
        var state = new AccountCompanyPatchState();

        var result = AccountCompanyPatchApplier.Apply(new[] { Op("replace", "/companyTypePublicId", "not-a-guid") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_NameAsNumber_IsRejected()
    {
        var state = new AccountCompanyPatchState();

        var result = AccountCompanyPatchApplier.Apply(new[] { Op("replace", "/name", 42) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_RemoveName_IsRejected()
    {
        var state = new AccountCompanyPatchState();

        var result = AccountCompanyPatchApplier.Apply(new[] { Op("remove", "/name", null) }, state);

        Assert.True(result.IsFailure);
        Assert.False(state.NameSet);
    }

    [Fact]
    public void Apply_ConcurrencyTokenPath_IsRejected()
    {
        var state = new AccountCompanyPatchState();

        var result = AccountCompanyPatchApplier.Apply(new[] { Op("replace", "/concurrencyToken", Guid.NewGuid().ToString()) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_StatusPath_IsRejected()
    {
        var state = new AccountCompanyPatchState();

        // Status transitions go through /archive and /reactivate, never this patch.
        var result = AccountCompanyPatchApplier.Apply(new[] { Op("replace", "/status", "Archived") }, state);

        Assert.True(result.IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_ReadOnlyFields_AreRejected()
    {
        var state = new AccountCompanyPatchState();

        Assert.True(AccountCompanyPatchApplier.Apply(new[] { Op("replace", "/id", Guid.NewGuid().ToString()) }, state).IsFailure);
        Assert.True(AccountCompanyPatchApplier.Apply(new[] { Op("replace", "/publicId", Guid.NewGuid().ToString()) }, state).IsFailure);
        Assert.True(AccountCompanyPatchApplier.Apply(new[] { Op("replace", "/slug", "x") }, state).IsFailure);
        Assert.True(AccountCompanyPatchApplier.Apply(new[] { Op("replace", "/countryCode", "US") }, state).IsFailure);
        Assert.True(AccountCompanyPatchApplier.Apply(new[] { Op("replace", "/createdAtUtc", DateTime.UtcNow.ToString("O")) }, state).IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_UnsupportedOperation_IsRejected()
    {
        var state = new AccountCompanyPatchState();

        var result = AccountCompanyPatchApplier.Apply(new[] { Op("move", "/name", "x") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_IsRejected()
    {
        var state = new AccountCompanyPatchState();

        var result = AccountCompanyPatchApplier.Apply(new[] { Op("replace", "/name/sub", "x") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Validate_NameEmpty_Fails()
    {
        var state = new AccountCompanyPatchState();

        var apply = AccountCompanyPatchApplier.Apply(new[] { Op("replace", "/name", "   ") }, state);
        Assert.True(apply.IsSuccess);

        // Whitespace-only name trims to empty in the domain normalizer; must be a 400, not a 500.
        Assert.True(AccountCompanyPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_NameTooLong_Fails()
    {
        var state = new AccountCompanyPatchState();

        var apply = AccountCompanyPatchApplier.Apply(new[] { Op("replace", "/name", new string('z', 151)) }, state);
        Assert.True(apply.IsSuccess);

        // varchar(150) column: a >150 name would throw on save → 500; Validate must reject as a 400.
        Assert.True(AccountCompanyPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_WhenNothingPatched_Succeeds()
    {
        var state = new AccountCompanyPatchState();

        // Validate only inspects fields that were actually patched.
        Assert.True(AccountCompanyPatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = new AccountCompanyPatchState();

        var result = AccountCompanyPatchApplier.Apply(Array.Empty<AccountCompanyPatchOperation>(), state);

        Assert.True(result.IsSuccess);
        Assert.False(state.HasMutation);
    }
}
