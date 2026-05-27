using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical Family Member JSON Patch surface (PersonnelFilePersonalInfo
/// remediation): the pure <see cref="PersonnelFileFamilyMemberPatchApplier"/> apply/validate
/// logic and the <see cref="PersonnelFileFamilyMemberPatchState"/> projection. Exercises the
/// conditional domain rules (studying/working/deceased) and the <c>sex</c> enum reader.
/// </summary>
public sealed class PersonnelFileFamilyMemberPatchTests
{
    private static PersonnelFileFamilyMemberResponse Baseline() =>
        new(
            Guid.NewGuid(),
            "Luis",
            "Lopez",
            "Luis Lopez",
            "HERMANO_A",
            "Salvadorena",
            new DateTime(2000, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            PersonnelFamilyMemberSex.Male,
            "Single",
            "Student",
            "DUI",
            "11111111-1",
            "+50370000002",
            false,
            null,
            null,
            true,
            false,
            null,
            null,
            null,
            null,
            false,
            null,
            Guid.NewGuid());

    private static PersonnelFileFamilyMemberPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFileFamilyMemberPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var state = PersonnelFileFamilyMemberPatchState.From(Baseline());

        Assert.Equal("Luis", state.FirstName);
        Assert.Equal("Lopez", state.LastName);
        Assert.Equal("HERMANO_A", state.KinshipCode);
        Assert.Equal(PersonnelFamilyMemberSex.Male, state.Sex);
        Assert.True(state.IsBeneficiary);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var input = PersonnelFileFamilyMemberPatchState.From(Baseline()).ToInput();

        Assert.Equal("Luis", input.FirstName);
        Assert.Equal("HERMANO_A", input.KinshipCode);
        Assert.Equal(PersonnelFamilyMemberSex.Male, input.Sex);
        Assert.True(input.IsBeneficiary);
    }

    [Fact]
    public void Apply_ReplaceScalarField_Mutates()
    {
        var state = PersonnelFileFamilyMemberPatchState.From(Baseline());

        Assert.True(PersonnelFileFamilyMemberPatchApplier.Apply([Replace("/firstName", "Carlos")], state).IsSuccess);
        Assert.Equal("Carlos", state.FirstName);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceSexByName_Mutates()
    {
        var state = PersonnelFileFamilyMemberPatchState.From(Baseline());

        Assert.True(PersonnelFileFamilyMemberPatchApplier.Apply([Replace("/sex", "Female")], state).IsSuccess);
        Assert.Equal(PersonnelFamilyMemberSex.Female, state.Sex);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceSalary_ParsesNumber()
    {
        var state = PersonnelFileFamilyMemberPatchState.From(Baseline());

        Assert.True(PersonnelFileFamilyMemberPatchApplier.Apply([Replace("/salary", 1234.56)], state).IsSuccess);
        Assert.Equal(1234.56m, state.Salary);
    }

    [Fact]
    public void Apply_ReplaceBirthDate_ParsesIsoString()
    {
        var state = PersonnelFileFamilyMemberPatchState.From(Baseline());

        Assert.True(PersonnelFileFamilyMemberPatchApplier.Apply([Replace("/birthDate", "1999-01-01T00:00:00Z")], state).IsSuccess);
        Assert.Equal(1999, state.BirthDate!.Value.Year);
    }

    [Fact]
    public void Apply_RemoveOptional_SetsNull()
    {
        var state = PersonnelFileFamilyMemberPatchState.From(Baseline());

        Assert.True(PersonnelFileFamilyMemberPatchApplier.Apply([Remove("/nationality")], state).IsSuccess);
        Assert.Null(state.Nationality);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveRequired_FailsValidation()
    {
        var state = PersonnelFileFamilyMemberPatchState.From(Baseline());

        // Remove clears the value to empty, which the subsequent Validate rejects.
        Assert.True(PersonnelFileFamilyMemberPatchApplier.Apply([Remove("/firstName")], state).IsSuccess);
        Assert.True(PersonnelFileFamilyMemberPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveSex_Fails()
    {
        var state = PersonnelFileFamilyMemberPatchState.From(Baseline());

        Assert.True(PersonnelFileFamilyMemberPatchApplier.Apply([Remove("/sex")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveIsStudying_Fails()
    {
        var state = PersonnelFileFamilyMemberPatchState.From(Baseline());

        Assert.True(PersonnelFileFamilyMemberPatchApplier.Apply([Remove("/isStudying")], state).IsFailure);
    }

    [Fact]
    public void Apply_InvalidSexValue_Fails()
    {
        var state = PersonnelFileFamilyMemberPatchState.From(Baseline());

        Assert.True(PersonnelFileFamilyMemberPatchApplier.Apply([Replace("/sex", "Martian")], state).IsFailure);
    }

    [Fact]
    public void Apply_NonStringForFirstName_Fails()
    {
        var state = PersonnelFileFamilyMemberPatchState.From(Baseline());

        Assert.True(PersonnelFileFamilyMemberPatchApplier.Apply([Replace("/firstName", 42)], state).IsFailure);
    }

    [Fact]
    public void Apply_NonBooleanForIsWorking_Fails()
    {
        var state = PersonnelFileFamilyMemberPatchState.From(Baseline());

        Assert.True(PersonnelFileFamilyMemberPatchApplier.Apply([Replace("/isWorking", "yes")], state).IsFailure);
    }

    [Fact]
    public void Apply_NonNumberForSalary_Fails()
    {
        var state = PersonnelFileFamilyMemberPatchState.From(Baseline());

        Assert.True(PersonnelFileFamilyMemberPatchApplier.Apply([Replace("/salary", "lots")], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFileFamilyMemberPatchState.From(Baseline());

        Assert.True(PersonnelFileFamilyMemberPatchApplier.Apply(
            [new PersonnelFileFamilyMemberPatchOperation("move", "/firstName", "/lastName", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFileFamilyMemberPatchState.From(Baseline());

        Assert.True(PersonnelFileFamilyMemberPatchApplier.Apply([Replace("/notARealField", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_Fails()
    {
        var state = PersonnelFileFamilyMemberPatchState.From(Baseline());

        Assert.True(PersonnelFileFamilyMemberPatchApplier.Apply([Replace("/firstName/0", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFileFamilyMemberPatchState.From(Baseline());

        Assert.True(PersonnelFileFamilyMemberPatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_Baseline_Succeeds()
    {
        Assert.True(PersonnelFileFamilyMemberPatchApplier.Validate(PersonnelFileFamilyMemberPatchState.From(Baseline())).IsSuccess);
    }

    [Fact]
    public void Validate_BlankKinshipCode_Fails()
    {
        var state = PersonnelFileFamilyMemberPatchState.From(Baseline());
        state.KinshipCode = "  ";

        Assert.True(PersonnelFileFamilyMemberPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_StudyingWithoutStudyPlace_Fails()
    {
        var state = PersonnelFileFamilyMemberPatchState.From(Baseline());
        state.IsStudying = true;
        state.StudyPlace = null;
        state.AcademicLevel = "Tertiary";

        Assert.True(PersonnelFileFamilyMemberPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_WorkingWithoutJobTitle_Fails()
    {
        var state = PersonnelFileFamilyMemberPatchState.From(Baseline());
        state.IsWorking = true;
        state.Workplace = "Corp SA";
        state.JobTitle = null;

        Assert.True(PersonnelFileFamilyMemberPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_DeceasedWithoutDate_Fails()
    {
        var state = PersonnelFileFamilyMemberPatchState.From(Baseline());
        state.IsDeceased = true;
        state.DeceasedDate = null;

        Assert.True(PersonnelFileFamilyMemberPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_NegativeSalary_Fails()
    {
        var state = PersonnelFileFamilyMemberPatchState.From(Baseline());
        state.Salary = -1m;

        Assert.True(PersonnelFileFamilyMemberPatchApplier.Validate(state).IsFailure);
    }
}
