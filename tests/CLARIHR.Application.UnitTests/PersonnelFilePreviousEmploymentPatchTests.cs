using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical PreviousEmployment JSON Patch surface
/// (PersonnelFileBackground remediation): the pure
/// <see cref="PersonnelFilePreviousEmploymentPatchApplier"/> and the
/// <see cref="PersonnelFilePreviousEmploymentPatchState"/> projection.
/// </summary>
public sealed class PersonnelFilePreviousEmploymentPatchTests
{
    private static PersonnelFilePreviousEmploymentResponse Baseline() =>
        new(
            Guid.NewGuid(),
            "Globex Corp",
            "San Salvador",
            "Senior Engineer",
            "Maria Lopez",
            new DateTime(2018, 3, 1),
            new DateTime(2022, 6, 30),
            "+50322001234",
            "Career growth",
            1500m,
            2500m,
            300m,
            "USD",
            Guid.NewGuid());

    private static PersonnelFilePreviousEmploymentPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFilePreviousEmploymentPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var state = PersonnelFilePreviousEmploymentPatchState.From(Baseline());

        Assert.Equal("Globex Corp", state.Institution);
        Assert.Equal("San Salvador", state.Place);
        Assert.Equal("Senior Engineer", state.LastPosition);
        Assert.Equal("Maria Lopez", state.ManagerName);
        Assert.Equal(new DateTime(2018, 3, 1), state.EntryDate);
        Assert.Equal(new DateTime(2022, 6, 30), state.RetirementDate);
        Assert.Equal("+50322001234", state.CompanyPhone);
        Assert.Equal("Career growth", state.ExitReason);
        Assert.Equal(1500m, state.FirstSalaryAmount);
        Assert.Equal(2500m, state.LastSalaryAmount);
        Assert.Equal(300m, state.AverageCommissionAmount);
        Assert.Equal("USD", state.CurrencyCode);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var input = PersonnelFilePreviousEmploymentPatchState.From(Baseline()).ToInput();

        Assert.Equal("Globex Corp", input.Institution);
        Assert.Equal(new DateTime(2018, 3, 1), input.EntryDate);
        Assert.Equal(2500m, input.LastSalaryAmount);
        Assert.Equal("USD", input.CurrencyCode);
    }

    [Fact]
    public void Apply_ReplaceInstitution_Mutates()
    {
        var state = PersonnelFilePreviousEmploymentPatchState.From(Baseline());

        Assert.True(PersonnelFilePreviousEmploymentPatchApplier.Apply([Replace("/institution", "Initech")], state).IsSuccess);
        Assert.Equal("Initech", state.Institution);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceFirstSalaryAmount_Mutates()
    {
        var state = PersonnelFilePreviousEmploymentPatchState.From(Baseline());

        Assert.True(PersonnelFilePreviousEmploymentPatchApplier.Apply([Replace("/firstSalaryAmount", 1750.75)], state).IsSuccess);
        Assert.Equal(1750.75m, state.FirstSalaryAmount);
    }

    [Fact]
    public void Apply_RemoveOptionalRetirementDate_SetsNull()
    {
        var state = PersonnelFilePreviousEmploymentPatchState.From(Baseline());

        Assert.True(PersonnelFilePreviousEmploymentPatchApplier.Apply([Remove("/retirementDate")], state).IsSuccess);
        Assert.Null(state.RetirementDate);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveOptionalFirstSalaryAmount_SetsNull()
    {
        var state = PersonnelFilePreviousEmploymentPatchState.From(Baseline());

        Assert.True(PersonnelFilePreviousEmploymentPatchApplier.Apply([Remove("/firstSalaryAmount")], state).IsSuccess);
        Assert.Null(state.FirstSalaryAmount);
    }

    [Fact]
    public void Apply_RemoveRequiredInstitution_Fails()
    {
        var state = PersonnelFilePreviousEmploymentPatchState.From(Baseline());

        Assert.True(PersonnelFilePreviousEmploymentPatchApplier.Apply([Remove("/institution")], state).IsSuccess);
        Assert.True(PersonnelFilePreviousEmploymentPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveEntryDate_Fails()
    {
        var state = PersonnelFilePreviousEmploymentPatchState.From(Baseline());

        Assert.True(PersonnelFilePreviousEmploymentPatchApplier.Apply([Remove("/entryDate")], state).IsFailure);
    }

    [Fact]
    public void Apply_NonNumberForSalary_Fails()
    {
        var state = PersonnelFilePreviousEmploymentPatchState.From(Baseline());

        Assert.True(PersonnelFilePreviousEmploymentPatchApplier.Apply([Replace("/lastSalaryAmount", "lots")], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFilePreviousEmploymentPatchState.From(Baseline());

        Assert.True(PersonnelFilePreviousEmploymentPatchApplier.Apply(
            [new PersonnelFilePreviousEmploymentPatchOperation("copy", "/place", "/institution", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFilePreviousEmploymentPatchState.From(Baseline());

        Assert.True(PersonnelFilePreviousEmploymentPatchApplier.Apply([Replace("/unknown", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFilePreviousEmploymentPatchState.From(Baseline());

        Assert.True(PersonnelFilePreviousEmploymentPatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_Baseline_Succeeds()
    {
        Assert.True(PersonnelFilePreviousEmploymentPatchApplier.Validate(PersonnelFilePreviousEmploymentPatchState.From(Baseline())).IsSuccess);
    }

    [Fact]
    public void Validate_RetirementDateBeforeEntryDate_Fails()
    {
        var state = PersonnelFilePreviousEmploymentPatchState.From(Baseline());
        state.RetirementDate = state.EntryDate.AddDays(-1);

        Assert.True(PersonnelFilePreviousEmploymentPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_NegativeSalary_Fails()
    {
        var state = PersonnelFilePreviousEmploymentPatchState.From(Baseline());
        state.LastSalaryAmount = -1m;

        Assert.True(PersonnelFilePreviousEmploymentPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_BlankCurrencyCode_Fails()
    {
        var state = PersonnelFilePreviousEmploymentPatchState.From(Baseline());
        state.CurrencyCode = " ";

        Assert.True(PersonnelFilePreviousEmploymentPatchApplier.Validate(state).IsFailure);
    }
}
