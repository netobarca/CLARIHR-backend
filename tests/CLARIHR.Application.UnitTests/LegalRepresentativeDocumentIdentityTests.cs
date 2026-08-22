using CLARIHR.Application.Features.LegalRepresentatives.Common;
using CLARIHR.Domain.LegalRepresentatives;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Guards what "the same document" means. Identity is (tenant, document type, normalized number), so these
/// invariants are what stops one human being registered twice.
///
/// The second test exists because its absence already cost a real defect: the write handler normalized with
/// one rule while the domain stored with another, so the duplicate check compared a punctuated string
/// against stripped values and could never match. Everything compiled, and the whole suite stayed green.
/// </summary>
public sealed class LegalRepresentativeDocumentIdentityTests
{
    [Theory]
    [InlineData("01234567-8", "012345678")]
    [InlineData("0614-010180-101-2", "06140101801012")]
    [InlineData(" 01234567-8 ", "01234567.8")]
    public void SameDocumentWrittenDifferently_ShouldNormalizeToTheSameValue(string first, string second)
    {
        var a = CreateWithDocumentNumber(first);
        var b = CreateWithDocumentNumber(second);

        Assert.Equal(a.NormalizedDocumentNumber, b.NormalizedDocumentNumber);
    }

    [Theory]
    [InlineData("01234567-8")]
    [InlineData("0614-010180-101-2")]
    [InlineData("AB 123 456")]
    public void HandlerNormalization_ShouldMatchWhatTheDomainStores(string documentNumber)
    {
        // The duplicate check queries with StripSeparators; the row is stored with the domain's rule.
        // If these two ever diverge, duplicates stop being detected — silently.
        var stored = CreateWithDocumentNumber(documentNumber).NormalizedDocumentNumber;
        var queried = LegalRepresentativeValidationRules.StripSeparators(documentNumber);

        Assert.Equal(stored, queried);
    }

    [Theory]
    [InlineData("SV", "DUI", "01234567-8", true)]
    [InlineData("SV", "DUI", "012345678", true)]
    [InlineData("SV", "DUI", "012345678-9", false)]   // ten digits: the number that started all this
    [InlineData("SV", "DUI", "0123456", false)]
    [InlineData("SV", "NIT", "0614-010180-101-2", true)]
    [InlineData("SV", "NIT", "01234567-8", false)]
    [InlineData("SV", "PASSPORT", "AB123456", true)]  // no national format: generic check
    [InlineData("CO", "CC", "1020304050", true)]      // country without rules yet must stay usable
    public void DocumentNumberFormat_ShouldBeValidatedPerCountryAndType(
        string countryCode,
        string documentType,
        string documentNumber,
        bool expected)
    {
        var actual = LegalRepresentativeValidationRules.IsValidDocumentNumber(countryCode, documentType, documentNumber);

        Assert.Equal(expected, actual);
    }

    private static LegalRepresentative CreateWithDocumentNumber(string documentNumber) =>
        LegalRepresentative.Create(
            firstName: "Ana",
            lastName: "Portillo",
            documentType: "DUI",
            documentNumber: documentNumber,
            positionTitle: "Representante Legal",
            representationType: LegalRepresentativeRepresentationType.PrimaryLegalRepresentative,
            authorityDescription: null,
            appointmentInstrument: null,
            appointmentDate: null,
            effectiveFrom: new DateOnly(2026, 1, 1),
            effectiveTo: null,
            email: null,
            phone: null,
            isPrimary: true);
}
