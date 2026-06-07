using CLARIHR.Application.Features.CostCenters;
using CLARIHR.Application.Features.CostCenters.Common;
using CLARIHR.Application.Features.LegalRepresentatives.Common;
using FluentValidation.TestHelper;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// R4 (§LR3 / §12.8) — the Cost Centers free-text search (NormalizedCode/NormalizedName
/// <c>Contains</c> → non-sargable <c>LIKE '%x%'</c>) must enforce a minimum trimmed length in the
/// query validators, so a 1-char term is rejected with a clean 400 before it reaches the DB. Covers
/// both wire paths (Search list + Export). Mirrors <see cref="LegalRepresentativeValidationTests"/>.
/// </summary>
public sealed class CostCenterSearchValidatorTests
{
    [Theory]
    [InlineData("a")]
    [InlineData(" a ")]
    public void SearchValidator_WhenSearchBelowMinLength_ShouldFail(string search)
    {
        var validator = new SearchCostCentersQueryValidator();
        var query = new SearchCostCentersQuery(Guid.NewGuid(), null, null, search);

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
        var validator = new SearchCostCentersQueryValidator();
        var query = new SearchCostCentersQuery(Guid.NewGuid(), null, null, search);

        var result = validator.TestValidate(query);

        result.ShouldNotHaveValidationErrorFor(q => q.Search);
    }

    [Fact]
    public void ExportValidator_WhenSearchBelowMinLength_ShouldFail()
    {
        var validator = new ExportCostCentersQueryValidator();
        var query = new ExportCostCentersQuery(Guid.NewGuid(), null, null, "a");

        var result = validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Search);
    }

    // Drift guard: the threshold must stay aligned with the established §LR3 precedent (2).
    [Fact]
    public void CostCenter_MinSearchLength_ShouldMatchLegalRepresentativePrecedent() =>
        Assert.Equal(
            LegalRepresentativeValidationRules.MinSearchLength,
            CostCenterValidationRules.MinSearchLength);
}
