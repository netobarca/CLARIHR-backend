using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PositionSlots.Common;
using FluentValidation;

namespace CLARIHR.Application.UnitTests;

// §PF1 guardrail: free-text `q` on the 4 Personnel Files search surfaces (Search / Export /
// DynamicQuery / Analytics) must reject sub-minimum-length terms — the repository
// (PersonnelFileRepository.ApplySearch) fans a non-sargable LIKE '%x%' over NormalizedFullName
// (+ NormalizedIdentificationNumber). Mirrors the PositionSlots §PS2 / PDC §P2 precedent —
// see project-foundation.md §12.8 / ADR-0002.
public sealed class PersonnelFileSearchValidatorTests
{
    private static readonly IValidator<SearchPersonnelFilesQuery> SearchValidator =
        new SearchPersonnelFilesQueryValidator();

    private static readonly IValidator<ExportPersonnelFilesQuery> ExportValidator =
        new ExportPersonnelFilesQueryValidator();

    private static readonly IValidator<DynamicQueryPersonnelFilesQuery> DynamicValidator =
        new DynamicQueryPersonnelFilesQueryValidator();

    private static readonly IValidator<GetPersonnelFilesAnalyticsSummaryQuery> AnalyticsValidator =
        new GetPersonnelFilesAnalyticsSummaryQueryValidator();

    public static TheoryData<string?, bool> SearchCases() => new()
    {
        { null, true },
        { "", true },
        { "   ", true },
        { "a", false },
        { " a ", false }, // trims to 1 char
        { "ab", true },
        { "abc", true },
        { new string('a', PersonnelFileValidationRules.MaxSearchLength), true },
        { new string('a', PersonnelFileValidationRules.MaxSearchLength + 1), false },
    };

    [Theory]
    [MemberData(nameof(SearchCases))]
    public async Task SearchValidator_ShouldEnforceSearchLength(string? search, bool expectedValid)
    {
        var query = new SearchPersonnelFilesQuery(
            CompanyId: Guid.NewGuid(),
            IsActive: null,
            RecordType: null,
            OrgUnitId: null,
            MinAge: null,
            MaxAge: null,
            MaritalStatus: null,
            Nationality: null,
            Profession: null,
            CreatedFromUtc: null,
            CreatedToUtc: null,
            Search: search);

        var result = await SearchValidator.ValidateAsync(query);

        AssertSearchValidity(result, expectedValid);
    }

    [Theory]
    [MemberData(nameof(SearchCases))]
    public async Task ExportValidator_ShouldEnforceSearchLength(string? search, bool expectedValid)
    {
        var query = new ExportPersonnelFilesQuery(
            CompanyId: Guid.NewGuid(),
            IsActive: null,
            RecordType: null,
            OrgUnitId: null,
            MinAge: null,
            MaxAge: null,
            MaritalStatus: null,
            Nationality: null,
            Profession: null,
            CreatedFromUtc: null,
            CreatedToUtc: null,
            Search: search);

        var result = await ExportValidator.ValidateAsync(query);

        AssertSearchValidity(result, expectedValid);
    }

    [Theory]
    [MemberData(nameof(SearchCases))]
    public async Task DynamicQueryValidator_ShouldEnforceSearchLength(string? search, bool expectedValid)
    {
        var query = new DynamicQueryPersonnelFilesQuery(
            CompanyId: Guid.NewGuid(),
            Filters: [],
            GroupBy: [],
            Sort: [],
            Search: search);

        var result = await DynamicValidator.ValidateAsync(query);

        AssertSearchValidity(result, expectedValid);
    }

    [Theory]
    [MemberData(nameof(SearchCases))]
    public async Task AnalyticsValidator_ShouldEnforceSearchLength(string? search, bool expectedValid)
    {
        var query = new GetPersonnelFilesAnalyticsSummaryQuery(
            CompanyId: Guid.NewGuid(),
            IsActive: null,
            RecordType: null,
            OrgUnitId: null,
            MinAge: null,
            MaxAge: null,
            Search: search);

        var result = await AnalyticsValidator.ValidateAsync(query);

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
        Assert.Equal(expected, PersonnelFileValidationRules.IsValidSearchLength(search));

    [Fact]
    public void PersonnelFile_MinSearchLength_ShouldMatchPositionSlotPrecedent() =>
        Assert.Equal(
            PositionSlotValidationRules.MinSearchLength,
            PersonnelFileValidationRules.MinSearchLength);

    private static void AssertSearchValidity(FluentValidation.Results.ValidationResult result, bool expectedValid)
    {
        var searchErrors = result.Errors
            .Where(error => error.PropertyName == "Search")
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
