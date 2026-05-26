using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical Language JSON Patch surface (PersonnelFileBackground
/// remediation, Phase 3): the pure <see cref="PersonnelFileLanguagePatchApplier"/> and the
/// <see cref="PersonnelFileLanguagePatchState"/> projection.
/// </summary>
public sealed class PersonnelFileLanguagePatchTests
{
    private static PersonnelFileLanguageResponse Baseline() =>
        new(Guid.NewGuid(), "en", "C1", true, false, true, Guid.NewGuid());

    private static PersonnelFileLanguagePatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFileLanguagePatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var state = PersonnelFileLanguagePatchState.From(Baseline());

        Assert.Equal("en", state.LanguageCode);
        Assert.Equal("C1", state.LevelCode);
        Assert.True(state.Speaks);
        Assert.False(state.Writes);
        Assert.True(state.Reads);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var input = PersonnelFileLanguagePatchState.From(Baseline()).ToInput();

        Assert.Equal("en", input.LanguageCode);
        Assert.Equal("C1", input.LevelCode);
        Assert.True(input.Reads);
    }

    [Fact]
    public void Apply_ReplaceLanguageCode_Mutates()
    {
        var state = PersonnelFileLanguagePatchState.From(Baseline());

        Assert.True(PersonnelFileLanguagePatchApplier.Apply([Replace("/languageCode", "fr")], state).IsSuccess);
        Assert.Equal("fr", state.LanguageCode);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceBoolSkill_Mutates()
    {
        var state = PersonnelFileLanguagePatchState.From(Baseline());

        Assert.True(PersonnelFileLanguagePatchApplier.Apply([Replace("/writes", true)], state).IsSuccess);
        Assert.True(state.Writes);
    }

    [Fact]
    public void Apply_RemoveSkill_Fails()
    {
        var state = PersonnelFileLanguagePatchState.From(Baseline());

        Assert.True(PersonnelFileLanguagePatchApplier.Apply([Remove("/speaks")], state).IsFailure);
    }

    [Fact]
    public void Apply_NonBoolForSkill_Fails()
    {
        var state = PersonnelFileLanguagePatchState.From(Baseline());

        Assert.True(PersonnelFileLanguagePatchApplier.Apply([Replace("/speaks", "yes")], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFileLanguagePatchState.From(Baseline());

        Assert.True(PersonnelFileLanguagePatchApplier.Apply(
            [new PersonnelFileLanguagePatchOperation("copy", "/levelCode", "/languageCode", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFileLanguagePatchState.From(Baseline());

        Assert.True(PersonnelFileLanguagePatchApplier.Apply([Replace("/fluency", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFileLanguagePatchState.From(Baseline());

        Assert.True(PersonnelFileLanguagePatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_Baseline_Succeeds()
    {
        Assert.True(PersonnelFileLanguagePatchApplier.Validate(PersonnelFileLanguagePatchState.From(Baseline())).IsSuccess);
    }

    [Fact]
    public void Validate_BlankLanguageCode_Fails()
    {
        var state = PersonnelFileLanguagePatchState.From(Baseline());
        state.LanguageCode = " ";

        Assert.True(PersonnelFileLanguagePatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_AllSkillsFalse_Fails()
    {
        var state = PersonnelFileLanguagePatchState.From(Baseline());
        state.Speaks = false;
        state.Writes = false;
        state.Reads = false;

        Assert.True(PersonnelFileLanguagePatchApplier.Validate(state).IsFailure);
    }
}
