using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the pure insurance rules module (Fase 1): anti-duplicate policy per employee and
/// anti-duplicate beneficiary per insurance (D-13), and the primary-beneficiary allocation cap (D-09).
/// All checks operate on already-loaded sibling collections, so they are exercised without a database.
/// </summary>
public sealed class InsuranceRulesTests
{
    private static InsuranceRules.ExistingInsurance Insurance(string? policy) =>
        new(Guid.NewGuid(), InsuranceRules.NormalizePolicy(policy));

    private static InsuranceRules.ExistingBeneficiary Beneficiary(
        string? documentTypeCode,
        string? documentNumber,
        bool isActive = true,
        bool isPrimary = true,
        decimal? allocation = null) =>
        new(Guid.NewGuid(), InsuranceRules.NormalizeDocumentKey(documentTypeCode, documentNumber), isActive, isPrimary, allocation);

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("PRINCIPAL", true)]
    [InlineData("principal", true)]
    [InlineData("CONTINGENTE", false)]
    public void IsPrimary_DefaultsToPrimaryWhenUnset(string? type, bool expected) =>
        Assert.Equal(expected, InsuranceRules.IsPrimary(type));

    [Fact]
    public void CheckPolicyUnique_DistinctPolicies_Succeeds() =>
        Assert.True(InsuranceRules.CheckPolicyUnique(null, "POL-2", [Insurance("POL-1")]).IsSuccess);

    [Fact]
    public void CheckPolicyUnique_SamePolicyCaseInsensitive_Fails() =>
        Assert.True(InsuranceRules.CheckPolicyUnique(null, "pol-1", [Insurance("POL-1")]).IsFailure);

    [Fact]
    public void CheckPolicyUnique_NoPolicy_Succeeds() =>
        Assert.True(InsuranceRules.CheckPolicyUnique(null, null, [Insurance("POL-1")]).IsSuccess);

    [Fact]
    public void CheckPolicyUnique_SamePolicyOnSelf_Succeeds()
    {
        var self = Insurance("POL-1");
        Assert.True(InsuranceRules.CheckPolicyUnique(self.PublicId, "POL-1", [self]).IsSuccess);
    }

    [Fact]
    public void CheckBeneficiaryUnique_SameActiveDocument_Fails() =>
        Assert.True(InsuranceRules.CheckBeneficiaryUnique(null, "DUI", "123", [Beneficiary("DUI", "123")]).IsFailure);

    [Fact]
    public void CheckBeneficiaryUnique_InactiveDuplicate_Succeeds() =>
        Assert.True(InsuranceRules.CheckBeneficiaryUnique(null, "DUI", "123", [Beneficiary("DUI", "123", isActive: false)]).IsSuccess);

    [Fact]
    public void CheckBeneficiaryUnique_DifferentDocumentType_Succeeds() =>
        Assert.True(InsuranceRules.CheckBeneficiaryUnique(null, "PASSPORT", "123", [Beneficiary("DUI", "123")]).IsSuccess);

    [Fact]
    public void CheckBeneficiaryUnique_NoDocument_Succeeds() =>
        Assert.True(InsuranceRules.CheckBeneficiaryUnique(null, "DUI", null, [Beneficiary("DUI", "123")]).IsSuccess);

    [Fact]
    public void CheckPrimaryAllocation_WithinCap_Succeeds() =>
        Assert.True(InsuranceRules.CheckPrimaryAllocation(null, true, "PRINCIPAL", 40m, [Beneficiary("DUI", "1", allocation: 60m)]).IsSuccess);

    [Fact]
    public void CheckPrimaryAllocation_ExactlyHundred_Succeeds() =>
        Assert.True(InsuranceRules.CheckPrimaryAllocation(null, true, "PRINCIPAL", 50m, [Beneficiary("DUI", "1", allocation: 50m)]).IsSuccess);

    [Fact]
    public void CheckPrimaryAllocation_OverHundred_Fails() =>
        Assert.True(InsuranceRules.CheckPrimaryAllocation(null, true, "PRINCIPAL", 50m, [Beneficiary("DUI", "1", allocation: 60m)]).IsFailure);

    [Fact]
    public void CheckPrimaryAllocation_ContingentCandidate_DoesNotCount() =>
        Assert.True(InsuranceRules.CheckPrimaryAllocation(null, true, "CONTINGENTE", 100m, [Beneficiary("DUI", "1", allocation: 90m)]).IsSuccess);

    [Fact]
    public void CheckPrimaryAllocation_InactiveCandidate_DoesNotCount() =>
        Assert.True(InsuranceRules.CheckPrimaryAllocation(null, false, "PRINCIPAL", 100m, [Beneficiary("DUI", "1", allocation: 90m)]).IsSuccess);

    [Fact]
    public void CheckPrimaryAllocation_InactiveOrContingentSiblings_DoNotCount() =>
        Assert.True(InsuranceRules.CheckPrimaryAllocation(
            null,
            true,
            "PRINCIPAL",
            100m,
            [
                Beneficiary("DUI", "1", isActive: false, allocation: 90m),
                Beneficiary("DUI", "2", isPrimary: false, allocation: 90m)
            ]).IsSuccess);
}
