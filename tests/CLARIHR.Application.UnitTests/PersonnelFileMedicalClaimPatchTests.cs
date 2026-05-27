using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical medical-claim JSON Patch surface: the pure
/// <see cref="PersonnelFileMedicalClaimPatchApplier"/> and the
/// <see cref="PersonnelFileMedicalClaimPatchState"/> projection. The medical claim's business
/// <c>Input</c>/<c>PUT</c> contract does not carry <c>isActive</c>, but the patch surface still
/// supports toggling it (replacing the former dedicated <c>/deactivate</c> endpoint), so the
/// applier must accept boolean values and flag the mutation while preserving the business-field
/// validation the Add/Update commands run.
/// </summary>
public sealed class PersonnelFileMedicalClaimPatchTests
{
    private static PersonnelFileMedicalClaimResponse Baseline() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "ACC-100",
            "OUTPATIENT",
            "Routine checkup.",
            1200.00m,
            "USD",
            900.00m,
            5,
            "Reimbursed via payroll.",
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            "LEGACY",
            "REF-1",
            new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc),
            true,
            Guid.NewGuid());

    private static PersonnelFileMedicalClaimPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFileMedicalClaimPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var baseline = Baseline();
        var state = PersonnelFileMedicalClaimPatchState.From(baseline);

        Assert.Equal("OUTPATIENT", state.ClaimTypeCode);
        Assert.Equal(baseline.InsuranceId, state.InsurancePublicId);
        Assert.Equal(1200.00m, state.ClaimAmount);
        Assert.Equal(5, state.ResponseTimeDays);
        Assert.True(state.IsActive);
        Assert.False(state.IsActiveMutated);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var baseline = Baseline();
        var input = PersonnelFileMedicalClaimPatchState.From(baseline).ToInput();

        Assert.Equal("OUTPATIENT", input.ClaimTypeCode);
        Assert.Equal(baseline.InsuranceId, input.InsurancePublicId);
        Assert.Equal(1200.00m, input.ClaimAmount);
        Assert.Equal("USD", input.CurrencyCode);
        Assert.Equal(5, input.ResponseTimeDays);
    }

    [Fact]
    public void Apply_ReplaceClaimTypeCode_Mutates()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply([Replace("/claimTypeCode", "INPATIENT")], state).IsSuccess);
        Assert.Equal("INPATIENT", state.ClaimTypeCode);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceClaimAmount_AcceptsNumber()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply([Replace("/claimAmount", 2500.75m)], state).IsSuccess);
        Assert.Equal(2500.75m, state.ClaimAmount);
    }

    [Fact]
    public void Apply_ReplaceResponseTimeDays_AcceptsInteger()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply([Replace("/responseTimeDays", 12)], state).IsSuccess);
        Assert.Equal(12, state.ResponseTimeDays);
    }

    [Fact]
    public void Apply_ReplaceIsActiveFalse_MutatesAndFlagsActiveChange()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply([Replace("/isActive", false)], state).IsSuccess);
        Assert.False(state.IsActive);
        Assert.True(state.IsActiveMutated);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceIsActiveTrue_MutatesAndFlagsActiveChange()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline() with { IsActive = false });

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply([Replace("/isActive", true)], state).IsSuccess);
        Assert.True(state.IsActive);
        Assert.True(state.IsActiveMutated);
    }

    [Fact]
    public void Apply_BusinessFieldOnly_DoesNotFlagActiveChange()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply([Replace("/claimAmount", 1m)], state).IsSuccess);
        Assert.False(state.IsActiveMutated);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_NonBooleanForIsActive_Fails()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply([Replace("/isActive", "yes")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveIsActive_Fails()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply([Remove("/isActive")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveOptionalDiagnosis_ClearsValue()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply([Remove("/diagnosis")], state).IsSuccess);
        Assert.Null(state.Diagnosis);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveOptionalClaimAmount_ClearsValue()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply([Remove("/claimAmount")], state).IsSuccess);
        Assert.Null(state.ClaimAmount);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveOptionalInsurancePublicId_ClearsValue()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply([Remove("/insurancePublicId")], state).IsSuccess);
        Assert.Null(state.InsurancePublicId);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveOptionalSourceSyncedUtc_ClearsValue()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply([Remove("/sourceSyncedUtc")], state).IsSuccess);
        Assert.Null(state.SourceSyncedUtc);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveClaimDateUtc_Fails()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply([Remove("/claimDateUtc")], state).IsFailure);
    }

    [Fact]
    public void Apply_RemoveRequiredClaimTypeCode_FailsValidation()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply([Remove("/claimTypeCode")], state).IsSuccess);
        Assert.True(PersonnelFileMedicalClaimPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Apply_NonNumberForClaimAmount_Fails()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply([Replace("/claimAmount", "not-a-number")], state).IsFailure);
    }

    [Fact]
    public void Apply_NonIntegerForResponseTimeDays_Fails()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply([Replace("/responseTimeDays", "not-an-int")], state).IsFailure);
    }

    [Fact]
    public void Apply_NonGuidForInsurancePublicId_Fails()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply([Replace("/insurancePublicId", "not-a-guid")], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_Fails()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply(
            [new PersonnelFileMedicalClaimPatchOperation("copy", "/claimTypeCode", "/claimTypeCode", null)], state).IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedPath_Fails()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply([Replace("/unknown", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_Fails()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply([Replace("/claimTypeCode/0", "x")], state).IsFailure);
    }

    [Fact]
    public void Apply_NoOperations_DoesNotMutate()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply([], state).IsSuccess);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void Validate_Baseline_Succeeds()
    {
        Assert.True(PersonnelFileMedicalClaimPatchApplier.Validate(PersonnelFileMedicalClaimPatchState.From(Baseline())).IsSuccess);
    }

    [Fact]
    public void Validate_BlankClaimTypeCode_Fails()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());
        state.ClaimTypeCode = " ";

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Validate(state).IsFailure);
    }
}
