using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the medical-claim pure rules (response-time derivation, reimbursement signal) and the
/// hardened <see cref="MedicalClaimInputValidator"/> (mandatory insurance, claimant/beneficiary coherence,
/// monetary, currency and date rules — D-02…D-09).
/// </summary>
public sealed class MedicalClaimRulesAndValidatorTests
{
    private static MedicalClaimInput ValidInput() =>
        new(
            Guid.NewGuid(),                                 // InsurancePublicId
            "ACC-1",                                        // AccountNumber
            MedicalClaimClaimantTypes.Titular,              // ClaimantType
            null,                                           // BeneficiaryPublicId
            "AMBULATORIO",                                  // ClaimTypeCode
            "dx",                                           // Diagnosis
            100m,                                           // ClaimAmount
            "USD",                                          // CurrencyCode
            80m,                                            // PaidAmount
            "notes",                                        // Notes
            DateTime.UtcNow.Date.AddDays(-10),              // ClaimDateUtc (past)
            null,                                           // ResolutionDateUtc
            null,                                           // ClaimStatusCode
            null,                                           // SourceSystem
            null,                                           // SourceReference
            null);                                          // SourceSyncedUtc

    private static bool IsValid(MedicalClaimInput input) =>
        new MedicalClaimInputValidator().Validate(input).IsValid;

    // ── Derived response time (D-07) ──────────────────────────────────────────

    [Fact]
    public void DeriveResponseTimeDays_NoResolution_ReturnsNull() =>
        Assert.Null(PersonnelFileMedicalClaim.DeriveResponseTimeDays(DateTime.UtcNow, null));

    [Fact]
    public void DeriveResponseTimeDays_SameDay_ReturnsZero() =>
        Assert.Equal(0, PersonnelFileMedicalClaim.DeriveResponseTimeDays(
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 1, 23, 0, 0, DateTimeKind.Utc)));

    [Fact]
    public void DeriveResponseTimeDays_FiveDaysLater_ReturnsFive() =>
        Assert.Equal(5, PersonnelFileMedicalClaim.DeriveResponseTimeDays(
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 6, 0, 0, 0, DateTimeKind.Utc)));

    [Fact]
    public void DeriveResponseTimeDays_ResolutionBeforeClaim_ReturnsNull() =>
        Assert.Null(PersonnelFileMedicalClaim.DeriveResponseTimeDays(
            new DateTime(2026, 3, 6, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)));

    // ── Reimbursement signal (D-06) ───────────────────────────────────────────

    [Fact]
    public void IsReimbursementOverpay_PaidExceedsClaimed_True() =>
        Assert.True(MedicalClaimRules.IsReimbursementOverpay(100m, 120m));

    [Fact]
    public void IsReimbursementOverpay_PaidWithinClaimed_False() =>
        Assert.False(MedicalClaimRules.IsReimbursementOverpay(100m, 80m));

    [Fact]
    public void IsReimbursementOverpay_NullAmounts_False() =>
        Assert.False(MedicalClaimRules.IsReimbursementOverpay(null, 80m));

    // ── Validator (D-02…D-09) ─────────────────────────────────────────────────

    [Fact]
    public void Validator_ValidInput_Passes() => Assert.True(IsValid(ValidInput()));

    [Fact]
    public void Validator_EmptyInsurance_Fails() =>
        Assert.False(IsValid(ValidInput() with { InsurancePublicId = Guid.Empty }));

    [Fact]
    public void Validator_InvalidClaimantType_Fails() =>
        Assert.False(IsValid(ValidInput() with { ClaimantType = "OTHER" }));

    [Fact]
    public void Validator_BeneficiaryClaimantWithoutBeneficiary_Fails() =>
        Assert.False(IsValid(ValidInput() with { ClaimantType = MedicalClaimClaimantTypes.Beneficiario, BeneficiaryPublicId = null }));

    [Fact]
    public void Validator_BeneficiaryClaimantWithBeneficiary_Passes() =>
        Assert.True(IsValid(ValidInput() with { ClaimantType = MedicalClaimClaimantTypes.Beneficiario, BeneficiaryPublicId = Guid.NewGuid() }));

    [Fact]
    public void Validator_NegativeClaimAmount_Fails() =>
        Assert.False(IsValid(ValidInput() with { ClaimAmount = -1m }));

    [Fact]
    public void Validator_PaidExceedsClaimed_Passes_Reimbursement() =>
        Assert.True(IsValid(ValidInput() with { ClaimAmount = 100m, PaidAmount = 150m }));

    [Fact]
    public void Validator_CurrencyWrongLength_Fails() =>
        Assert.False(IsValid(ValidInput() with { CurrencyCode = "DOLLAR" }));

    [Fact]
    public void Validator_FutureClaimDate_Fails() =>
        Assert.False(IsValid(ValidInput() with { ClaimDateUtc = DateTime.UtcNow.Date.AddDays(10) }));

    [Fact]
    public void Validator_ResolutionBeforeClaim_Fails()
    {
        var input = ValidInput();
        Assert.False(IsValid(input with { ResolutionDateUtc = input.ClaimDateUtc.AddDays(-1) }));
    }

    [Fact]
    public void Validator_ResolutionAfterClaim_Passes()
    {
        var input = ValidInput();
        Assert.True(IsValid(input with { ResolutionDateUtc = input.ClaimDateUtc.AddDays(3) }));
    }
}
