using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical Education JSON Patch surface (pilot of the
/// PersonnelFileBackground remediation): the pure <see cref="PersonnelFileEducationPatchApplier"/>
/// apply/validate logic and the <see cref="PersonnelFileEducationPatchState"/> projection.
/// Mirrors the JobProfile sub-resource patch pattern.
/// </summary>
public sealed class PersonnelFileEducationPatchTests
{
    private static readonly Guid StatusId = Guid.NewGuid();
    private static readonly Guid StudyTypeId = Guid.NewGuid();
    private static readonly Guid CareerId = Guid.NewGuid();
    private static readonly Guid ShiftId = Guid.NewGuid();
    private static readonly Guid ModalityId = Guid.NewGuid();

    private static PersonnelFileEducationResponse BaselineResponse() =>
        new(
            Guid.NewGuid(),
            new PersonnelEducationCatalogReferenceResponse(StatusId, "GRAD", "Graduated", true),
            "BSc",
            new PersonnelEducationCatalogReferenceResponse(StudyTypeId, "UNIV", "University", true),
            new PersonnelEducationCatalogReferenceResponse(CareerId, "CS", "Computer Science", true),
            "MIT",
            "US",
            "AI",
            false,
            new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new PersonnelEducationCatalogReferenceResponse(ShiftId, "DAY", "Daytime", true),
            new PersonnelEducationCatalogReferenceResponse(ModalityId, "ONS", "On-site", true),
            40,
            38,
            Guid.NewGuid());

    private static PersonnelFileEducationPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFileEducationPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponseCatalogReferencesToPublicIds()
    {
        var state = PersonnelFileEducationPatchState.From(BaselineResponse());

        Assert.Equal(StatusId, state.StatusPublicId);
        Assert.Equal(StudyTypeId, state.StudyTypePublicId);
        Assert.Equal(CareerId, state.CareerPublicId);
        Assert.Equal(ShiftId, state.ShiftPublicId);
        Assert.Equal(ModalityId, state.ModalityPublicId);
        Assert.Equal("MIT", state.Institution);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTripsStateIntoEducationInput()
    {
        var input = PersonnelFileEducationPatchState.From(BaselineResponse()).ToInput();

        Assert.Equal(StatusId, input.StatusPublicId);
        Assert.Equal(CareerId, input.CareerPublicId);
        Assert.Equal("MIT", input.Institution);
        Assert.Equal(ShiftId, input.ShiftPublicId);
        Assert.Equal(40, input.TotalSubjects);
    }

    [Fact]
    public void Apply_ReplaceScalarField_MutatesState()
    {
        var state = PersonnelFileEducationPatchState.From(BaselineResponse());

        var result = PersonnelFileEducationPatchApplier.Apply([Replace("/institution", "Stanford")], state);

        Assert.True(result.IsSuccess);
        Assert.True(state.HasMutation);
        Assert.Equal("Stanford", state.Institution);
    }

    [Fact]
    public void Apply_ReplaceCatalogPublicId_AcceptsBothAliasSpellings()
    {
        var newStatus = Guid.NewGuid();
        var fromPublicId = PersonnelFileEducationPatchState.From(BaselineResponse());
        var fromShortId = PersonnelFileEducationPatchState.From(BaselineResponse());

        Assert.True(PersonnelFileEducationPatchApplier.Apply([Replace("/statusPublicId", newStatus)], fromPublicId).IsSuccess);
        Assert.True(PersonnelFileEducationPatchApplier.Apply([Replace("/statusId", newStatus)], fromShortId).IsSuccess);
        Assert.Equal(newStatus, fromPublicId.StatusPublicId);
        Assert.Equal(newStatus, fromShortId.StatusPublicId);
    }

    [Fact]
    public void Apply_RemoveOptionalCatalog_SetsNull()
    {
        var state = PersonnelFileEducationPatchState.From(BaselineResponse());

        Assert.True(PersonnelFileEducationPatchApplier.Apply([Remove("/shiftPublicId")], state).IsSuccess);
        Assert.Null(state.ShiftPublicId);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveRequiredCatalog_Fails()
    {
        var state = PersonnelFileEducationPatchState.From(BaselineResponse());

        Assert.True(PersonnelFileEducationPatchApplier.Apply([Remove("/statusPublicId")], state).IsFailure);
    }

    [Fact]
    public void Apply_ReplaceEndDate_ParsesIsoString()
    {
        var state = PersonnelFileEducationPatchState.From(BaselineResponse());

        var result = PersonnelFileEducationPatchApplier.Apply([Replace("/endDate", "2023-06-30T00:00:00Z")], state);

        Assert.True(result.IsSuccess);
        Assert.Equal(2023, state.EndDate!.Value.Year);
    }

    [Fact]
    public void Apply_InvalidGuidValue_Fails()
    {
        var state = PersonnelFileEducationPatchState.From(BaselineResponse());

        Assert.True(PersonnelFileEducationPatchApplier.Apply([Replace("/statusPublicId", "not-a-guid")], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFileEducationPatchState.From(BaselineResponse());

        var result = PersonnelFileEducationPatchApplier.Apply(
            [new PersonnelFileEducationPatchOperation("move", "/institution", "/countryCode", null)],
            state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFileEducationPatchState.From(BaselineResponse());

        Assert.True(PersonnelFileEducationPatchApplier.Apply([Replace("/notARealField", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_Fails()
    {
        var state = PersonnelFileEducationPatchState.From(BaselineResponse());

        Assert.True(PersonnelFileEducationPatchApplier.Apply([Replace("/status/code", "X")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFileEducationPatchState.From(BaselineResponse());

        Assert.True(PersonnelFileEducationPatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_BaselineState_Succeeds()
    {
        Assert.True(PersonnelFileEducationPatchApplier.Validate(PersonnelFileEducationPatchState.From(BaselineResponse())).IsSuccess);
    }

    [Fact]
    public void Validate_EmptyRequiredCatalog_Fails()
    {
        var state = PersonnelFileEducationPatchState.From(BaselineResponse());
        state.StudyTypePublicId = Guid.Empty;

        Assert.True(PersonnelFileEducationPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_BlankInstitution_Fails()
    {
        var state = PersonnelFileEducationPatchState.From(BaselineResponse());
        state.Institution = "   ";

        Assert.True(PersonnelFileEducationPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_EndDateBeforeStartDate_Fails()
    {
        var state = PersonnelFileEducationPatchState.From(BaselineResponse());
        state.EndDate = state.StartDate.AddYears(-1);

        Assert.True(PersonnelFileEducationPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_NotStudyingWithoutEndDate_Fails()
    {
        var state = PersonnelFileEducationPatchState.From(BaselineResponse());
        state.IsCurrentlyStudying = false;
        state.EndDate = null;

        Assert.True(PersonnelFileEducationPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_ApprovedSubjectsGreaterThanTotal_Fails()
    {
        var state = PersonnelFileEducationPatchState.From(BaselineResponse());
        state.TotalSubjects = 10;
        state.ApprovedSubjects = 11;

        Assert.True(PersonnelFileEducationPatchApplier.Validate(state).IsFailure);
    }
}
