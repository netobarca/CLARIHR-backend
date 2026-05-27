using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical salary-item JSON Patch surface (the req-10 pilot):
/// the pure <see cref="PersonnelFileSalaryItemPatchApplier"/> and the
/// <see cref="PersonnelFileSalaryItemPatchState"/> projection. The salary item is the only
/// Compensation sub-resource whose <c>isActive</c> flag is patchable (replacing the former
/// dedicated <c>/deactivate</c> endpoint), so the applier must accept boolean values and flag
/// the mutation while preserving the business-field validation the Add/Update commands run.
/// </summary>
public sealed class PersonnelFileSalaryItemPatchTests
{
    private static PersonnelFileSalaryItemResponse Baseline() =>
        new(
            Guid.NewGuid(),
            "BASE",
            "RUBRIC-1",
            "USD",
            "MONTHLY",
            1500.00m,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            true,
            Guid.NewGuid());

    private static PersonnelFileSalaryItemPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFileSalaryItemPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var state = PersonnelFileSalaryItemPatchState.From(Baseline());

        Assert.Equal("BASE", state.IncomeTypeCode);
        Assert.Equal(1500.00m, state.Amount);
        Assert.True(state.IsActive);
        Assert.False(state.IsActiveMutated);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var input = PersonnelFileSalaryItemPatchState.From(Baseline()).ToInput();

        Assert.Equal("BASE", input.IncomeTypeCode);
        Assert.Equal("RUBRIC-1", input.SalaryRubricCode);
        Assert.Equal("USD", input.CurrencyCode);
        Assert.Equal("MONTHLY", input.PayPeriodCode);
        Assert.True(input.IsActive);
    }

    [Fact]
    public void Apply_ReplaceIncomeTypeCode_Mutates()
    {
        var state = PersonnelFileSalaryItemPatchState.From(Baseline());

        Assert.True(PersonnelFileSalaryItemPatchApplier.Apply([Replace("/incomeTypeCode", "BONUS")], state).IsSuccess);
        Assert.Equal("BONUS", state.IncomeTypeCode);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceAmount_AcceptsNumber()
    {
        var state = PersonnelFileSalaryItemPatchState.From(Baseline());

        Assert.True(PersonnelFileSalaryItemPatchApplier.Apply([Replace("/amount", 2000.50m)], state).IsSuccess);
        Assert.Equal(2000.50m, state.Amount);
    }

    [Fact]
    public void Apply_ReplaceIsActiveFalse_MutatesAndFlagsActiveChange()
    {
        var state = PersonnelFileSalaryItemPatchState.From(Baseline());

        Assert.True(PersonnelFileSalaryItemPatchApplier.Apply([Replace("/isActive", false)], state).IsSuccess);
        Assert.False(state.IsActive);
        Assert.True(state.IsActiveMutated);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceIsActiveTrue_MutatesAndFlagsActiveChange()
    {
        var state = PersonnelFileSalaryItemPatchState.From(Baseline() with { IsActive = false });

        Assert.True(PersonnelFileSalaryItemPatchApplier.Apply([Replace("/isActive", true)], state).IsSuccess);
        Assert.True(state.IsActive);
        Assert.True(state.IsActiveMutated);
    }

    [Fact]
    public void Apply_BusinessFieldOnly_DoesNotFlagActiveChange()
    {
        var state = PersonnelFileSalaryItemPatchState.From(Baseline());

        Assert.True(PersonnelFileSalaryItemPatchApplier.Apply([Replace("/amount", 999m)], state).IsSuccess);
        Assert.False(state.IsActiveMutated);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_NonBooleanForIsActive_Fails()
    {
        var state = PersonnelFileSalaryItemPatchState.From(Baseline());

        Assert.True(PersonnelFileSalaryItemPatchApplier.Apply([Replace("/isActive", "yes")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveIsActive_Fails()
    {
        var state = PersonnelFileSalaryItemPatchState.From(Baseline());

        Assert.True(PersonnelFileSalaryItemPatchApplier.Apply([Remove("/isActive")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveEndDate_ClearsValue()
    {
        var state = PersonnelFileSalaryItemPatchState.From(Baseline());

        Assert.True(PersonnelFileSalaryItemPatchApplier.Apply([Remove("/endDate")], state).IsSuccess);
        Assert.Null(state.EndDate);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveStartDate_Fails()
    {
        var state = PersonnelFileSalaryItemPatchState.From(Baseline());

        Assert.True(PersonnelFileSalaryItemPatchApplier.Apply([Remove("/startDate")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveAmount_Fails()
    {
        var state = PersonnelFileSalaryItemPatchState.From(Baseline());

        Assert.True(PersonnelFileSalaryItemPatchApplier.Apply([Remove("/amount")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveRequiredIncomeTypeCode_FailsValidation()
    {
        var state = PersonnelFileSalaryItemPatchState.From(Baseline());

        Assert.True(PersonnelFileSalaryItemPatchApplier.Apply([Remove("/incomeTypeCode")], state).IsSuccess);
        Assert.True(PersonnelFileSalaryItemPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_NonStringForCurrencyCode_Fails()
    {
        var state = PersonnelFileSalaryItemPatchState.From(Baseline());

        Assert.True(PersonnelFileSalaryItemPatchApplier.Apply([Replace("/currencyCode", 42)], state).IsFailure);
    }

    [Fact]
    public void Apply_NonNumberForAmount_Fails()
    {
        var state = PersonnelFileSalaryItemPatchState.From(Baseline());

        Assert.True(PersonnelFileSalaryItemPatchApplier.Apply([Replace("/amount", "not-a-number")], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFileSalaryItemPatchState.From(Baseline());

        Assert.True(PersonnelFileSalaryItemPatchApplier.Apply(
            [new PersonnelFileSalaryItemPatchOperation("copy", "/incomeTypeCode", "/incomeTypeCode", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFileSalaryItemPatchState.From(Baseline());

        Assert.True(PersonnelFileSalaryItemPatchApplier.Apply([Replace("/unknown", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_Fails()
    {
        var state = PersonnelFileSalaryItemPatchState.From(Baseline());

        Assert.True(PersonnelFileSalaryItemPatchApplier.Apply([Replace("/incomeTypeCode/0", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFileSalaryItemPatchState.From(Baseline());

        Assert.True(PersonnelFileSalaryItemPatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_Baseline_Succeeds()
    {
        Assert.True(PersonnelFileSalaryItemPatchApplier.Validate(PersonnelFileSalaryItemPatchState.From(Baseline())).IsSuccess);
    }

    [Fact]
    public void Validate_BlankSalaryRubricCode_Fails()
    {
        var state = PersonnelFileSalaryItemPatchState.From(Baseline());
        state.SalaryRubricCode = " ";

        Assert.True(PersonnelFileSalaryItemPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_NegativeAmount_Fails()
    {
        var state = PersonnelFileSalaryItemPatchState.From(Baseline());
        state.Amount = -1m;

        Assert.True(PersonnelFileSalaryItemPatchApplier.Validate(state).IsFailure);
    }
}
