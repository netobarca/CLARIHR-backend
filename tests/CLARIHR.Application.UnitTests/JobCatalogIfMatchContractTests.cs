using System.Text.Json;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Domain.JobProfiles;

namespace CLARIHR.Application.UnitTests;

public sealed class JobCatalogIfMatchContractTests
{
    [Fact]
    public void PatchValidator_WhenHeaderTokenIsProvided_ShouldNotRequireConcurrencyTokenPatchOperation()
    {
        var validator = new PatchJobCatalogItemCommandValidator();
        var command = new PatchJobCatalogItemCommand(
            CompanyId: Guid.NewGuid(),
            Category: JobCatalogCategory.EducationLevel,
            ItemId: Guid.NewGuid(),
            ConcurrencyToken: Guid.NewGuid(),
            Operations:
            [
                new JobCatalogItemPatchOperation(
                    "replace",
                    "/name",
                    From: null,
                    JsonSerializer.SerializeToElement("Licenciatura"))
            ]);

        var result = validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void PatchValidator_WhenHeaderTokenIsMissing_ShouldFail()
    {
        var validator = new PatchJobCatalogItemCommandValidator();
        var command = new PatchJobCatalogItemCommand(
            CompanyId: Guid.NewGuid(),
            Category: JobCatalogCategory.EducationLevel,
            ItemId: Guid.NewGuid(),
            ConcurrencyToken: Guid.Empty,
            Operations:
            [
                new JobCatalogItemPatchOperation(
                    "replace",
                    "/name",
                    From: null,
                    JsonSerializer.SerializeToElement("Licenciatura"))
            ]);

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, static error => error.PropertyName == nameof(PatchJobCatalogItemCommand.ConcurrencyToken));
    }

    [Fact]
    public void PatchApplier_WhenConcurrencyTokenPathIsSent_ShouldRejectItAsUnsupportedContractField()
    {
        var state = JobCatalogItemPatchState.From(new JobCatalogItemResponse(
            Id: Guid.NewGuid(),
            Category: JobCatalogCategory.EducationLevel,
            Code: "EDU-001",
            Name: "Licenciatura",
            IsSystem: false,
            IsActive: true,
            ConcurrencyToken: Guid.NewGuid(),
            CreatedAtUtc: DateTime.UtcNow,
            ModifiedAtUtc: null));

        var result = JobCatalogItemPatchApplier.Apply(
            [
                new JobCatalogItemPatchOperation(
                    "replace",
                    "/concurrencyToken",
                    From: null,
                    JsonSerializer.SerializeToElement(Guid.NewGuid().ToString("D")))
            ],
            state);

        Assert.True(result.IsFailure);
        Assert.Equal("common.validation", result.Error.Code);
    }
}
