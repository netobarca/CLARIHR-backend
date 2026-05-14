using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CLARIHR.Application.Features.CompetencyFramework.Common;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Application.Features.OrgUnits.Common;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using CLARIHR.Domain.InternalCatalogs;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

public sealed class InternalCatalogsIntegrationTests(IntegrationTestWebApplicationFactory factory)
    : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private const string RequirementCatalogContext = "job-profile.requirements";
    private const string CertificationCatalogKey = "job-profile.requirements.certification";
    private const string KnowledgeCatalogKey = "job-profile.requirements.knowledge";
    private static readonly Guid SeedActorUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly JsonSerializerOptions JsonOptions = IntegrationTestJson.CreateOptions();

    [Fact]
    public async Task InternalCatalogDefinitions_WithAuthenticatedUserWithoutTenant_ShouldReturnManifest()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.AuthenticatedWithoutTenant(scenario.ActorUserId));

        var response = await client.GetAsync($"/api/account/internal-catalogs?context={RequirementCatalogContext}");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<InternalCatalogDefinitionItem>>(JsonOptions);
        Assert.NotNull(payload);

        var certification = Assert.Single(payload!, item => item.Identifier == "Certification");
        Assert.Equal("Search", certification.RenderType);
        Assert.Equal(CertificationCatalogKey, certification.CatalogKey);
        Assert.True(certification.AllowCreate);

        var experience = Assert.Single(payload, item => item.Identifier == "Experience");
        Assert.Equal("FreeText", experience.RenderType);
        Assert.Null(experience.CatalogKey);
    }

    [Fact]
    public async Task InternalCatalogSearch_ShouldBeGlobalCrossTenantAndRestrictedByCatalogKey()
    {
        var scenario = await factory.ResetDatabaseAsync(dbContext =>
        {
            dbContext.InternalCatalogValues.AddRange(
                InternalCatalogValue.Create(CertificationCatalogKey, "Azure AI Fundamentals", SeedActorUserId),
                InternalCatalogValue.Create(KnowledgeCatalogKey, "Azure AI Fundamentals", SeedActorUserId));
            return Task.CompletedTask;
        });
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.OtherTenantId));

        var response = await client.GetAsync(
            $"/api/account/internal-catalogs/{CertificationCatalogKey}/values?q={Uri.EscapeDataString("Azure AI Fundamentals")}");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<InternalCatalogValueSuggestionItem>>(JsonOptions);
        Assert.NotNull(payload);
        var match = Assert.Single(payload!);
        Assert.Equal("Azure AI Fundamentals", match.Value);
    }

    [Fact]
    public async Task InternalCatalogCreate_WhenSimilarValueExists_ShouldReturnConflictWithSuggestions()
    {
        var scenario = await factory.ResetDatabaseAsync(dbContext =>
        {
            dbContext.InternalCatalogValues.Add(
                InternalCatalogValue.Create(CertificationCatalogKey, "Azure AI Fundamentals A", SeedActorUserId));
            return Task.CompletedTask;
        });
        using var client = factory.CreateClientFor(TestUserContext.AuthenticatedWithoutTenant(scenario.ActorUserId));

        var response = await client.PostJsonAsync(
            $"/api/account/internal-catalogs/{CertificationCatalogKey}/values",
            new { value = "Azure AI Fundamentals" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var problemDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(problemDocument.RootElement.TryGetProperty("suggestions", out var suggestions));
        Assert.Equal(JsonValueKind.Array, suggestions.ValueKind);
        Assert.NotEmpty(suggestions.EnumerateArray());
    }

    [Fact]
    public async Task PositionDescriptionCatalogs_PatchEndpoints_ShouldReturnPatchedEntity()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var frequency = await EnsurePositionDescriptionCatalogItemAsync(client, scenario.TenantId, "frequencies", "FREQ-PATCH");
        using var frequencyPatchRequest = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/position-description-catalogs/frequencies/items/{frequency.Id}")
        {
            Content = CreateJsonPatchContent(
                new { op = "replace", path = "/name", value = (object)"Frecuencia parcheada" },
                new { op = "replace", path = "/isActive", value = (object)false },
                new { op = "replace", path = "/concurrencyToken", value = (object)frequency.ConcurrencyToken.ToString() })
        };

        var frequencyPatchResponse = await client.SendAsync(frequencyPatchRequest);
        frequencyPatchResponse.EnsureSuccessStatusCode();
        var patchedFrequency = await frequencyPatchResponse.Content.ReadFromJsonAsync<PositionDescriptionCatalogItem>(JsonOptions);
        Assert.NotNull(patchedFrequency);
        Assert.Equal(frequency.Id, patchedFrequency!.Id);
        Assert.Equal("Frecuencia parcheada", patchedFrequency.Name);
        Assert.False(patchedFrequency.IsActive);
        Assert.NotEqual(frequency.ConcurrencyToken, patchedFrequency.ConcurrencyToken);

        var orgUnitType = await EnsureOrgUnitTypeAsync(client, scenario.TenantId, "ORG-PATCH");
        var functionType = await EnsurePositionDescriptionCatalogItemAsync(client, scenario.TenantId, "position-function-types", "FUNC-PATCH");
        var contractType = await EnsurePositionDescriptionCatalogItemAsync(client, scenario.TenantId, "position-contract-types", "CON-PATCH");
        var classification = await EnsurePositionCategoryClassificationAsync(
            client,
            scenario.TenantId,
            "CLASS-PATCH",
            functionType.Id,
            contractType.Id,
            orgUnitType.Id);

        using var classificationPatchRequest = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/position-category-classifications/{classification.Id}")
        {
            Content = CreateJsonPatchContent(
                new { op = "replace", path = "/name", value = (object)"Clasificacion parcheada" },
                new { op = "replace", path = "/sortOrder", value = (object)25 },
                new { op = "replace", path = "/concurrencyToken", value = (object)classification.ConcurrencyToken.ToString() })
        };

        var classificationPatchResponse = await client.SendAsync(classificationPatchRequest);
        classificationPatchResponse.EnsureSuccessStatusCode();
        var patchedClassification = await classificationPatchResponse.Content.ReadFromJsonAsync<PositionCategoryClassificationItem>(JsonOptions);
        Assert.NotNull(patchedClassification);
        Assert.Equal(classification.Id, patchedClassification!.Id);
        Assert.Equal("Clasificacion parcheada", patchedClassification.Name);
        Assert.Equal(25, patchedClassification.SortOrder);
        Assert.NotEqual(classification.ConcurrencyToken, patchedClassification.ConcurrencyToken);

        var category = await EnsurePositionCategoryAsync(client, scenario.TenantId, "CAT-PATCH", classification.Id);
        using var categoryPatchRequest = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/position-categories/{category.Id}")
        {
            Content = CreateJsonPatchContent(
                new { op = "replace", path = "/name", value = (object)"Categoria parcheada" },
                new { op = "replace", path = "/isActive", value = (object)false },
                new { op = "replace", path = "/concurrencyToken", value = (object)category.ConcurrencyToken.ToString() })
        };

        var categoryPatchResponse = await client.SendAsync(categoryPatchRequest);
        categoryPatchResponse.EnsureSuccessStatusCode();
        var patchedCategory = await categoryPatchResponse.Content.ReadFromJsonAsync<PositionCategoryItem>(JsonOptions);
        Assert.NotNull(patchedCategory);
        Assert.Equal(category.Id, patchedCategory!.Id);
        Assert.Equal("Categoria parcheada", patchedCategory.Name);
        Assert.False(patchedCategory.IsActive);
        Assert.NotEqual(category.ConcurrencyToken, patchedCategory.ConcurrencyToken);
    }

    [Fact]
    public async Task JobProfiles_CreateAndUpdate_WithSearchRequirements_ShouldPopulateGlobalInternalCatalogs()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var createdProfile = await CreateJobProfileAsync(
            client,
            scenario.TenantId,
            "JP-INT-001",
            "Perfil Catalogo",
            requirementType: "Certification",
            description: "Azure AI Fundamentals");

        await UpdateJobProfileAsync(
            client,
            createdProfile,
            scenario.TenantId,
            requirementType: "Knowledge",
            description: "Machine Learning");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var values = await dbContext.InternalCatalogValues
            .AsNoTracking()
            .OrderBy(value => value.CatalogKey)
            .ThenBy(value => value.Value)
            .Select(value => new { value.CatalogKey, value.Value, value.UsageCount })
            .ToListAsync();

        Assert.Contains(values, value => value.CatalogKey == CertificationCatalogKey && value.Value == "Azure AI Fundamentals" && value.UsageCount == 1);
        Assert.Contains(values, value => value.CatalogKey == KnowledgeCatalogKey && value.Value == "Machine Learning" && value.UsageCount == 1);
    }

    [Fact]
    public async Task JobProfiles_CreateAndUpdate_WithFreeTextRequirements_ShouldNotPopulateGlobalInternalCatalogs()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var createdProfile = await CreateJobProfileAsync(
            client,
            scenario.TenantId,
            "JP-INT-002",
            "Perfil Texto Libre",
            requirementType: "Experience",
            description: "5 years");

        await UpdateJobProfileAsync(
            client,
            createdProfile,
            scenario.TenantId,
            requirementType: "Other",
            description: "Licencia de conducir");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var count = await dbContext.InternalCatalogValues.CountAsync();

        Assert.Equal(0, count);
    }

    private static TestUserContext CreateJobProfileAdminContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            JobProfilePermissionCodes.Admin,
            PositionDescriptionCatalogPermissionCodes.Admin,
            OrgUnitPermissionCodes.Admin,
            CompetencyFrameworkPermissionCodes.Admin);

    private async Task<JobProfileItem> CreateJobProfileAsync(
        HttpClient client,
        Guid companyId,
        string code,
        string title,
        string requirementType,
        string description)
    {
        var positionCategory = await EnsureDefaultPositionCategoryAsync(client, companyId);
        var orgUnit = await EnsureDefaultOrgUnitAsync(client, companyId);

        var response = await client.PostJsonAsync($"/api/v1/companies/{companyId}/job-profiles", new
        {
            code,
            title,
            objective = "Objetivo",
            orgUnitPublicId = orgUnit.Id,
            reportsToJobProfilePublicId = (Guid?)null,
            positionCategoryPublicId = positionCategory.Id,
            strategicObjectiveCatalogItemPublicId = (Guid?)null,
            assignedWorkEquipmentCatalogItemPublicId = (Guid?)null,
            responsibilityCatalogItemPublicId = (Guid?)null,
            decisionScope = "Operacion",
            assignedResources = "Equipo",
            responsibilities = "Responsabilidades",
            benefitsSummary = "Ley",
            workingConditionSummary = "Presencial",
            marketSalaryReference = "Mercado",
            valuationNotes = "Notas",
            effectiveFromUtc = (DateTime?)null,
            effectiveToUtc = (DateTime?)null,
            allowInlineCatalogCreate = false,
            requirements = new[]
            {
                new
                {
                    requirementType,
                    requirementTypeCatalogItemPublicId = (Guid?)null,
                    catalogItemPublicId = (Guid?)null,
                    catalogCode = (string?)null,
                    catalogName = (string?)null,
                    description,
                    sortOrder = 1
                }
            },
            functions = new[]
            {
                new
                {
                    functionType = "General",
                    frequencyCatalogItemPublicId = (Guid?)null,
                    description = "Funcion",
                    sortOrder = 1
                }
            },
            relations = Array.Empty<object>(),
            competencies = Array.Empty<object>(),
            trainings = Array.Empty<object>(),
            compensation = (object?)null,
            benefits = Array.Empty<object>(),
            workingConditions = Array.Empty<object>(),
            dependentPositions = Array.Empty<object>()
        });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JobProfileItem>(JsonOptions);
        Assert.NotNull(payload);
        return payload!;
    }

    private async Task UpdateJobProfileAsync(
        HttpClient client,
        JobProfileItem profile,
        Guid companyId,
        string requirementType,
        string description)
    {
        var positionCategory = await EnsureDefaultPositionCategoryAsync(client, companyId);
        var orgUnit = await EnsureDefaultOrgUnitAsync(client, companyId);

        var response = await client.PutJsonAsync($"/api/v1/job-profiles/{profile.Id}", new
        {
            code = profile.Code,
            title = profile.Title,
            objective = "Objetivo",
            orgUnitPublicId = orgUnit.Id,
            reportsToJobProfilePublicId = (Guid?)null,
            positionCategoryPublicId = positionCategory.Id,
            strategicObjectiveCatalogItemPublicId = (Guid?)null,
            assignedWorkEquipmentCatalogItemPublicId = (Guid?)null,
            responsibilityCatalogItemPublicId = (Guid?)null,
            decisionScope = "Operacion",
            assignedResources = "Equipo",
            responsibilities = "Responsabilidades",
            benefitsSummary = "Ley",
            workingConditionSummary = "Presencial",
            marketSalaryReference = "Mercado",
            valuationNotes = "Notas",
            effectiveFromUtc = (DateTime?)null,
            effectiveToUtc = (DateTime?)null,
            allowInlineCatalogCreate = false,
            requirements = new[]
            {
                new
                {
                    requirementType,
                    catalogItemPublicId = (Guid?)null,
                    catalogCode = (string?)null,
                    catalogName = (string?)null,
                    description,
                    sortOrder = 1
                }
            },
            functions = new[]
            {
                new
                {
                    functionType = "General",
                    description = "Funcion",
                    sortOrder = 1
                }
            },
            relations = Array.Empty<object>(),
            competencies = Array.Empty<object>(),
            trainings = Array.Empty<object>(),
            compensation = (object?)null,
            benefits = Array.Empty<object>(),
            workingConditions = Array.Empty<object>(),
            dependentPositions = Array.Empty<object>(),
            concurrencyToken = profile.ConcurrencyToken
        });
        response.EnsureSuccessStatusCode();
    }

    private async Task<OrgStructureCatalogItem> EnsureOrgUnitTypeAsync(HttpClient client, Guid companyId, string code)
    {
        var listResponse = await client.GetAsync(
            $"/api/v1/companies/{companyId}/org-structure-catalogs/unit-types?page=1&pageSize=100&q={Uri.EscapeDataString(code)}");
        listResponse.EnsureSuccessStatusCode();

        var listPayload = await listResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<OrgStructureCatalogItem>>(JsonOptions);
        Assert.NotNull(listPayload);

        var existing = listPayload!.Items.FirstOrDefault(item => item.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var createResponse = await client.PostJsonAsync($"/api/v1/companies/{companyId}/org-structure-catalogs/unit-types", new
        {
            code,
            name = code,
            description = (string?)null,
            sortOrder = 10
        });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<OrgStructureCatalogItem>(JsonOptions);
        Assert.NotNull(created);
        return created!;
    }

    private async Task<PositionDescriptionCatalogItem> EnsurePositionDescriptionCatalogItemAsync(
        HttpClient client,
        Guid companyId,
        string routeSegment,
        string code)
    {
        var listResponse = await client.GetAsync(
            $"/api/v1/companies/{companyId}/position-description-catalogs/{routeSegment}/items?page=1&pageSize=100&q={Uri.EscapeDataString(code)}");
        listResponse.EnsureSuccessStatusCode();

        var listPayload = await listResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<PositionDescriptionCatalogItem>>(JsonOptions);
        Assert.NotNull(listPayload);

        var existing = listPayload!.Items.FirstOrDefault(item => item.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var createResponse = await client.PostJsonAsync($"/api/v1/companies/{companyId}/position-description-catalogs/{routeSegment}/items", new
        {
            code,
            name = code,
            description = (string?)null,
            sortOrder = 10
        });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<PositionDescriptionCatalogItem>(JsonOptions);
        Assert.NotNull(created);
        return created!;
    }

    private async Task<PositionCategoryClassificationItem> EnsurePositionCategoryClassificationAsync(
        HttpClient client,
        Guid companyId,
        string code,
        Guid positionFunctionTypeId,
        Guid positionContractTypeId,
        Guid orgUnitTypeId)
    {
        var listResponse = await client.GetAsync(
            $"/api/v1/companies/{companyId}/position-category-classifications?page=1&pageSize=100&q={Uri.EscapeDataString(code)}");
        listResponse.EnsureSuccessStatusCode();

        var listPayload = await listResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<PositionCategoryClassificationItem>>(JsonOptions);
        Assert.NotNull(listPayload);

        var existing = listPayload!.Items.FirstOrDefault(item => item.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var createResponse = await client.PostJsonAsync($"/api/v1/companies/{companyId}/position-category-classifications", new
        {
            code,
            name = code,
            description = (string?)null,
            positionFunctionTypePublicId = positionFunctionTypeId,
            positionContractTypePublicId = positionContractTypeId,
            orgUnitTypePublicId = orgUnitTypeId,
            sortOrder = 10
        });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<PositionCategoryClassificationItem>(JsonOptions);
        Assert.NotNull(created);
        return created!;
    }

    private async Task<PositionCategoryItem> EnsurePositionCategoryAsync(
        HttpClient client,
        Guid companyId,
        string code,
        Guid classificationId)
    {
        var listResponse = await client.GetAsync(
            $"/api/v1/companies/{companyId}/position-categories?page=1&pageSize=100&q={Uri.EscapeDataString(code)}");
        listResponse.EnsureSuccessStatusCode();

        var listPayload = await listResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<PositionCategoryItem>>(JsonOptions);
        Assert.NotNull(listPayload);

        var existing = listPayload!.Items.FirstOrDefault(item => item.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var createResponse = await client.PostJsonAsync($"/api/v1/companies/{companyId}/position-categories", new
        {
            code,
            name = code,
            description = (string?)null,
            classificationPublicId = classificationId,
            sortOrder = 10
        });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<PositionCategoryItem>(JsonOptions);
        Assert.NotNull(created);
        return created!;
    }

    private async Task<PositionCategoryItem> EnsureDefaultPositionCategoryAsync(HttpClient client, Guid companyId)
    {
        var orgUnitType = await EnsureOrgUnitTypeAsync(client, companyId, "Direccion");
        var functionType = await EnsurePositionDescriptionCatalogItemAsync(client, companyId, "position-function-types", "FUNC-BASE");
        var contractType = await EnsurePositionDescriptionCatalogItemAsync(client, companyId, "position-contract-types", "CON-BASE");
        var classification = await EnsurePositionCategoryClassificationAsync(
            client,
            companyId,
            "CLASS-BASE",
            functionType.Id,
            contractType.Id,
            orgUnitType.Id);

        return await EnsurePositionCategoryAsync(client, companyId, "CAT-BASE", classification.Id);
    }

    private static StringContent CreateJsonPatchContent(params object[] operations) =>
        new(
            JsonSerializer.Serialize(operations),
            Encoding.UTF8,
            "application/json-patch+json");

    private async Task<OrgUnitItem> EnsureDefaultOrgUnitAsync(HttpClient client, Guid companyId)
    {
        const string orgUnitCode = "OU-BASE";
        var orgUnitType = await EnsureOrgUnitTypeAsync(client, companyId, "Direccion");

        var listResponse = await client.GetAsync(
            $"/api/v1/companies/{companyId}/org-units?page=1&pageSize=100&q={Uri.EscapeDataString(orgUnitCode)}");
        listResponse.EnsureSuccessStatusCode();

        var listPayload = await listResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<OrgUnitItem>>(JsonOptions);
        Assert.NotNull(listPayload);

        var existing = listPayload!.Items.FirstOrDefault(item => item.Code.Equals(orgUnitCode, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var createResponse = await client.PostJsonAsync($"/api/v1/companies/{companyId}/org-units", new
        {
            code = orgUnitCode,
            name = "Unidad Base",
            orgUnitTypePublicId = orgUnitType.Id,
            functionalAreaPublicId = (Guid?)null,
            parentPublicId = (Guid?)null,
            sortOrder = (int?)null,
            description = (string?)null,
            costCenterCode = (string?)null,
            managerEmployeePublicId = (Guid?)null
        });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<OrgUnitItem>(JsonOptions);
        Assert.NotNull(created);
        return created!;
    }

    private sealed record InternalCatalogDefinitionItem(
        string Context,
        string Identifier,
        string Label,
        string RenderType,
        string? CatalogKey,
        bool AllowCreate,
        int MinQueryLength);

    private sealed record InternalCatalogValueSuggestionItem(Guid Id, string Value, double Score);

    private sealed record PagedResponseEnvelope<TItem>(IReadOnlyCollection<TItem> Items);

    private sealed record JobProfileItem(Guid Id, string Code, string Title, Guid ConcurrencyToken);

    private sealed record OrgUnitItem(Guid Id, string Code);

    private sealed record OrgStructureCatalogItem(Guid Id, string Code);

    private sealed record PositionDescriptionCatalogItem(
        Guid Id,
        string Code,
        string Name,
        bool IsActive,
        Guid ConcurrencyToken);

    private sealed record PositionCategoryClassificationItem(
        Guid Id,
        string Code,
        string Name,
        int SortOrder,
        bool IsActive,
        Guid ConcurrencyToken);

    private sealed record PositionCategoryItem(
        Guid Id,
        string Code,
        string Name,
        bool IsActive,
        Guid ConcurrencyToken);
}
