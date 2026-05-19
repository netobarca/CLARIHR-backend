using CLARIHR.Application.Features.PositionSlots;
using CLARIHR.Application.Features.PositionSlots.Common;
using FluentValidation;

namespace CLARIHR.Application.UnitTests;

// §PS2 guardrail: free-text `q` on Position Slot search + export must reject
// sub-minimum-length terms (non-sargable LIKE '%x%' over 7 Normalized* columns
// on a 6-table join). Mirrors the PDC §P2 precedent — see
// project-foundation.md §12.8 / ADR-0002.
public sealed class PositionSlotSearchValidatorTests
{
    private static readonly IValidator<SearchPositionSlotsQuery> SearchValidator =
        new SearchPositionSlotsQueryValidator();

    private static readonly IValidator<GetPositionSlotExportRowsQuery> ExportValidator =
        new GetPositionSlotExportRowsQueryValidator();

    public static TheoryData<string?, bool> SearchCases() => new()
    {
        { null, true },
        { "", true },
        { "   ", true },
        { "a", false },
        { " a ", false }, // trims to 1 char
        { "ab", true },
        { "abc", true },
        { new string('a', PositionSlotValidationRules.MaxSearchLength), true },
        { new string('a', PositionSlotValidationRules.MaxSearchLength + 1), false },
    };

    [Theory]
    [MemberData(nameof(SearchCases))]
    public async Task SearchValidator_ShouldEnforceSearchLength(string? search, bool expectedValid)
    {
        var query = new SearchPositionSlotsQuery(
            CompanyId: Guid.NewGuid(),
            Status: null,
            JobProfileId: null,
            OrgUnitId: null,
            WorkCenterId: null,
            ContractTypeId: null,
            Search: search);

        var result = await SearchValidator.ValidateAsync(query);

        AssertSearchValidity(result, expectedValid);
    }

    [Theory]
    [MemberData(nameof(SearchCases))]
    public async Task ExportValidator_ShouldEnforceSearchLength(string? search, bool expectedValid)
    {
        var query = new GetPositionSlotExportRowsQuery(
            CompanyId: Guid.NewGuid(),
            Status: null,
            JobProfileId: null,
            OrgUnitId: null,
            WorkCenterId: null,
            ContractTypeId: null,
            Search: search);

        var result = await ExportValidator.ValidateAsync(query);

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
        Assert.Equal(expected, PositionSlotValidationRules.IsValidSearchLength(search));

    [Fact]
    public void PositionSlot_MinSearchLength_ShouldMatchPdcPrecedent() =>
        Assert.Equal(
            CLARIHR.Application.Features.PositionDescriptionCatalogs.Common.PositionDescriptionCatalogValidationRules.MinSearchLength,
            PositionSlotValidationRules.MinSearchLength);

    private static void AssertSearchValidity(FluentValidation.Results.ValidationResult result, bool expectedValid)
    {
        var searchErrors = result.Errors
            .Where(error => error.PropertyName == nameof(SearchPositionSlotsQuery.Search))
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
