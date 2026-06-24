using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the canonical medical-claim JSON Patch surface: the pure
/// <see cref="PersonnelFileMedicalClaimPatchApplier"/> and the
/// <see cref="PersonnelFileMedicalClaimPatchState"/> projection. After the Fase-1 hardening the insurance is
/// mandatory (non-removable), the claimant dimension and resolution/status fields are patchable, and the
/// response time is derived (no longer a patch segment).
/// </summary>
public sealed class PersonnelFileMedicalClaimPatchTests
{
    private static PersonnelFileMedicalClaimResponse Baseline() =>
        new(
            Guid.NewGuid(),                                                 // MedicalClaimPublicId
            Guid.NewGuid(),                                                 // InsuranceId
            "SEGURO-VIDA",                                                  // InsuranceName
            "ACC-100",                                                      // AccountNumber
            MedicalClaimClaimantTypes.Titular,                             // ClaimantType
            null,                                                           // BeneficiaryPublicId
            null,                                                           // PatientName
            null,                                                           // KinshipCode
            "AMBULATORIO",                                                  // ClaimTypeCode
            "Routine checkup.",                                             // Diagnosis
            1200.00m,                                                       // ClaimAmount
            "USD",                                                          // CurrencyCode
            900.00m,                                                        // PaidAmount
            5,                                                              // ResponseTimeDays (derived)
            "Reimbursed via payroll.",                                      // Notes
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),           // ClaimDateUtc
            new DateTime(2026, 3, 6, 0, 0, 0, DateTimeKind.Utc),           // ResolutionDateUtc
            "PRESENTADO",                                                   // ClaimStatusCode
            "LEGACY",                                                       // SourceSystem
            "REF-1",                                                        // SourceReference
            new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc),           // SourceSyncedUtc
            true,                                                           // IsActive
            Guid.NewGuid());                                                // ConcurrencyToken

    private static PersonnelFileMedicalClaimPatchOperation Replace<T>(string path, T value) =>
        new("replace", path, null, JsonSerializer.SerializeToElement(value));

    private static PersonnelFileMedicalClaimPatchOperation Remove(string path) =>
        new("remove", path, null, null);

    [Fact]
    public void From_MapsResponse()
    {
        var baseline = Baseline();
        var state = PersonnelFileMedicalClaimPatchState.From(baseline);

        Assert.Equal("AMBULATORIO", state.ClaimTypeCode);
        Assert.Equal(baseline.InsuranceId, state.InsurancePublicId);
        Assert.Equal(MedicalClaimClaimantTypes.Titular, state.ClaimantType);
        Assert.Equal(1200.00m, state.ClaimAmount);
        Assert.Equal(baseline.ResolutionDateUtc, state.ResolutionDateUtc);
        Assert.Equal("PRESENTADO", state.ClaimStatusCode);
        Assert.True(state.IsActive);
        Assert.False(state.IsActiveMutated);
        Assert.False(state.HasMutation);
    }

    [Fact]
    public void ToInput_RoundTrips()
    {
        var baseline = Baseline();
        var input = PersonnelFileMedicalClaimPatchState.From(baseline).ToInput();

        Assert.Equal("AMBULATORIO", input.ClaimTypeCode);
        Assert.Equal(baseline.InsuranceId, input.InsurancePublicId);
        Assert.Equal(MedicalClaimClaimantTypes.Titular, input.ClaimantType);
        Assert.Equal(1200.00m, input.ClaimAmount);
        Assert.Equal("USD", input.CurrencyCode);
        Assert.Equal(baseline.ResolutionDateUtc, input.ResolutionDateUtc);
        Assert.Equal("PRESENTADO", input.ClaimStatusCode);
    }

    [Fact]
    public void Apply_ReplaceClaimTypeCode_Mutates()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply([Replace("/claimTypeCode", "HOSPITALARIO")], state).IsSuccess);
        Assert.Equal("HOSPITALARIO", state.ClaimTypeCode);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceClaimantTypeAndBeneficiary_Mutates()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());
        var beneficiaryId = Guid.NewGuid();

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply(
            [Replace("/claimantType", "BENEFICIARIO"), Replace("/beneficiaryPublicId", beneficiaryId)], state).IsSuccess);
        Assert.Equal("BENEFICIARIO", state.ClaimantType);
        Assert.Equal(beneficiaryId, state.BeneficiaryPublicId);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_ReplaceResolutionDateAndStatus_Mutates()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply(
            [Replace("/resolutionDateUtc", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)), Replace("/claimStatusCode", "PAGADO")], state).IsSuccess);
        Assert.Equal(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), state.ResolutionDateUtc);
        Assert.Equal("PAGADO", state.ClaimStatusCode);
    }

    [Fact]
    public void Apply_ReplaceClaimAmount_AcceptsNumber()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply([Replace("/claimAmount", 2500.75m)], state).IsSuccess);
        Assert.Equal(2500.75m, state.ClaimAmount);
    }

    [Fact]
    public void Apply_ReplaceResponseTimeDays_Fails_BecauseDerived()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        // responseTimeDays is derived (D-07) and is no longer a patchable segment.
        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply([Replace("/responseTimeDays", 12)], state).IsFailure);
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
    public void Apply_RemoveOptionalResolutionDate_ClearsValue()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply([Remove("/resolutionDateUtc")], state).IsSuccess);
        Assert.Null(state.ResolutionDateUtc);
        Assert.True(state.HasMutation);
    }

    [Fact]
    public void Apply_RemoveInsurancePublicId_Fails_BecauseRequired()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply([Remove("/insurancePublicId")], state).IsFailure);
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
    public void Apply_NonGuidForInsurancePublicId_Fails()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply([Replace("/insurancePublicId", "not-a-guid")], state).IsFailure);
    }

    [Fact]
    public void Apply_EmptyGuidForInsurancePublicId_Fails()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Apply([Replace("/insurancePublicId", Guid.Empty)], state).IsFailure);
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

    [Fact]
    public void Validate_EmptyInsurancePublicId_Fails()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());
        state.InsurancePublicId = Guid.Empty;

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_BeneficiaryClaimantWithoutBeneficiary_Fails()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());
        state.ClaimantType = MedicalClaimClaimantTypes.Beneficiario;
        state.BeneficiaryPublicId = null;

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_BeneficiaryClaimantWithBeneficiary_Succeeds()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());
        state.ClaimantType = MedicalClaimClaimantTypes.Beneficiario;
        state.BeneficiaryPublicId = Guid.NewGuid();

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Validate(state).IsSuccess);
    }

    [Fact]
    public void Validate_InvalidClaimantType_Fails()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());
        state.ClaimantType = "OTHER";

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Validate(state).IsFailure);
    }

    [Fact]
    public void Validate_ResolutionBeforeClaim_Fails()
    {
        var state = PersonnelFileMedicalClaimPatchState.From(Baseline());
        state.ResolutionDateUtc = state.ClaimDateUtc.AddDays(-1);

        Assert.True(PersonnelFileMedicalClaimPatchApplier.Validate(state).IsFailure);
    }
}
