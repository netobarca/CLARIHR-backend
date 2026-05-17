using CLARIHR.Application.Features.PositionDescriptionCatalogs;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using CLARIHR.Domain.PositionDescriptionCatalogs;
using FluentValidation;

namespace CLARIHR.Application.UnitTests;

// P2 guardrail: free-text `q` on the 3 catalog search endpoints must reject
// sub-minimum-length terms (non-sargable LIKE '%x%'). Empty/whitespace stays
// "no filter" (valid). See project-foundation.md §12.8 / ADR-0002.
public sealed class PositionDescriptionCatalogSearchValidatorTests
{
    private static readonly IValidator<SearchPositionDescriptionCatalogItemsQuery> ItemsValidator =
        new SearchPositionDescriptionCatalogItemsQueryValidator();

    private static readonly IValidator<SearchPositionCategoryClassificationsQuery> ClassificationsValidator =
        new SearchPositionCategoryClassificationsQueryValidator();

    private static readonly IValidator<SearchPositionCategoriesQuery> CategoriesValidator =
        new SearchPositionCategoriesQueryValidator();

    public static TheoryData<string?, bool> SearchCases() => new()
    {
        { null, true },
        { "", true },
        { "   ", true },
        { "a", false },
        { " a ", false }, // trims to 1 char
        { "ab", true },
        { "abc", true },
        { new string('a', PositionDescriptionCatalogValidationRules.MaxSearchLength), true },
        { new string('a', PositionDescriptionCatalogValidationRules.MaxSearchLength + 1), false },
    };

    [Theory]
    [MemberData(nameof(SearchCases))]
    public async Task ItemsValidator_ShouldEnforceSearchLength(string? search, bool expectedValid)
    {
        var query = new SearchPositionDescriptionCatalogItemsQuery(
            CompanyId: Guid.NewGuid(),
            CatalogType: PositionDescriptionCatalogType.Frequency,
            IsActive: null,
            Search: search);

        var result = await ItemsValidator.ValidateAsync(query);

        AssertSearchValidity(result, expectedValid);
    }

    [Theory]
    [MemberData(nameof(SearchCases))]
    public async Task ClassificationsValidator_ShouldEnforceSearchLength(string? search, bool expectedValid)
    {
        var query = new SearchPositionCategoryClassificationsQuery(
            CompanyId: Guid.NewGuid(),
            PositionFunctionTypeId: null,
            PositionContractTypeId: null,
            OrgUnitTypeId: null,
            IsActive: null,
            Search: search);

        var result = await ClassificationsValidator.ValidateAsync(query);

        AssertSearchValidity(result, expectedValid);
    }

    [Theory]
    [MemberData(nameof(SearchCases))]
    public async Task CategoriesValidator_ShouldEnforceSearchLength(string? search, bool expectedValid)
    {
        var query = new SearchPositionCategoriesQuery(
            CompanyId: Guid.NewGuid(),
            ClassificationId: null,
            IsActive: null,
            Search: search);

        var result = await CategoriesValidator.ValidateAsync(query);

        AssertSearchValidity(result, expectedValid);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("a", false)]
    [InlineData(" a ", false)]
    [InlineData("ab", true)]
    public void IsValidSearchLength_ShouldMatchGuardrail(string? search, bool expected) =>
        Assert.Equal(expected, PositionDescriptionCatalogValidationRules.IsValidSearchLength(search));

    private static void AssertSearchValidity(FluentValidation.Results.ValidationResult result, bool expectedValid)
    {
        var searchErrors = result.Errors
            .Where(error => error.PropertyName == nameof(SearchPositionCategoriesQuery.Search))
            .ToArray();

        if (expectedValid)
        {
            Assert.Empty(searchErrors);
        }
        else
        {
            Assert.NotEmpty(searchErrors);
        }
    }
}
