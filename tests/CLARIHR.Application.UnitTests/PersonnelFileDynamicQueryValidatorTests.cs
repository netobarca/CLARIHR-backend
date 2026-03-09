using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

public sealed class PersonnelFileDynamicQueryValidatorTests
{
    private readonly DynamicQueryPersonnelFilesQueryValidator _dynamicValidator = new();
    private readonly SearchPersonnelFilesQueryValidator _searchValidator = new();
    private readonly ExportPersonnelFilesQueryValidator _exportValidator = new();

    [Fact]
    public void DynamicQuery_WithSupportedFields_ShouldBeValid()
    {
        var query = new DynamicQueryPersonnelFilesQuery(
            Guid.NewGuid(),
            [
                new PersonnelFileDynamicFilterInput("maritalStatus", "eq", "SINGLE", null, null),
                new PersonnelFileDynamicFilterInput("age", "between", "20", "40", null)
            ],
            ["maritalStatus", "nationality"],
            [new PersonnelFileDynamicSortInput("fullName", PersonnelFileSortDirection.Asc)],
            Search: "maria",
            PageNumber: 1,
            PageSize: 20,
            IncludeAllowedActions: false);

        var validation = _dynamicValidator.Validate(query);

        Assert.True(validation.IsValid);
    }

    [Fact]
    public void DynamicQuery_WithUnsupportedGroupField_ShouldFail()
    {
        var query = new DynamicQueryPersonnelFilesQuery(
            Guid.NewGuid(),
            [],
            ["contractType"],
            [],
            Search: null,
            PageNumber: 1,
            PageSize: 20,
            IncludeAllowedActions: false);

        var validation = _dynamicValidator.Validate(query);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.PropertyName.Contains("GroupBy", StringComparison.Ordinal));
    }

    [Fact]
    public void DynamicQuery_WithUnsupportedFilterOperator_ShouldFail()
    {
        var query = new DynamicQueryPersonnelFilesQuery(
            Guid.NewGuid(),
            [new PersonnelFileDynamicFilterInput("maritalStatus", "startsWith", "S", null, null)],
            [],
            [],
            Search: null,
            PageNumber: 1,
            PageSize: 20,
            IncludeAllowedActions: false);

        var validation = _dynamicValidator.Validate(query);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.PropertyName.Contains("Filters", StringComparison.Ordinal));
    }

    [Fact]
    public void Search_WithUnsupportedSortBy_ShouldFail()
    {
        var query = new SearchPersonnelFilesQuery(
            Guid.NewGuid(),
            IsActive: null,
            RecordType: PersonnelFileRecordType.Candidate,
            OrgUnitId: null,
            MinAge: null,
            MaxAge: null,
            MaritalStatus: null,
            Nationality: null,
            Profession: null,
            CreatedFromUtc: null,
            CreatedToUtc: null,
            Search: null,
            SortBy: "salaryRange",
            SortDirection: PersonnelFileSortDirection.Asc,
            PageNumber: 1,
            PageSize: 20,
            IncludeAllowedActions: false);

        var validation = _searchValidator.Validate(query);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.PropertyName == "SortBy");
    }

    [Fact]
    public void Export_WithUnsupportedSortBy_ShouldFail()
    {
        var query = new ExportPersonnelFilesQuery(
            Guid.NewGuid(),
            IsActive: null,
            RecordType: PersonnelFileRecordType.Candidate,
            OrgUnitId: null,
            MinAge: null,
            MaxAge: null,
            MaritalStatus: null,
            Nationality: null,
            Profession: null,
            CreatedFromUtc: null,
            CreatedToUtc: null,
            Search: null,
            SortBy: "salaryRange",
            SortDirection: PersonnelFileSortDirection.Desc);

        var validation = _exportValidator.Validate(query);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.PropertyName == "SortBy");
    }
}
