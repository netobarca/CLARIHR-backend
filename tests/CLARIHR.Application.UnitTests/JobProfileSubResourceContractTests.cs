using System.Text.Json;
using CLARIHR.Api.Controllers;
using CLARIHR.Application.Features.JobProfiles;

namespace CLARIHR.Application.UnitTests;

public sealed class JobProfileSubResourceContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEnumerable<object[]> SubResourceResponses()
    {
        var publicId = Guid.Parse("eafae5a0-130e-437f-a4c7-bf78d2bf1101");
        var relatedPublicId = Guid.Parse("d47e6960-b4fd-4a92-a003-71ecae24125a");
        var concurrencyToken = Guid.Parse("2754c741-d5d7-4cd5-9b3c-176e6e268107");

        yield return
        [
            new JobProfileTrainingResponse(publicId, relatedPublicId, "Training", "Notes", 1, concurrencyToken),
            new[] { "trainingPublicId", "catalogItemPublicId" },
            new[] { "id", "catalogItemId" }
        ];

        yield return
        [
            new JobProfileBenefitResponse(publicId, relatedPublicId, "Benefit", "Notes", 1, concurrencyToken),
            new[] { "benefitPublicId", "catalogItemPublicId" },
            new[] { "id", "catalogItemId" }
        ];

        yield return
        [
            new JobProfileWorkingConditionResponse(publicId, relatedPublicId, relatedPublicId, "Condition", "Notes", 1, concurrencyToken),
            new[] { "workingConditionPublicId", "catalogItemPublicId", "workConditionTypeCatalogItemPublicId" },
            new[] { "id", "catalogItemId", "workConditionTypeCatalogItemId" }
        ];

        yield return
        [
            new JobProfileDependentPositionResponse(publicId, relatedPublicId, "DEV-LEAD", "Tech Lead", 2, "Notes", concurrencyToken),
            new[] { "dependentPositionPublicId", "dependentJobProfilePublicId" },
            new[] { "id", "dependentJobProfileId" }
        ];

        yield return
        [
            new JobProfileCompensationItemResponse(
                publicId,
                relatedPublicId,
                "CLS-1",
                "SCL-1",
                "USD",
                1_000m,
                900m,
                1_100m,
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                null,
                "Notes",
                concurrencyToken,
                new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                null),
            new[] { "compensationPublicId", "salaryTabulatorLinePublicId" },
            new[] { "id", "salaryTabulatorLineId" }
        ];
    }

    public static IEnumerable<object[]> SubResourceRequests()
    {
        var publicId = Guid.Parse("eafae5a0-130e-437f-a4c7-bf78d2bf1101");
        var relatedPublicId = Guid.Parse("d47e6960-b4fd-4a92-a003-71ecae24125a");
        var concurrencyToken = Guid.Parse("2754c741-d5d7-4cd5-9b3c-176e6e268107");

        yield return
        [
            new JobProfileTrainingsController.AddTrainingRequest
            {
                CatalogItemPublicId = relatedPublicId,
                Name = "Training",
                Notes = "Notes",
                SortOrder = 1
            },
            new[] { "catalogItemPublicId" },
            new[] { "catalogItemId" }
        ];

        yield return
        [
            new JobProfileTrainingsController.UpdateTrainingRequest
            {
                CatalogItemPublicId = relatedPublicId,
                Name = "Training",
                Notes = "Notes",
                SortOrder = 1,
                ConcurrencyToken = concurrencyToken
            },
            new[] { "catalogItemPublicId" },
            new[] { "catalogItemId" }
        ];

        yield return
        [
            new JobProfileBenefitsController.AddBenefitRequest
            {
                CatalogItemPublicId = relatedPublicId,
                Name = "Benefit",
                Notes = "Notes",
                SortOrder = 1
            },
            new[] { "catalogItemPublicId" },
            new[] { "catalogItemId" }
        ];

        yield return
        [
            new JobProfileBenefitsController.UpdateBenefitRequest
            {
                CatalogItemPublicId = relatedPublicId,
                Name = "Benefit",
                Notes = "Notes",
                SortOrder = 1,
                ConcurrencyToken = concurrencyToken
            },
            new[] { "catalogItemPublicId" },
            new[] { "catalogItemId" }
        ];

        yield return
        [
            new JobProfileWorkingConditionsController.AddWorkingConditionRequest
            {
                WorkConditionTypeCatalogItemPublicId = relatedPublicId,
                CatalogItemPublicId = publicId,
                Name = "Condition",
                Notes = "Notes",
                SortOrder = 1
            },
            new[] { "workConditionTypeCatalogItemPublicId", "catalogItemPublicId" },
            new[] { "workConditionTypeCatalogItemId", "catalogItemId" }
        ];

        yield return
        [
            new JobProfileWorkingConditionsController.UpdateWorkingConditionRequest
            {
                WorkConditionTypeCatalogItemPublicId = relatedPublicId,
                CatalogItemPublicId = publicId,
                Name = "Condition",
                Notes = "Notes",
                SortOrder = 1,
                ConcurrencyToken = concurrencyToken
            },
            new[] { "workConditionTypeCatalogItemPublicId", "catalogItemPublicId" },
            new[] { "workConditionTypeCatalogItemId", "catalogItemId" }
        ];

        yield return
        [
            new JobProfileDependentPositionsController.AddDependentPositionRequest
            {
                DependentJobProfilePublicId = relatedPublicId,
                Quantity = 1,
                Notes = "Notes"
            },
            new[] { "dependentJobProfilePublicId" },
            new[] { "dependentJobProfileId" }
        ];

        yield return
        [
            new JobProfileDependentPositionsController.UpdateDependentPositionRequest
            {
                DependentJobProfilePublicId = relatedPublicId,
                Quantity = 1,
                Notes = "Notes",
                ConcurrencyToken = concurrencyToken
            },
            new[] { "dependentJobProfilePublicId" },
            new[] { "dependentJobProfileId" }
        ];

        yield return
        [
            new JobProfileCompensationsController.AddCompensationRequest
            {
                SalaryTabulatorLinePublicId = relatedPublicId,
                Notes = "Notes"
            },
            new[] { "salaryTabulatorLinePublicId" },
            new[] { "salaryTabulatorLineId" }
        ];

        yield return
        [
            new JobProfileCompensationsController.UpdateCompensationRequest
            {
                SalaryTabulatorLinePublicId = relatedPublicId,
                Notes = "Notes",
                ConcurrencyToken = concurrencyToken
            },
            new[] { "salaryTabulatorLinePublicId" },
            new[] { "salaryTabulatorLineId" }
        ];
    }

    [Theory]
    [MemberData(nameof(SubResourceResponses))]
    public void JobProfileSubResourceResponses_ShouldExposePublicIdContract(
        object response,
        string[] expectedProperties,
        string[] legacyProperties)
    {
        AssertPublicIdContract(response, expectedProperties, legacyProperties);
    }

    [Theory]
    [MemberData(nameof(SubResourceRequests))]
    public void JobProfileSubResourceRequests_ShouldExposePublicIdContract(
        object request,
        string[] expectedProperties,
        string[] legacyProperties)
    {
        AssertPublicIdContract(request, expectedProperties, legacyProperties);
    }

    private static void AssertPublicIdContract(
        object value,
        string[] expectedProperties,
        string[] legacyProperties)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value, JsonOptions));
        var root = document.RootElement;

        foreach (var property in expectedProperties)
        {
            Assert.True(root.TryGetProperty(property, out _), $"Expected JSON property '{property}'.");
        }

        foreach (var property in legacyProperties)
        {
            Assert.False(root.TryGetProperty(property, out _), $"Legacy JSON property '{property}' must not be exposed.");
        }
    }
}
