using System.Text.Json;
using CLARIHR.Application.Features.Preferences.Company;
using CLARIHR.Domain.Preferences;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the scalar-only JSON Patch allow-list of
/// <see cref="CompanyPreferencePatchApplier"/> (the novel logic added when CompanyPreferences was
/// aligned to the canonical RFC-6902 PATCH). Patchable = <c>/currencyCode</c> (exactly 3 chars) and
/// <c>/timeZone</c> (max 100 chars); both are required and cannot be removed. The concurrency token
/// travels in the <c>If-Match</c> header and is not patchable; id/timestamps are read-only. Locks
/// that boundary in.
/// </summary>
public sealed class CompanyPreferencePatchApplierTests
{
    private static CompanyPreferencePatchState NewState() =>
        CompanyPreferencePatchState.From(CompanyPreference.Create("USD", "UTC"));

    private static CompanyPreferencePatchOperation Op(string op, string path, object? value) =>
        new(op, path, null, value is null ? null : JsonSerializer.SerializeToElement(value));

    [Fact]
    public void Apply_ReplaceCurrencyCode_MutatesStateAndValidates()
    {
        var state = NewState();

        var result = CompanyPreferencePatchApplier.Apply(new[] { Op("replace", "/currencyCode", "EUR") }, state);

        Assert.True(result.IsSuccess);
        Assert.True(state.HasMutation);
        Assert.Equal("EUR", state.CurrencyCode);
        Assert.True(CompanyPreferencePatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_ReplaceTimeZone_MutatesStateAndValidates()
    {
        var state = NewState();

        var result = CompanyPreferencePatchApplier.Apply(new[] { Op("replace", "/timeZone", "Europe/Madrid") }, state);

        Assert.True(result.IsSuccess);
        Assert.True(state.HasMutation);
        Assert.Equal("Europe/Madrid", state.TimeZone);
        Assert.True(CompanyPreferencePatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_AddOperationOnCurrencyCode_IsSupported()
    {
        var state = NewState();

        var result = CompanyPreferencePatchApplier.Apply(new[] { Op("add", "/currencyCode", "EUR") }, state);

        Assert.True(result.IsSuccess);
        Assert.Equal("EUR", state.CurrencyCode);
    }

    [Fact]
    public void Apply_AddOperationOnTimeZone_IsSupported()
    {
        var state = NewState();

        var result = CompanyPreferencePatchApplier.Apply(new[] { Op("add", "/timeZone", "America/New_York") }, state);

        Assert.True(result.IsSuccess);
        Assert.Equal("America/New_York", state.TimeZone);
    }

    [Fact]
    public void Apply_CurrencyCodeAsNumber_IsRejected()
    {
        var state = NewState();

        var result = CompanyPreferencePatchApplier.Apply(new[] { Op("replace", "/currencyCode", 840) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_RemoveCurrencyCode_IsRejected()
    {
        var state = NewState();

        var result = CompanyPreferencePatchApplier.Apply(new[] { Op("remove", "/currencyCode", null) }, state);

        Assert.True(result.IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveTimeZone_IsRejected()
    {
        var state = NewState();

        var result = CompanyPreferencePatchApplier.Apply(new[] { Op("remove", "/timeZone", null) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_ConcurrencyTokenPath_IsRejected()
    {
        var state = NewState();

        var result = CompanyPreferencePatchApplier.Apply(new[] { Op("replace", "/concurrencyToken", Guid.NewGuid().ToString()) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_ReadOnlyFields_AreRejected()
    {
        var state = NewState();

        Assert.True(CompanyPreferencePatchApplier.Apply(new[] { Op("replace", "/id", Guid.NewGuid().ToString()) }, state).IsFailure);
        Assert.True(CompanyPreferencePatchApplier.Apply(new[] { Op("replace", "/createdAtUtc", DateTime.UtcNow.ToString("O")) }, state).IsFailure);
        Assert.True(CompanyPreferencePatchApplier.Apply(new[] { Op("replace", "/modifiedAtUtc", DateTime.UtcNow.ToString("O")) }, state).IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_UnsupportedOperation_IsRejected()
    {
        var state = NewState();

        var result = CompanyPreferencePatchApplier.Apply(new[] { Op("move", "/currencyCode", "EUR") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_IsRejected()
    {
        var state = NewState();

        var result = CompanyPreferencePatchApplier.Apply(new[] { Op("replace", "/currencyCode/sub", "EUR") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_UnknownPath_IsRejected()
    {
        var state = NewState();

        var result = CompanyPreferencePatchApplier.Apply(new[] { Op("replace", "/locale", "es") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Validate_CurrencyCodeWrongLength_Fails()
    {
        var state = NewState();

        var apply = CompanyPreferencePatchApplier.Apply(new[] { Op("replace", "/currencyCode", "EU") }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(CompanyPreferencePatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_CurrencyCodeWithWhitespacePaddingThatTrimsShorter_Fails()
    {
        var state = NewState();

        // Raw length 3 but trims to "US" (length 2). Must be rejected by Validate so the domain
        // normalizer (which trims first) never throws an unmapped ArgumentException → HTTP 500.
        var apply = CompanyPreferencePatchApplier.Apply(new[] { Op("replace", "/currencyCode", " US") }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(CompanyPreferencePatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_TimeZoneTooLong_Fails()
    {
        var state = NewState();

        var apply = CompanyPreferencePatchApplier.Apply(new[] { Op("replace", "/timeZone", new string('z', 101)) }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(CompanyPreferencePatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = NewState();

        var result = CompanyPreferencePatchApplier.Apply(Array.Empty<CompanyPreferencePatchOperation>(), state);

        Assert.True(result.IsSuccess);
        Assert.False(state.HasMutation);
    }
}
