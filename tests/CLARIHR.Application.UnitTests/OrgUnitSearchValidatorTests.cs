using CLARIHR.Application.Features.OrgUnits;
using FluentValidation.Results;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// OU-002 (§12.8 / Locations §LG5): every Org Units free-text search validator must reject a <c>q</c>
/// shorter than <c>OrgUnitValidationRules.MinSearchLength</c> (after Trim), so the non-sargable
/// <c>Normalized{Code,Name}.Contains(q)</c> <c>LIKE '%x%'</c> scan over 6 columns + 4 joins cannot be
/// triggered by a 1-char query. An empty/whitespace <c>q</c> means "no filter" (valid); a <c>q</c> at or
/// above the minimum is valid. Covers both the paged <c>Search</c> and the <c>Export</c> validators —
/// the two free-text entry points that share <c>OrgUnitRepository.SearchAsync</c>.
/// </summary>
public sealed class OrgUnitSearchValidatorTests
{
    [Fact]
    public void OrgUnitsSearch_EnforcesMinSearchLength()
    {
        var validator = new SearchOrgUnitsQueryValidator();
        AssertMinSearchLength(search =>
            validator.Validate(new SearchOrgUnitsQuery(
                Guid.NewGuid(), IsActive: null, search, OrgUnitTypeId: null, FunctionalAreaId: null, ParentId: null)));
    }

    [Fact]
    public void OrgUnitsExport_EnforcesMinSearchLength()
    {
        var validator = new GetOrgUnitExportRowsQueryValidator();
        AssertMinSearchLength(search =>
            validator.Validate(new GetOrgUnitExportRowsQuery(
                Guid.NewGuid(), IsActive: null, search, OrgUnitTypeId: null, FunctionalAreaId: null, ParentId: null)));
    }

    // Shared §12.8 contract: q below the minimum (after Trim) is rejected on the Search property;
    // empty/whitespace = no filter (valid); q at or above the minimum is valid.
    private static void AssertMinSearchLength(Func<string?, ValidationResult> validate)
    {
        foreach (var tooShort in new[] { "a", "  b  " })
        {
            var result = validate(tooShort);
            Assert.False(result.IsValid, $"Expected '{tooShort}' to be rejected (below MinSearchLength).");
            Assert.Contains(result.Errors, error => error.PropertyName == "Search");
        }

        foreach (var acceptable in new[] { null, "", "   ", "ab", "Engineering" })
        {
            Assert.True(validate(acceptable).IsValid, $"Expected '{acceptable ?? "<null>"}' to be valid.");
        }
    }
}
