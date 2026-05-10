using CLARIHR.Application.Features.JobProfiles;

namespace CLARIHR.Application.UnitTests;

public sealed class JobProfilesMappersTests
{
    [Fact]
    public void MapCompensation_WhenRequestUsesPublicSalaryClassId_ShouldMapToApplicationInput()
    {
        // Arrange
        var salaryTabulatorLineId = Guid.NewGuid();
        var salaryClassPublicId = Guid.NewGuid();
        var request = new JobProfileCompensationRequest
        {
            SalaryTabulatorLineId = salaryTabulatorLineId,
            SalaryClassPublicId = salaryClassPublicId,
            SalaryClassCode = "S1",
            CurrencyCode = "USD",
            MinSalary = 90000m,
            MaxSalary = 110000m,
            WorkSchedule = "Full time",
            IsPrimary = true
        };

        // Act
        var result = JobProfilesMappers.MapCompensation(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(salaryTabulatorLineId, result.SalaryTabulatorLineId);
        Assert.Equal(salaryClassPublicId, result.SalaryClassId);
        Assert.Equal("S1", result.SalaryClassCode);
        Assert.Equal("USD", result.CurrencyCode);
        Assert.Equal(90000m, result.MinAmount);
        Assert.Equal(110000m, result.MaxAmount);
    }

    [Fact]
    public void MapCompensation_WhenOnlyLegacySalaryClassIdIsProvided_ShouldUseAlias()
    {
        // Arrange
        var salaryClassId = Guid.NewGuid();
        var request = new JobProfileCompensationRequest
        {
            SalaryClassId = salaryClassId
        };

        // Act
        var result = JobProfilesMappers.MapCompensation(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(salaryClassId, result.SalaryClassId);
    }
}
