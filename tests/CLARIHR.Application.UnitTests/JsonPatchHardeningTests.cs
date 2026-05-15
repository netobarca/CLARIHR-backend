using System.Reflection;
using System.Text.Json;
using CLARIHR.Api.Controllers;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.PositionDescriptionCatalogs;
using CLARIHR.Domain.JobProfiles;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Application.UnitTests;

public sealed class JsonPatchHardeningTests
{
    [Fact]
    public void PatchValidators_ShouldRejectDocumentsExceedingConfiguredOperationLimit()
    {
        var jobCatalogValidator = new PatchJobCatalogItemCommandValidator();
        var jobProfileValidator = new PatchJobProfileCommandValidator();
        var positionCategoryValidator = new PatchPositionCategoryCommandValidator();

        var jobCatalogResult = jobCatalogValidator.Validate(new PatchJobCatalogItemCommand(
            CompanyId: Guid.NewGuid(),
            Category: JobCatalogCategory.EducationLevel,
            ItemId: Guid.NewGuid(),
            ConcurrencyToken: Guid.NewGuid(),
            Operations: CreateJobCatalogOperations(JsonPatchHardening.MaxOperationsPerDocument + 1)));

        var jobProfileResult = jobProfileValidator.Validate(new PatchJobProfileCommand(
            JobProfileId: Guid.NewGuid(),
            ConcurrencyToken: Guid.NewGuid(),
            Operations: CreateJobProfileOperations(JsonPatchHardening.MaxOperationsPerDocument + 1)));

        var positionCategoryResult = positionCategoryValidator.Validate(new PatchPositionCategoryCommand(
            CategoryId: Guid.NewGuid(),
            Operations: CreatePositionCategoryOperations(JsonPatchHardening.MaxOperationsPerDocument + 1)));

        Assert.Contains(jobCatalogResult.Errors, static error => error.ErrorMessage == JsonPatchHardening.MaxOperationsMessage);
        Assert.Contains(jobProfileResult.Errors, static error => error.ErrorMessage == JsonPatchHardening.MaxOperationsMessage);
        Assert.Contains(positionCategoryResult.Errors, static error => error.ErrorMessage == JsonPatchHardening.MaxOperationsMessage);
    }

    [Fact]
    public void JsonPatchControllerActions_ShouldDeclareRequestBodySizeLimit()
    {
        var actions = typeof(JobProfilesController).Assembly
            .GetTypes()
            .Where(static type => type is { IsAbstract: false, IsClass: true } && type.Namespace == "CLARIHR.Api.Controllers")
            .SelectMany(static type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(static method => method.GetParameters().Any(static parameter =>
                parameter.ParameterType.IsGenericType &&
                parameter.ParameterType.GetGenericTypeDefinition() == typeof(JsonPatchDocument<>)))
            .OrderBy(static method => $"{method.DeclaringType!.FullName}.{method.Name}")
            .ToArray();

        Assert.NotEmpty(actions);

        foreach (var action in actions)
        {
            var attribute = action.GetCustomAttribute<RequestSizeLimitAttribute>();
            Assert.NotNull(attribute);
            Assert.Equal(JsonPatchHardening.MaxRequestBodySizeBytes, attribute!.Bytes);
        }
    }

    private static IReadOnlyCollection<JobCatalogItemPatchOperation> CreateJobCatalogOperations(int count)
        => Enumerable.Range(0, count)
            .Select(index => new JobCatalogItemPatchOperation(
                "replace",
                $"/name{index}",
                From: null,
                JsonSerializer.SerializeToElement($"value-{index}")))
            .ToArray();

    private static IReadOnlyCollection<JobProfilePatchOperation> CreateJobProfileOperations(int count)
        => Enumerable.Range(0, count)
            .Select(index => new JobProfilePatchOperation(
                "replace",
                "/title",
                From: null,
                JsonSerializer.SerializeToElement($"value-{index}")))
            .ToArray();

    private static IReadOnlyCollection<PositionDescriptionCatalogPatchOperation> CreatePositionCategoryOperations(int count)
    {
        var operations = new List<PositionDescriptionCatalogPatchOperation>(count)
        {
            new(
                "replace",
                "/concurrencyToken",
                From: null,
                JsonSerializer.SerializeToElement(Guid.NewGuid().ToString("D")))
        };

        operations.AddRange(Enumerable.Range(1, count - 1)
            .Select(index => new PositionDescriptionCatalogPatchOperation(
                "replace",
                "/name",
                From: null,
                JsonSerializer.SerializeToElement($"value-{index}"))));

        return operations;
    }
}
