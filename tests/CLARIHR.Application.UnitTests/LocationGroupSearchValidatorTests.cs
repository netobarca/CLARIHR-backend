using CLARIHR.Application.Features.Locations.Groups;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// §LG5 (§12.8): the LocationGroups search validator must reject a free-text <c>q</c> shorter than
/// <c>LocationValidationRules.MinSearchLength</c> (after Trim), so the non-sargable
/// <c>Normalized*.Contains(q)</c> <c>LIKE '%x%'</c> scan cannot be triggered by a 1-char query. An
/// empty/whitespace <c>q</c> means "no filter" (valid); a <c>q</c> at or above the minimum is valid.
/// </summary>
public sealed class LocationGroupSearchValidatorTests
{
    private static readonly SearchLocationGroupsQueryValidator Validator = new();

    private static SearchLocationGroupsQuery Query(string? search) =>
        new(Guid.NewGuid(), LevelOrder: null, IsActive: null, search);

    [Theory]
    [InlineData("a")]
    [InlineData("  b  ")] // trims to a single character
    public void Search_BelowMinLength_IsInvalid(string search)
    {
        var result = Validator.Validate(Query(search));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(SearchLocationGroupsQuery.Search));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    [InlineData("San Salvador")]
    public void Search_EmptyOrAtLeastMinLength_IsValid(string? search)
    {
        var result = Validator.Validate(Query(search));

        Assert.True(result.IsValid);
    }
}
