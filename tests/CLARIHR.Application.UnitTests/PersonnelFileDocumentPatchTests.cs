using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical Document JSON Patch surface (PersonnelFileDocuments
/// canonicalization): the pure <see cref="PersonnelFileDocumentPatchApplier"/> and the
/// <see cref="PersonnelFileDocumentPatchState"/> projection. The patchable members are the
/// document's metadata (`documentTypeCatalogItemPublicId`, `observations`); the file content
/// itself is replaced through the PUT endpoint, not patched here.
/// </summary>
public sealed class PersonnelFileDocumentPatchTests
{
    private static PersonnelFileDocumentMetadataResponse Baseline() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "DIPLOMA",
            "Diploma",
            string.Empty,
            "Initial observations",
            Guid.NewGuid(),
            "proof.pdf",
            "application/pdf",
            2048,
            true,
            Guid.NewGuid(),
            new DateTime(2026, 1, 1),
            null);

    private static PersonnelFileDocumentPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFileDocumentPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var baseline = Baseline();
        var state = PersonnelFileDocumentPatchState.From(baseline);

        Assert.Equal(baseline.DocumentTypeCatalogItemPublicId, state.DocumentTypeCatalogItemPublicId);
        Assert.Equal("Initial observations", state.Observations);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceObservations_Mutates()
    {
        var state = PersonnelFileDocumentPatchState.From(Baseline());

        Assert.True(PersonnelFileDocumentPatchApplier.Apply([Replace("/observations", "Updated note")], state).IsSuccess);
        Assert.Equal("Updated note", state.Observations);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceDocumentType_Mutates()
    {
        var state = PersonnelFileDocumentPatchState.From(Baseline());
        var newType = Guid.NewGuid();

        Assert.True(PersonnelFileDocumentPatchApplier.Apply([Replace("/documentTypeCatalogItemPublicId", newType)], state).IsSuccess);
        Assert.Equal(newType, state.DocumentTypeCatalogItemPublicId);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveOptionalObservations_SetsNull()
    {
        var state = PersonnelFileDocumentPatchState.From(Baseline());

        Assert.True(PersonnelFileDocumentPatchApplier.Apply([Remove("/observations")], state).IsSuccess);
        Assert.Null(state.Observations);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveRequiredDocumentType_Fails()
    {
        var state = PersonnelFileDocumentPatchState.From(Baseline());

        Assert.True(PersonnelFileDocumentPatchApplier.Apply([Remove("/documentTypeCatalogItemPublicId")], state).IsFailure);
    }

    [Fact]
    public void Apply_NonGuidForDocumentType_Fails()
    {
        var state = PersonnelFileDocumentPatchState.From(Baseline());

        Assert.True(PersonnelFileDocumentPatchApplier.Apply([Replace("/documentTypeCatalogItemPublicId", "not-a-guid")], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFileDocumentPatchState.From(Baseline());

        Assert.True(PersonnelFileDocumentPatchApplier.Apply(
            [new PersonnelFileDocumentPatchOperation("copy", "/observations", "/documentTypeCatalogItemPublicId", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFileDocumentPatchState.From(Baseline());

        Assert.True(PersonnelFileDocumentPatchApplier.Apply([Replace("/fileName", "tampered.pdf")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFileDocumentPatchState.From(Baseline());

        Assert.True(PersonnelFileDocumentPatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_Baseline_Succeeds()
    {
        Assert.True(PersonnelFileDocumentPatchApplier.Validate(PersonnelFileDocumentPatchState.From(Baseline())).IsSuccess);
    }

    [Fact]
    public void Validate_EmptyDocumentType_Fails()
    {
        var state = PersonnelFileDocumentPatchState.From(Baseline());
        state.DocumentTypeCatalogItemPublicId = Guid.Empty;

        Assert.True(PersonnelFileDocumentPatchApplier.Validate(state).IsFailure);
    }
}
