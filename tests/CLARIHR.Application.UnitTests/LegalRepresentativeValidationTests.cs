using CLARIHR.Application.Features.LegalRepresentatives;
using CLARIHR.Application.Features.LegalRepresentatives.Common;
using CLARIHR.Domain.LegalRepresentatives;
using FluentValidation.TestHelper;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// §LR2 — the <c>DocumentType</c> validators must match the <c>document_type varchar(40)</c>
/// column (<see cref="LegalRepresentativeValidationRules.MaxDocumentTypeLength"/>), so an
/// over-length value is rejected with a clean 400 at validation instead of escaping to an
/// unmapped PostgreSQL "value too long" → HTTP 500. Covers the Create and Update command
/// validators (the wire paths).
/// </summary>
public sealed class LegalRepresentativeValidationTests
{
    [Fact]
    public void CreateValidator_WhenDocumentTypeExceedsColumnWidth_ShouldFail()
    {
        var validator = new CreateLegalRepresentativeCommandValidator();
        var command = CreateCommand(new string('A', LegalRepresentativeValidationRules.MaxDocumentTypeLength + 1));

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.DocumentType);
    }

    [Fact]
    public void CreateValidator_WhenDocumentTypeIsAtColumnWidth_ShouldPass()
    {
        var validator = new CreateLegalRepresentativeCommandValidator();
        var command = CreateCommand(new string('A', LegalRepresentativeValidationRules.MaxDocumentTypeLength));

        var result = validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(c => c.DocumentType);
    }

    [Fact]
    public void UpdateValidator_WhenDocumentTypeExceedsColumnWidth_ShouldFail()
    {
        var validator = new UpdateLegalRepresentativeCommandValidator();
        var command = UpdateCommand(new string('A', LegalRepresentativeValidationRules.MaxDocumentTypeLength + 1));

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.DocumentType);
    }

    [Theory]
    [InlineData("a")]
    [InlineData(" a ")]
    public void SearchValidator_WhenSearchBelowMinLength_ShouldFail(string search)
    {
        var validator = new SearchLegalRepresentativesQueryValidator();
        var query = new SearchLegalRepresentativesQuery(Guid.NewGuid(), null, null, null, search);

        var result = validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Search);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    public void SearchValidator_WhenSearchEmptyOrAtMinLength_ShouldPass(string? search)
    {
        var validator = new SearchLegalRepresentativesQueryValidator();
        var query = new SearchLegalRepresentativesQuery(Guid.NewGuid(), null, null, null, search);

        var result = validator.TestValidate(query);

        result.ShouldNotHaveValidationErrorFor(q => q.Search);
    }

    [Fact]
    public void ExportValidator_WhenSearchBelowMinLength_ShouldFail()
    {
        var validator = new ExportLegalRepresentativesQueryValidator();
        var query = new ExportLegalRepresentativesQuery(Guid.NewGuid(), null, null, null, "a");

        var result = validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Search);
    }

    private static CreateLegalRepresentativeCommand CreateCommand(string documentType) =>
        new(
            CompanyId: Guid.NewGuid(),
            FirstName: "Jane",
            LastName: "Doe",
            DocumentType: documentType,
            DocumentNumber: "ABC123",
            PositionTitle: "CEO",
            RepresentationType: LegalRepresentativeRepresentationType.PrimaryLegalRepresentative,
            AuthorityDescription: null,
            AppointmentInstrument: null,
            AppointmentDateUtc: null,
            EffectiveFromUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveToUtc: null,
            Email: null,
            Phone: null,
            IsPrimary: false);

    private static UpdateLegalRepresentativeCommand UpdateCommand(string documentType) =>
        new(
            LegalRepresentativeId: Guid.NewGuid(),
            FirstName: "Jane",
            LastName: "Doe",
            DocumentType: documentType,
            DocumentNumber: "ABC123",
            PositionTitle: "CEO",
            RepresentationType: LegalRepresentativeRepresentationType.PrimaryLegalRepresentative,
            AuthorityDescription: null,
            AppointmentInstrument: null,
            AppointmentDateUtc: null,
            EffectiveFromUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveToUtc: null,
            Email: null,
            Phone: null,
            IsPrimary: false,
            ConcurrencyToken: Guid.NewGuid());
}
