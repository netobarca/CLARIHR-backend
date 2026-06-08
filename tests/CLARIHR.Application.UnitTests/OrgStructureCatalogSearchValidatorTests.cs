using CLARIHR.Application.Features.OrgStructureCatalogs;
using FluentValidation.Results;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// OSC-004 (§12.8 / OrgUnits OU-002): every Organization Structure Catalogs free-text search validator
/// must reject a <c>q</c> shorter than <c>OrgStructureCatalogValidationRules.MinSearchLength</c> (after
/// Trim), so the non-sargable <c>Normalized*.Contains(q)</c> <c>LIKE '%x%'</c> scan cannot be triggered
/// by a 1-char query. Empty/whitespace = "no filter" (valid); a <c>q</c> at or above the minimum is
/// valid. Covers both the unit-types and functional-areas search validators.
/// </summary>
public sealed class OrgStructureCatalogSearchValidatorTests
{
    [Fact]
    public void UnitTypesSearch_EnforcesMinSearchLength()
    {
        var validator = new SearchOrgUnitTypesQueryValidator();
        AssertMinSearchLength(search =>
            validator.Validate(new SearchOrgUnitTypesQuery(Guid.NewGuid(), IsActive: null, search)));
    }

    [Fact]
    public void FunctionalAreasSearch_EnforcesMinSearchLength()
    {
        var validator = new SearchFunctionalAreasQueryValidator();
        AssertMinSearchLength(search =>
            validator.Validate(new SearchFunctionalAreasQuery(Guid.NewGuid(), IsActive: null, search)));
    }

    private static void AssertMinSearchLength(Func<string?, ValidationResult> validate)
    {
        foreach (var tooShort in new[] { "a", "  b  " })
        {
            var result = validate(tooShort);
            Assert.False(result.IsValid, $"Expected '{tooShort}' to be rejected (below MinSearchLength).");
            Assert.Contains(result.Errors, error => error.PropertyName == "Search");
        }

        foreach (var acceptable in new[] { null, "", "   ", "ab", "Division" })
        {
            Assert.True(validate(acceptable).IsValid, $"Expected '{acceptable ?? "<null>"}' to be valid.");
        }
    }
}
