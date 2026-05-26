using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical Reference JSON Patch surface (PersonnelFileBackground
/// remediation): the pure <see cref="PersonnelFileReferencePatchApplier"/> and the
/// <see cref="PersonnelFileReferencePatchState"/> projection.
/// </summary>
public sealed class PersonnelFileReferencePatchTests
{
    private static PersonnelFileReferenceResponse Baseline() =>
        new(
            Guid.NewGuid(),
            "Juan Perez",
            "Col. Escalon",
            "+50370001234",
            "PROFESSIONAL",
            "Director",
            "Corp SA",
            "+50322005678",
            5m,
            Guid.NewGuid());

    private static PersonnelFileReferencePatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFileReferencePatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var state = PersonnelFileReferencePatchState.From(Baseline());

        Assert.Equal("Juan Perez", state.PersonName);
        Assert.Equal("Col. Escalon", state.Address);
        Assert.Equal("+50370001234", state.Phone);
        Assert.Equal("PROFESSIONAL", state.ReferenceTypeCode);
        Assert.Equal("Director", state.Occupation);
        Assert.Equal("Corp SA", state.Workplace);
        Assert.Equal("+50322005678", state.WorkPhone);
        Assert.Equal(5m, state.KnownTimeYears);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var input = PersonnelFileReferencePatchState.From(Baseline()).ToInput();

        Assert.Equal("Juan Perez", input.PersonName);
        Assert.Equal("+50370001234", input.Phone);
        Assert.Equal("PROFESSIONAL", input.ReferenceTypeCode);
        Assert.Equal(5m, input.KnownTimeYears);
    }

    [Fact]
    public void Apply_ReplacePersonName_Mutates()
    {
        var state = PersonnelFileReferencePatchState.From(Baseline());

        Assert.True(PersonnelFileReferencePatchApplier.Apply([Replace("/personName", "Juan Carlos Perez")], state).IsSuccess);
        Assert.Equal("Juan Carlos Perez", state.PersonName);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceKnownTimeYears_Mutates()
    {
        var state = PersonnelFileReferencePatchState.From(Baseline());

        Assert.True(PersonnelFileReferencePatchApplier.Apply([Replace("/knownTimeYears", 7.5)], state).IsSuccess);
        Assert.Equal(7.5m, state.KnownTimeYears);
    }

    [Fact]
    public void Apply_RemoveOptionalWorkplace_SetsNull()
    {
        var state = PersonnelFileReferencePatchState.From(Baseline());

        Assert.True(PersonnelFileReferencePatchApplier.Apply([Remove("/workplace")], state).IsSuccess);
        Assert.Null(state.Workplace);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveOptionalAddress_SetsNull()
    {
        var state = PersonnelFileReferencePatchState.From(Baseline());

        Assert.True(PersonnelFileReferencePatchApplier.Apply([Remove("/address")], state).IsSuccess);
        Assert.Null(state.Address);
    }

    [Fact]
    public void Apply_RemoveRequiredPersonName_Fails()
    {
        var state = PersonnelFileReferencePatchState.From(Baseline());

        Assert.True(PersonnelFileReferencePatchApplier.Apply([Remove("/personName")], state).IsSuccess);
        Assert.True(PersonnelFileReferencePatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveKnownTimeYears_Fails()
    {
        var state = PersonnelFileReferencePatchState.From(Baseline());

        Assert.True(PersonnelFileReferencePatchApplier.Apply([Remove("/knownTimeYears")], state).IsFailure);
    }

    [Fact]
    public void Apply_NonNumberForKnownTimeYears_Fails()
    {
        var state = PersonnelFileReferencePatchState.From(Baseline());

        Assert.True(PersonnelFileReferencePatchApplier.Apply([Replace("/knownTimeYears", "many")], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFileReferencePatchState.From(Baseline());

        Assert.True(PersonnelFileReferencePatchApplier.Apply(
            [new PersonnelFileReferencePatchOperation("copy", "/workplace", "/occupation", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFileReferencePatchState.From(Baseline());

        Assert.True(PersonnelFileReferencePatchApplier.Apply([Replace("/unknown", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFileReferencePatchState.From(Baseline());

        Assert.True(PersonnelFileReferencePatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_Baseline_Succeeds()
    {
        Assert.True(PersonnelFileReferencePatchApplier.Validate(PersonnelFileReferencePatchState.From(Baseline())).IsSuccess);
    }

    [Fact]
    public void Validate_BlankPhone_Fails()
    {
        var state = PersonnelFileReferencePatchState.From(Baseline());
        state.Phone = " ";

        Assert.True(PersonnelFileReferencePatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_BlankReferenceTypeCode_Fails()
    {
        var state = PersonnelFileReferencePatchState.From(Baseline());
        state.ReferenceTypeCode = " ";

        Assert.True(PersonnelFileReferencePatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_NegativeKnownTimeYears_Fails()
    {
        var state = PersonnelFileReferencePatchState.From(Baseline());
        state.KnownTimeYears = -1m;

        Assert.True(PersonnelFileReferencePatchApplier.Validate(state).IsFailure);
    }
}
