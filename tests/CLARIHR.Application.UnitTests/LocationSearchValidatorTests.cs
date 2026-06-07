using CLARIHR.Application.Features.Locations.Groups;
using CLARIHR.Application.Features.Locations.WorkCenters;
using CLARIHR.Application.Features.Locations.WorkCenterTypes;
using FluentValidation.Results;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// §LG5 (§12.8), feature-wide: every Locations free-text search validator must reject a <c>q</c> shorter
/// than <c>LocationValidationRules.MinSearchLength</c> (after Trim), so the non-sargable
/// <c>Normalized*.Contains(q)</c> <c>LIKE '%x%'</c> scan cannot be triggered by a 1-char query. An
/// empty/whitespace <c>q</c> means "no filter" (valid); a <c>q</c> at or above the minimum is valid.
/// Covers the LocationGroups, WorkCenters and WorkCenterTypes search validators.
/// </summary>
public sealed class LocationSearchValidatorTests
{
    [Fact]
    public void LocationGroupsSearch_EnforcesMinSearchLength()
    {
        var validator = new SearchLocationGroupsQueryValidator();
        AssertMinSearchLength(search =>
            validator.Validate(new SearchLocationGroupsQuery(Guid.NewGuid(), LevelOrder: null, IsActive: null, search)));
    }

    [Fact]
    public void WorkCentersSearch_EnforcesMinSearchLength()
    {
        var validator = new SearchWorkCentersQueryValidator();
        AssertMinSearchLength(search =>
            validator.Validate(new SearchWorkCentersQuery(Guid.NewGuid(), GroupId: null, TypeId: null, IsActive: null, search)));
    }

    [Fact]
    public void WorkCenterTypesSearch_EnforcesMinSearchLength()
    {
        var validator = new GetWorkCenterTypesQueryValidator();
        AssertMinSearchLength(search =>
            validator.Validate(new GetWorkCenterTypesQuery(Guid.NewGuid(), IsActive: null, search)));
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

        foreach (var acceptable in new[] { null, "", "   ", "ab", "San Salvador" })
        {
            Assert.True(validate(acceptable).IsValid, $"Expected '{acceptable ?? "<null>"}' to be valid.");
        }
    }
}
