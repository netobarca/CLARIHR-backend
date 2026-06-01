using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using FluentValidation;

namespace CLARIHR.Application.UnitTests;

// Audit finding (defense-in-depth, mirrors §J2/§N5 + §PF1): the two employee sub-resource
// paginated searches — personnel-actions and payroll-transactions — previously shipped with
// NO query validator, so pageSize/pageNumber were unbounded at every layer (pageSize=10_000_000
// materializes the whole tenant slice; pageNumber<=0 → negative SQL OFFSET → 500), silently
// bypassing the MaxPageSize=100 invariant every other paginated endpoint enforces. These tests
// lock in the bounds (PageSize in [1, MaxPageSize], PageNumber > 0) and the §PF1 q-length guard
// the new SearchPersonnelFile{PersonnelActions,PayrollTransactions}QueryValidator now enforce,
// matching the canonical SearchPersonnelFilesQueryValidator.
public sealed class PersonnelFileEmployeeSearchValidatorTests
{
    private static readonly IValidator<SearchPersonnelFilePersonnelActionsQuery> PersonnelActionsValidator =
        new SearchPersonnelFilePersonnelActionsQueryValidator();

    private static readonly IValidator<SearchPersonnelFilePayrollTransactionsQuery> PayrollTransactionsValidator =
        new SearchPersonnelFilePayrollTransactionsQueryValidator();

    public static TheoryData<int, int, bool> PaginationCases() => new()
    {
        { 1, 1, true },
        { PersonnelFileValidationRules.DefaultPageSize, 1, true },
        { PersonnelFileValidationRules.MaxPageSize, 1, true },
        { 0, 1, false },                                            // pageSize below minimum
        { -1, 1, false },                                           // pageSize negative
        { PersonnelFileValidationRules.MaxPageSize + 1, 1, false }, // over max (the original bug)
        { 10_000_000, 1, false },                                   // extreme over max (DoS vector)
        { 20, 0, false },                                           // pageNumber zero
        { 20, -5, false },                                          // pageNumber negative (negative OFFSET)
    };

    [Theory]
    [MemberData(nameof(PaginationCases))]
    public async Task PersonnelActionsValidator_ShouldEnforcePaginationBounds(int pageSize, int pageNumber, bool expectedValid)
    {
        var query = new SearchPersonnelFilePersonnelActionsQuery(
            PersonnelFileId: Guid.NewGuid(),
            FromUtc: null,
            ToUtc: null,
            Type: null,
            Status: null,
            Search: null,
            PageNumber: pageNumber,
            PageSize: pageSize);

        var result = await PersonnelActionsValidator.ValidateAsync(query);

        AssertPaginationValidity(result, expectedValid);
    }

    [Theory]
    [MemberData(nameof(PaginationCases))]
    public async Task PayrollTransactionsValidator_ShouldEnforcePaginationBounds(int pageSize, int pageNumber, bool expectedValid)
    {
        var query = new SearchPersonnelFilePayrollTransactionsQuery(
            PersonnelFileId: Guid.NewGuid(),
            FromUtc: null,
            ToUtc: null,
            Type: null,
            Status: null,
            Search: null,
            PageNumber: pageNumber,
            PageSize: pageSize);

        var result = await PayrollTransactionsValidator.ValidateAsync(query);

        AssertPaginationValidity(result, expectedValid);
    }

    public static TheoryData<string?, bool> SearchCases() => new()
    {
        { null, true },
        { "", true },
        { "   ", true },
        { "a", false },  // trims to 1 char, below MinSearchLength
        { "ab", true },
        { new string('a', PersonnelFileValidationRules.MaxSearchLength), true },
        { new string('a', PersonnelFileValidationRules.MaxSearchLength + 1), false },
    };

    [Theory]
    [MemberData(nameof(SearchCases))]
    public async Task PersonnelActionsValidator_ShouldEnforceSearchLength(string? search, bool expectedValid)
    {
        var query = new SearchPersonnelFilePersonnelActionsQuery(
            PersonnelFileId: Guid.NewGuid(),
            FromUtc: null,
            ToUtc: null,
            Type: null,
            Status: null,
            Search: search);

        var result = await PersonnelActionsValidator.ValidateAsync(query);

        AssertSearchValidity(result, expectedValid);
    }

    [Theory]
    [MemberData(nameof(SearchCases))]
    public async Task PayrollTransactionsValidator_ShouldEnforceSearchLength(string? search, bool expectedValid)
    {
        var query = new SearchPersonnelFilePayrollTransactionsQuery(
            PersonnelFileId: Guid.NewGuid(),
            FromUtc: null,
            ToUtc: null,
            Type: null,
            Status: null,
            Search: search);

        var result = await PayrollTransactionsValidator.ValidateAsync(query);

        AssertSearchValidity(result, expectedValid);
    }

    [Fact]
    public async Task PersonnelActionsValidator_ShouldRejectEmptyPersonnelFileId()
    {
        var query = new SearchPersonnelFilePersonnelActionsQuery(
            PersonnelFileId: Guid.Empty,
            FromUtc: null,
            ToUtc: null,
            Type: null,
            Status: null,
            Search: null);

        var result = await PersonnelActionsValidator.ValidateAsync(query);

        Assert.Contains(result.Errors, error => error.PropertyName == "PersonnelFileId");
    }

    private static void AssertPaginationValidity(FluentValidation.Results.ValidationResult result, bool expectedValid)
    {
        var paginationErrors = result.Errors
            .Where(error => error.PropertyName is "PageSize" or "PageNumber")
            .ToArray();

        if (expectedValid)
        {
            Assert.Empty(paginationErrors);
        }
        else
        {
            Assert.NotEmpty(paginationErrors);
        }
    }

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
