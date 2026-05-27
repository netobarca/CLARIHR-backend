using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical insurance-beneficiary JSON Patch surface: the pure
/// <see cref="PersonnelFileInsuranceBeneficiaryPatchApplier"/> and the
/// <see cref="PersonnelFileInsuranceBeneficiaryPatchState"/> projection. The beneficiary's
/// business <c>Input</c>/<c>PUT</c> contract does not carry <c>isActive</c>, but the patch surface
/// supports toggling it, so the applier must accept boolean values and flag the mutation while
/// preserving the business-field validation the Add/Update commands run. A patched
/// <c>kinshipCode</c> is separately flagged so the handler can re-validate it against the catalog.
/// </summary>
public sealed class PersonnelFileInsuranceBeneficiaryPatchTests
{
    private static PersonnelFileInsuranceBeneficiaryResponse Baseline() =>
        new(
            Guid.NewGuid(),
            "Jane Doe",
            "DOC-1234",
            new DateTime(1990, 6, 15, 0, 0, 0, DateTimeKind.Utc),
            "SPOUSE",
            true,
            Guid.NewGuid());

    private static PersonnelFileInsuranceBeneficiaryPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFileInsuranceBeneficiaryPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var state = PersonnelFileInsuranceBeneficiaryPatchState.From(Baseline());

        Assert.Equal("Jane Doe", state.FullName);
        Assert.Equal("DOC-1234", state.DocumentNumber);
        Assert.Equal("SPOUSE", state.KinshipCode);
        Assert.True(state.IsActive);
        Assert.False(state.IsActiveMutated);
        Assert.False(state.KinshipCodeMutated);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var input = PersonnelFileInsuranceBeneficiaryPatchState.From(Baseline()).ToInput();

        Assert.Equal("Jane Doe", input.FullName);
        Assert.Equal("DOC-1234", input.DocumentNumber);
        Assert.Equal("SPOUSE", input.KinshipCode);
    }

    [Fact]
    public void Apply_ReplaceFullName_Mutates()
    {
        var state = PersonnelFileInsuranceBeneficiaryPatchState.From(Baseline());

        Assert.True(PersonnelFileInsuranceBeneficiaryPatchApplier.Apply([Replace("/fullName", "John Roe")], state).IsSuccess);
        Assert.Equal("John Roe", state.FullName);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceKinshipCode_FlagsKinshipMutation()
    {
        var state = PersonnelFileInsuranceBeneficiaryPatchState.From(Baseline());

        Assert.True(PersonnelFileInsuranceBeneficiaryPatchApplier.Apply([Replace("/kinshipCode", "CHILD")], state).IsSuccess);
        Assert.Equal("CHILD", state.KinshipCode);
        Assert.True(state.KinshipCodeMutated);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceIsActiveFalse_MutatesAndFlagsActiveChange()
    {
        var state = PersonnelFileInsuranceBeneficiaryPatchState.From(Baseline());

        Assert.True(PersonnelFileInsuranceBeneficiaryPatchApplier.Apply([Replace("/isActive", false)], state).IsSuccess);
        Assert.False(state.IsActive);
        Assert.True(state.IsActiveMutated);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceIsActiveTrue_MutatesAndFlagsActiveChange()
    {
        var state = PersonnelFileInsuranceBeneficiaryPatchState.From(Baseline() with { IsActive = false });

        Assert.True(PersonnelFileInsuranceBeneficiaryPatchApplier.Apply([Replace("/isActive", true)], state).IsSuccess);
        Assert.True(state.IsActive);
        Assert.True(state.IsActiveMutated);
    }

    [Fact]
    public void Apply_BusinessFieldOnly_DoesNotFlagActiveChange()
    {
        var state = PersonnelFileInsuranceBeneficiaryPatchState.From(Baseline());

        Assert.True(PersonnelFileInsuranceBeneficiaryPatchApplier.Apply([Replace("/fullName", "Sam Roe")], state).IsSuccess);
        Assert.False(state.IsActiveMutated);
        Assert.False(state.KinshipCodeMutated);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_NonBooleanForIsActive_Fails()
    {
        var state = PersonnelFileInsuranceBeneficiaryPatchState.From(Baseline());

        Assert.True(PersonnelFileInsuranceBeneficiaryPatchApplier.Apply([Replace("/isActive", "yes")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveIsActive_Fails()
    {
        var state = PersonnelFileInsuranceBeneficiaryPatchState.From(Baseline());

        Assert.True(PersonnelFileInsuranceBeneficiaryPatchApplier.Apply([Remove("/isActive")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveOptionalDocumentNumber_ClearsValue()
    {
        var state = PersonnelFileInsuranceBeneficiaryPatchState.From(Baseline());

        Assert.True(PersonnelFileInsuranceBeneficiaryPatchApplier.Apply([Remove("/documentNumber")], state).IsSuccess);
        Assert.Null(state.DocumentNumber);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveOptionalBirthDate_ClearsValue()
    {
        var state = PersonnelFileInsuranceBeneficiaryPatchState.From(Baseline());

        Assert.True(PersonnelFileInsuranceBeneficiaryPatchApplier.Apply([Remove("/birthDate")], state).IsSuccess);
        Assert.Null(state.BirthDate);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveRequiredFullName_FailsValidation()
    {
        var state = PersonnelFileInsuranceBeneficiaryPatchState.From(Baseline());

        Assert.True(PersonnelFileInsuranceBeneficiaryPatchApplier.Apply([Remove("/fullName")], state).IsSuccess);
        Assert.True(PersonnelFileInsuranceBeneficiaryPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveRequiredKinshipCode_FailsValidation()
    {
        var state = PersonnelFileInsuranceBeneficiaryPatchState.From(Baseline());

        Assert.True(PersonnelFileInsuranceBeneficiaryPatchApplier.Apply([Remove("/kinshipCode")], state).IsSuccess);
        Assert.True(PersonnelFileInsuranceBeneficiaryPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_NonStringForFullName_Fails()
    {
        var state = PersonnelFileInsuranceBeneficiaryPatchState.From(Baseline());

        Assert.True(PersonnelFileInsuranceBeneficiaryPatchApplier.Apply([Replace("/fullName", 42)], state).IsFailure);
    }

    [Fact]
    public void Apply_NonDateForBirthDate_Fails()
    {
        var state = PersonnelFileInsuranceBeneficiaryPatchState.From(Baseline());

        Assert.True(PersonnelFileInsuranceBeneficiaryPatchApplier.Apply([Replace("/birthDate", "not-a-date")], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFileInsuranceBeneficiaryPatchState.From(Baseline());

        Assert.True(PersonnelFileInsuranceBeneficiaryPatchApplier.Apply(
            [new PersonnelFileInsuranceBeneficiaryPatchOperation("copy", "/fullName", "/fullName", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFileInsuranceBeneficiaryPatchState.From(Baseline());

        Assert.True(PersonnelFileInsuranceBeneficiaryPatchApplier.Apply([Replace("/unknown", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_Fails()
    {
        var state = PersonnelFileInsuranceBeneficiaryPatchState.From(Baseline());

        Assert.True(PersonnelFileInsuranceBeneficiaryPatchApplier.Apply([Replace("/fullName/0", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFileInsuranceBeneficiaryPatchState.From(Baseline());

        Assert.True(PersonnelFileInsuranceBeneficiaryPatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_Baseline_Succeeds()
    {
        Assert.True(PersonnelFileInsuranceBeneficiaryPatchApplier.Validate(PersonnelFileInsuranceBeneficiaryPatchState.From(Baseline())).IsSuccess);
    }

    [Fact]
    public void Validate_BlankKinshipCode_Fails()
    {
        var state = PersonnelFileInsuranceBeneficiaryPatchState.From(Baseline());
        state.KinshipCode = " ";

        Assert.True(PersonnelFileInsuranceBeneficiaryPatchApplier.Validate(state).IsFailure);
    }
}
