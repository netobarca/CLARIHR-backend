using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using CLARIHR.Api.Controllers;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.PositionDescriptionCatalogs;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using CLARIHR.Domain.JobProfiles;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
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
            ConcurrencyToken: Guid.NewGuid(),
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
            var metadata = Assert.IsAssignableFrom<IRequestSizeLimitMetadata>(attribute);
            Assert.Equal(JsonPatchHardening.MaxRequestBodySizeBytes, metadata.MaxRequestBodySize);
        }
    }

    /// <summary>
    /// Defense-in-depth guard for technical-debt §4.2: the paginated list endpoints of the
    /// PositionDescriptionCatalog controllers must constrain <c>pageSize</c> at the controller
    /// boundary via <c>[Range(1, MaxPageSize)]</c>, not rely solely on the handler validator.
    /// Fails the build if the annotation is removed (the exact regression the finding warns about).
    /// </summary>
    [Fact]
    public void PositionCatalogListEndpoints_ShouldDeclarePageSizeRange()
    {
        Type[] controllers =
        [
            typeof(PositionCategoriesController),
            typeof(PositionCategoryClassificationsController),
            typeof(PositionDescriptionCatalogItemsController),
        ];

        foreach (var controller in controllers)
        {
            var listAction = controller.GetMethod("Get", BindingFlags.Instance | BindingFlags.Public);
            Assert.True(listAction is not null, $"{controller.Name} must expose a list 'Get' action.");

            var pageSize = listAction!.GetParameters().SingleOrDefault(parameter => parameter.Name == "pageSize");
            Assert.True(pageSize is not null, $"{controller.Name}.Get must have a 'pageSize' parameter.");

            var range = pageSize!.GetCustomAttribute<RangeAttribute>();
            Assert.True(
                range is not null,
                $"{controller.Name}.Get 'pageSize' must declare [Range] (defense-in-depth, debt §4.2).");
            Assert.Equal(1, Convert.ToInt32(range!.Minimum));
            Assert.Equal(PositionDescriptionCatalogValidationRules.MaxPageSize, Convert.ToInt32(range.Maximum));
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
        => Enumerable.Range(0, count)
            .Select(index => new PositionDescriptionCatalogPatchOperation(
                "replace",
                "/name",
                From: null,
                JsonSerializer.SerializeToElement($"value-{index}")))
            .ToArray();
}
