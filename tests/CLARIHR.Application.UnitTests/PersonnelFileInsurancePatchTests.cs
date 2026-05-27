using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical insurance JSON Patch surface: the pure
/// <see cref="PersonnelFileInsurancePatchApplier"/> and the
/// <see cref="PersonnelFileInsurancePatchState"/> projection. The insurance's business
/// <c>Input</c>/<c>PUT</c> contract carries <c>isActive</c> but does not mutate it; the patch
/// surface is the sole writer of <c>isActive</c> (replacing the former dedicated
/// <c>/deactivate</c> endpoint), so the applier must accept boolean values and flag the mutation
/// while preserving the business-field validation the Add/Update commands run. Beneficiaries are a
/// separate nested sub-resource and are never part of the insurance write payload.
/// </summary>
public sealed class PersonnelFileInsurancePatchTests
{
    private static PersonnelFileInsuranceResponse Baseline() =>
        new(
            Guid.NewGuid(),
            "HEALTH",
            50.00m,
            120.00m,
            "RANGE-A",
            "POL-9000",
            250000.00m,
            "USD",
            true,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            [],
            Guid.NewGuid());

    private static PersonnelFileInsurancePatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFileInsurancePatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var state = PersonnelFileInsurancePatchState.From(Baseline());

        Assert.Equal("HEALTH", state.InsuranceCode);
        Assert.Equal(50.00m, state.EmployeeContribution);
        Assert.Equal(250000.00m, state.InsuredAmount);
        Assert.True(state.IsActive);
        Assert.False(state.IsActiveMutated);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var input = PersonnelFileInsurancePatchState.From(Baseline()).ToInput();

        Assert.Equal("HEALTH", input.InsuranceCode);
        Assert.Equal(50.00m, input.EmployeeContribution);
        Assert.Equal(120.00m, input.EmployerContribution);
        Assert.Equal("RANGE-A", input.RangeCode);
        Assert.Equal("POL-9000", input.PolicyNumber);
        Assert.Equal("USD", input.CurrencyCode);
        Assert.True(input.IsActive);
    }

    [Fact]
    public void Apply_ReplaceInsuranceCode_Mutates()
    {
        var state = PersonnelFileInsurancePatchState.From(Baseline());

        Assert.True(PersonnelFileInsurancePatchApplier.Apply([Replace("/insuranceCode", "DENTAL")], state).IsSuccess);
        Assert.Equal("DENTAL", state.InsuranceCode);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceInsuredAmount_AcceptsNumber()
    {
        var state = PersonnelFileInsurancePatchState.From(Baseline());

        Assert.True(PersonnelFileInsurancePatchApplier.Apply([Replace("/insuredAmount", 999999.50m)], state).IsSuccess);
        Assert.Equal(999999.50m, state.InsuredAmount);
    }

    [Fact]
    public void Apply_ReplaceIsActiveFalse_MutatesAndFlagsActiveChange()
    {
        var state = PersonnelFileInsurancePatchState.From(Baseline());

        Assert.True(PersonnelFileInsurancePatchApplier.Apply([Replace("/isActive", false)], state).IsSuccess);
        Assert.False(state.IsActive);
        Assert.True(state.IsActiveMutated);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceIsActiveTrue_MutatesAndFlagsActiveChange()
    {
        var state = PersonnelFileInsurancePatchState.From(Baseline() with { IsActive = false });

        Assert.True(PersonnelFileInsurancePatchApplier.Apply([Replace("/isActive", true)], state).IsSuccess);
        Assert.True(state.IsActive);
        Assert.True(state.IsActiveMutated);
    }

    [Fact]
    public void Apply_BusinessFieldOnly_DoesNotFlagActiveChange()
    {
        var state = PersonnelFileInsurancePatchState.From(Baseline());

        Assert.True(PersonnelFileInsurancePatchApplier.Apply([Replace("/insuredAmount", 1m)], state).IsSuccess);
        Assert.False(state.IsActiveMutated);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_NonBooleanForIsActive_Fails()
    {
        var state = PersonnelFileInsurancePatchState.From(Baseline());

        Assert.True(PersonnelFileInsurancePatchApplier.Apply([Replace("/isActive", "yes")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveIsActive_Fails()
    {
        var state = PersonnelFileInsurancePatchState.From(Baseline());

        Assert.True(PersonnelFileInsurancePatchApplier.Apply([Remove("/isActive")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveOptionalPolicyNumber_ClearsValue()
    {
        var state = PersonnelFileInsurancePatchState.From(Baseline());

        Assert.True(PersonnelFileInsurancePatchApplier.Apply([Remove("/policyNumber")], state).IsSuccess);
        Assert.Null(state.PolicyNumber);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveOptionalInsuredAmount_ClearsValue()
    {
        var state = PersonnelFileInsurancePatchState.From(Baseline());

        Assert.True(PersonnelFileInsurancePatchApplier.Apply([Remove("/insuredAmount")], state).IsSuccess);
        Assert.Null(state.InsuredAmount);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveOptionalStartDateUtc_ClearsValue()
    {
        var state = PersonnelFileInsurancePatchState.From(Baseline());

        Assert.True(PersonnelFileInsurancePatchApplier.Apply([Remove("/startDateUtc")], state).IsSuccess);
        Assert.Null(state.StartDateUtc);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveOptionalEndDateUtc_ClearsValue()
    {
        var state = PersonnelFileInsurancePatchState.From(Baseline());

        Assert.True(PersonnelFileInsurancePatchApplier.Apply([Remove("/endDateUtc")], state).IsSuccess);
        Assert.Null(state.EndDateUtc);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveRequiredInsuranceCode_FailsValidation()
    {
        var state = PersonnelFileInsurancePatchState.From(Baseline());

        Assert.True(PersonnelFileInsurancePatchApplier.Apply([Remove("/insuranceCode")], state).IsSuccess);
        Assert.True(PersonnelFileInsurancePatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_NonNumberForInsuredAmount_Fails()
    {
        var state = PersonnelFileInsurancePatchState.From(Baseline());

        Assert.True(PersonnelFileInsurancePatchApplier.Apply([Replace("/insuredAmount", "not-a-number")], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFileInsurancePatchState.From(Baseline());

        Assert.True(PersonnelFileInsurancePatchApplier.Apply(
            [new PersonnelFileInsurancePatchOperation("copy", "/insuranceCode", "/insuranceCode", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFileInsurancePatchState.From(Baseline());

        Assert.True(PersonnelFileInsurancePatchApplier.Apply([Replace("/unknown", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_Fails()
    {
        var state = PersonnelFileInsurancePatchState.From(Baseline());

        Assert.True(PersonnelFileInsurancePatchApplier.Apply([Replace("/insuranceCode/0", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFileInsurancePatchState.From(Baseline());

        Assert.True(PersonnelFileInsurancePatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_Baseline_Succeeds()
    {
        Assert.True(PersonnelFileInsurancePatchApplier.Validate(PersonnelFileInsurancePatchState.From(Baseline())).IsSuccess);
    }

    [Fact]
    public void Validate_BlankInsuranceCode_Fails()
    {
        var state = PersonnelFileInsurancePatchState.From(Baseline());
        state.InsuranceCode = " ";

        Assert.True(PersonnelFileInsurancePatchApplier.Validate(state).IsFailure);
    }
}
