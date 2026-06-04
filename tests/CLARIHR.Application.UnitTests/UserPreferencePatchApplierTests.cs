using System.Text.Json;
using CLARIHR.Application.Features.Preferences.User;
using CLARIHR.Domain.Preferences;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the scalar-only JSON Patch allow-list of
/// <see cref="UserPreferencePatchApplier"/> (the novel logic added when UserPreferences was aligned
/// to the canonical RFC-6902 PATCH). The only patchable path is <c>/language</c> (2–3 letters); it is
/// required and cannot be removed. Social links are replaced via <c>PUT /social-links</c>, not patched.
/// The concurrency token travels in the <c>If-Match</c> header and is not patchable; id/timestamps are
/// read-only. Locks that boundary in.
/// </summary>
public sealed class UserPreferencePatchApplierTests
{
    private static UserPreferencePatchState NewState() =>
        UserPreferencePatchState.From(UserPreference.Create(1, "en"));

    private static UserPreferencePatchOperation Op(string op, string path, object? value) =>
        new(op, path, null, value is null ? null : JsonSerializer.SerializeToElement(value));

    [Fact]
    public void Apply_ReplaceLanguage_MutatesStateAndValidates()
    {
        var state = NewState();

        var result = UserPreferencePatchApplier.Apply(new[] { Op("replace", "/language", "es") }, state);

        Assert.True(result.IsSuccess);
        Assert.True(state.HasMutation);
        Assert.Equal("es", state.Language);
        Assert.True(UserPreferencePatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_AddOperationOnLanguage_IsSupported()
    {
        var state = NewState();

        var result = UserPreferencePatchApplier.Apply(new[] { Op("add", "/language", "fr") }, state);

        Assert.True(result.IsSuccess);
        Assert.Equal("fr", state.Language);
    }

    [Fact]
    public void Apply_ThreeLetterLanguage_IsSupported()
    {
        var state = NewState();

        var result = UserPreferencePatchApplier.Apply(new[] { Op("replace", "/language", "eng") }, state);

        Assert.True(result.IsSuccess);
        Assert.Equal("eng", state.Language);
        Assert.True(UserPreferencePatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Apply_LanguageAsNumber_IsRejected()
    {
        var state = NewState();

        var result = UserPreferencePatchApplier.Apply(new[] { Op("replace", "/language", 42) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_RemoveLanguage_IsRejected()
    {
        var state = NewState();

        var result = UserPreferencePatchApplier.Apply(new[] { Op("remove", "/language", null) }, state);

        Assert.True(result.IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_SocialLinksPath_IsRejected()
    {
        var state = NewState();

        // Collections are replaced wholesale via PUT /social-links, never patched element-wise here.
        var result = UserPreferencePatchApplier.Apply(new[] { Op("add", "/socialLinks", "x") }, state);

        Assert.True(result.IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_ConcurrencyTokenPath_IsRejected()
    {
        var state = NewState();

        var result = UserPreferencePatchApplier.Apply(new[] { Op("replace", "/concurrencyToken", Guid.NewGuid().ToString()) }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_ReadOnlyFields_AreRejected()
    {
        var state = NewState();

        Assert.True(UserPreferencePatchApplier.Apply(new[] { Op("replace", "/id", Guid.NewGuid().ToString()) }, state).IsFailure);
        Assert.True(UserPreferencePatchApplier.Apply(new[] { Op("replace", "/createdAtUtc", DateTime.UtcNow.ToString("O")) }, state).IsFailure);
        Assert.True(UserPreferencePatchApplier.Apply(new[] { Op("replace", "/modifiedAtUtc", DateTime.UtcNow.ToString("O")) }, state).IsFailure);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_UnsupportedOperation_IsRejected()
    {
        var state = NewState();

        var result = UserPreferencePatchApplier.Apply(new[] { Op("move", "/language", "es") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_IsRejected()
    {
        var state = NewState();

        var result = UserPreferencePatchApplier.Apply(new[] { Op("replace", "/language/sub", "es") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_UnknownPath_IsRejected()
    {
        var state = NewState();

        var result = UserPreferencePatchApplier.Apply(new[] { Op("replace", "/currencyCode", "EUR") }, state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Validate_LanguageWrongFormat_Fails()
    {
        var state = NewState();

        var apply = UserPreferencePatchApplier.Apply(new[] { Op("replace", "/language", "e1") }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(UserPreferencePatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_LanguageTooLong_Fails()
    {
        var state = NewState();

        var apply = UserPreferencePatchApplier.Apply(new[] { Op("replace", "/language", "engl") }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(UserPreferencePatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_LanguageWithWhitespacePadding_Fails()
    {
        var state = NewState();

        // The anchored ^[A-Za-z]{2,3}$ regex rejects a whitespace-padded value, so the domain
        // normalizer (which trims first then re-validates) never throws an unmapped ArgumentException
        // → the request fails as a 400, not a 500.
        var apply = UserPreferencePatchApplier.Apply(new[] { Op("replace", "/language", " en") }, state);
        Assert.True(apply.IsSuccess);

        Assert.True(UserPreferencePatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = NewState();

        var result = UserPreferencePatchApplier.Apply(Array.Empty<UserPreferencePatchOperation>(), state);

        Assert.True(result.IsSuccess);
        Assert.False(state.HasMutation);
    }
}
