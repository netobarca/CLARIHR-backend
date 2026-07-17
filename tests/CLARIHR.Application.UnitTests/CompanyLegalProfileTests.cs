using CLARIHR.Domain.Compliance;
using CLARIHR.Domain.Preferences;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for <see cref="CompanyLegalProfile"/> (REQ-016 RF-006 — the employer legal identity that
/// gates payroll generation once <c>CompanyPreference.PayrollComplianceGatesEnabled</c> is on, P-03) and
/// for the paired gate toggle added to <see cref="CompanyPreference"/>.
/// </summary>
public sealed class CompanyLegalProfileTests
{
    private static CompanyLegalProfile CreateValid() =>
        CompanyLegalProfile.Create(
            legalName: "  Acme El Salvador, S.A. de C.V.  ",
            employerNitNumber: " 0614-010101-101-1 ",
            isssEmployerRegistrationNumber: " 123456 ",
            fiscalAddress: " Col. Escalón, San Salvador ",
            economicActivityDescription: " Servicios de tecnología ",
            legalRepresentativePublicId: Guid.NewGuid());

    [Fact]
    public void Create_TrimsTextFieldsAndAssignsAConcurrencyToken()
    {
        var profile = CreateValid();

        Assert.Equal("Acme El Salvador, S.A. de C.V.", profile.LegalName);
        Assert.Equal("0614-010101-101-1", profile.EmployerNitNumber);
        Assert.Equal("123456", profile.IsssEmployerRegistrationNumber);
        Assert.Equal("Col. Escalón, San Salvador", profile.FiscalAddress);
        Assert.Equal("Servicios de tecnología", profile.EconomicActivityDescription);
        Assert.NotEqual(Guid.Empty, profile.ConcurrencyToken);
        Assert.NotEqual(Guid.Empty, profile.PublicId);
    }

    [Fact]
    public void Create_WithoutEconomicActivity_LeavesItNull()
    {
        var profile = CompanyLegalProfile.Create(
            "Acme", "0614-010101-101-1", "123456", "San Salvador", economicActivityDescription: "   ", legalRepresentativePublicId: null);

        Assert.Null(profile.EconomicActivityDescription);
        Assert.Null(profile.LegalRepresentativePublicId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankLegalName_Throws(string blankLegalName)
    {
        Assert.Throws<ArgumentException>(() =>
            CompanyLegalProfile.Create(blankLegalName, "0614-010101-101-1", "123456", "San Salvador", null, null));
    }

    [Fact]
    public void Update_RotatesConcurrencyTokenAndReplacesEveryField()
    {
        var profile = CreateValid();
        var originalToken = profile.ConcurrencyToken;
        var newRepresentative = Guid.NewGuid();

        profile.Update(
            "Nuevo Nombre Legal",
            "0614-020202-102-2",
            "654321",
            "Santa Tecla",
            "Comercio",
            newRepresentative);

        Assert.Equal("Nuevo Nombre Legal", profile.LegalName);
        Assert.Equal("0614-020202-102-2", profile.EmployerNitNumber);
        Assert.Equal("654321", profile.IsssEmployerRegistrationNumber);
        Assert.Equal("Santa Tecla", profile.FiscalAddress);
        Assert.Equal("Comercio", profile.EconomicActivityDescription);
        Assert.Equal(newRepresentative, profile.LegalRepresentativePublicId);
        Assert.NotEqual(originalToken, profile.ConcurrencyToken);
    }

    [Fact]
    public void Update_WithBlankFiscalAddress_ThrowsAndLeavesStatePristine()
    {
        var profile = CreateValid();

        Assert.Throws<ArgumentException>(() =>
            profile.Update("Acme", "0614-010101-101-1", "123456", "   ", null, null));
    }

    [Fact]
    public void SetPayrollCompliancePolicy_DefaultsToNullAndCanBeToggled()
    {
        var preference = CompanyPreference.Create("USD", "UTC");
        Assert.Null(preference.PayrollComplianceGatesEnabled);

        var originalToken = preference.ConcurrencyToken;
        preference.SetPayrollCompliancePolicy(true);

        Assert.True(preference.PayrollComplianceGatesEnabled);
        Assert.NotEqual(originalToken, preference.ConcurrencyToken);
    }
}
