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

        var response = await client.GetAsync($"/api/v1/job-profiles/internal-catalogs?context={RequirementCatalogContext}");
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
            $"/api/v1/job-profiles/internal-catalogs/{CertificationCatalogKey}/values?q={Uri.EscapeDataString("Azure AI Fundamentals")}");
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
            $"/api/v1/job-profiles/internal-catalogs/{CertificationCatalogKey}/values",
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
                new { op = "replace", path = "/isActive", value = (object)false })
        };
        frequencyPatchRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{frequency.ConcurrencyToken}\"");

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
                new { op = "replace", path = "/sortOrder", value = (object)25 })
        };
        classificationPatchRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{classification.ConcurrencyToken}\"");

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
                new { op = "replace", path = "/isActive", value = (object)false })
        };
        categoryPatchRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{category.ConcurrencyToken}\"");

        var categoryPatchResponse = await client.SendAsync(categoryPatchRequest);
        categoryPatchResponse.EnsureSuccessStatusCode();
        var patchedCategory = await categoryPatchResponse.Content.ReadFromJsonAsync<PositionCategoryItem>(JsonOptions);
        Assert.NotNull(patchedCategory);
        Assert.Equal(category.Id, patchedCategory!.Id);
        Assert.Equal("Categoria parcheada", patchedCategory.Name);
        Assert.False(patchedCategory.IsActive);
        Assert.NotEqual(category.ConcurrencyToken, patchedCategory.ConcurrencyToken);
    }

    // Documents the current verb contract of the 3 position-description-catalog
    // flat resource routes: there is intentionally NO DELETE (debt §3.2, Opción B —
    // soft-delete via PATCH replace /isActive) and NO PUT (debt §3.3, Opción A —
    // PATCH-only). Both verbs must return 405. If PUT/DELETE is ever added the
    // contract changes and this test fails, forcing a conscious decision.
    [Theory]
    [InlineData("DELETE", "/api/v1/position-categories/11111111-1111-1111-1111-111111111111")]
    [InlineData("PUT", "/api/v1/position-categories/11111111-1111-1111-1111-111111111111")]
    [InlineData("DELETE", "/api/v1/position-category-classifications/11111111-1111-1111-1111-111111111111")]
    [InlineData("PUT", "/api/v1/position-category-classifications/11111111-1111-1111-1111-111111111111")]
    [InlineData("DELETE", "/api/v1/position-description-catalogs/frequencies/items/11111111-1111-1111-1111-111111111111")]
    [InlineData("PUT", "/api/v1/position-description-catalogs/frequencies/items/11111111-1111-1111-1111-111111111111")]
    public async Task PositionDescriptionCatalogs_FlatResourceRoutes_ShouldReturn405_ForDeleteAndPut(
        string verb, string route)
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        using var request = new HttpRequestMessage(new HttpMethod(verb), route);

        var response = await client.SendAsync(request);

        // Authenticated admin client → the only reason for non-200 is the verb
        // genuinely not being mapped (405), not auth/tenant.
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);

        // §S2: the 405 must NOT have an empty body — endpoint-routing
        // short-circuits bypass MVC, so UseStatusCodePages re-emits the standard
        // ProblemDetails envelope. Assert the SDK-parseable contract (code +
        // traceId + status), closing the doc `03` §9.3 residual.
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("common.method_not_allowed", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(405, body.RootElement.GetProperty("status").GetInt32());
        Assert.False(
            string.IsNullOrWhiteSpace(body.RootElement.GetProperty("traceId").GetString()),
            "405 ProblemDetails must carry a non-empty traceId.");
    }

    // Regression guard for the N+1 remediation (ADR-0001, project-foundation §12.7):
    // 1. With includeAllowedActions=true the 3 catalog list endpoints still return a
    //    populated `allowedActions` object (contract preserved, field not nulled).
    // 2. Listings no longer gate by dependency: an in-use classification still reports
    //    canInactivate=true in the list (advisory hint only).
    // 3. Enforcement is intact server-side: PATCH inactivation of that in-use
    //    classification returns 409, independent of the list flag.
    [Fact]
    public async Task PositionDescriptionCatalogs_ListWithAllowedActions_ShouldNotGateByDependency_ButPatchStillEnforces()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));
        var companyId = scenario.TenantId;

        var orgUnitType = await EnsureOrgUnitTypeAsync(client, companyId, "ORG-DEP");
        var functionType = await EnsurePositionDescriptionCatalogItemAsync(client, companyId, "position-function-types", "FUNC-DEP");
        var contractType = await EnsurePositionDescriptionCatalogItemAsync(client, companyId, "position-contract-types", "CON-DEP");
        var classification = await EnsurePositionCategoryClassificationAsync(
            client,
            companyId,
            "CLASS-DEP",
            functionType.Id,
            contractType.Id,
            orgUnitType.Id);
        // Category referencing the classification => the classification is now "in use".
        await EnsurePositionCategoryAsync(client, companyId, "CAT-DEP", classification.Id);

        // (1) allowedActions populated on all three list endpoints with the flag on.
        var functionItem = await GetListItemWithAllowedActionsAsync(
            client,
            $"/api/v1/companies/{companyId}/position-description-catalogs/position-function-types/items?page=1&pageSize=100&q={Uri.EscapeDataString("FUNC-DEP")}&includeAllowedActions=true",
            "FUNC-DEP");
        AssertAllowedActionsShape(functionItem);

        var classificationItem = await GetListItemWithAllowedActionsAsync(
            client,
            $"/api/v1/companies/{companyId}/position-category-classifications?page=1&pageSize=100&q={Uri.EscapeDataString("CLASS-DEP")}&includeAllowedActions=true",
            "CLASS-DEP");
        AssertAllowedActionsShape(classificationItem);

        var categoryItem = await GetListItemWithAllowedActionsAsync(
            client,
            $"/api/v1/companies/{companyId}/position-categories?page=1&pageSize=100&q={Uri.EscapeDataString("CAT-DEP")}&includeAllowedActions=true",
            "CAT-DEP");
        AssertAllowedActionsShape(categoryItem);

        // (2) In-use classification still reports canInactivate=true in the list
        //     (dependency gating removed from listings — advisory hint only).
        var classificationAllowedActions = classificationItem.GetProperty("allowedActions");
        Assert.True(classificationAllowedActions.GetProperty("canInactivate").GetBoolean());

        // Contract: without the flag, allowedActions is absent/null.
        var withoutFlagResponse = await client.GetAsync(
            $"/api/v1/companies/{companyId}/position-category-classifications?page=1&pageSize=100&q={Uri.EscapeDataString("CLASS-DEP")}");
        withoutFlagResponse.EnsureSuccessStatusCode();
        using (var withoutFlagDocument = JsonDocument.Parse(await withoutFlagResponse.Content.ReadAsStringAsync()))
        {
            var firstItem = withoutFlagDocument.RootElement.GetProperty("items")[0];
            var hasAllowedActions = firstItem.TryGetProperty("allowedActions", out var allowedActions)
                && allowedActions.ValueKind != JsonValueKind.Null;
            Assert.False(hasAllowedActions);
        }

        // (3) Server-side enforcement intact: PATCH inactivation of the in-use
        //     classification returns 409, regardless of the list advisory hint.
        using var inactivateRequest = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/position-category-classifications/{classification.Id}")
        {
            Content = CreateJsonPatchContent(
                new { op = "replace", path = "/isActive", value = (object)false })
        };
        inactivateRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{classification.ConcurrencyToken}\"");

        var inactivateResponse = await client.SendAsync(inactivateRequest);
        Assert.Equal(HttpStatusCode.Conflict, inactivateResponse.StatusCode);
    }

    private async Task<JsonElement> GetListItemWithAllowedActionsAsync(HttpClient client, string url, string code)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var match = document.RootElement
            .GetProperty("items")
            .EnumerateArray()
            .Single(item => item.GetProperty("code").GetString() == code);

        return match.Clone();
    }

    private static void AssertAllowedActionsShape(JsonElement item)
    {
        Assert.True(item.TryGetProperty("allowedActions", out var allowedActions));
        Assert.Equal(JsonValueKind.Object, allowedActions.ValueKind);
        Assert.True(allowedActions.TryGetProperty("canEdit", out _));
        Assert.True(allowedActions.TryGetProperty("reasons", out var reasons));
        Assert.Equal(JsonValueKind.Array, reasons.ValueKind);
    }

    // Regression guard for the P2 remediation (ADR-0002, project-foundation §12.8):
    // a non-empty `q` shorter than the minimum length is rejected with 400 before
    // touching cache/DB; empty/whitespace `q` stays "no filter" (200).
    [Theory]
    [InlineData("&q=a", HttpStatusCode.BadRequest)]
    [InlineData("&q=%20", HttpStatusCode.OK)]   // whitespace => no filter
    [InlineData("&q=ab", HttpStatusCode.OK)]
    [InlineData("", HttpStatusCode.OK)]          // no q => no filter
    public async Task PositionCategories_Search_ShouldEnforceMinimumQueryLength(
        string queryString, HttpStatusCode expectedStatus)
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var response = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/position-categories?page=1&pageSize=20{queryString}");

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    // Regression guard for the P4 remediation (ADR-0004): after consolidating the
    // double classification resolution into a single entity lookup + null-check,
    // creating a category with a non-existent classification still returns
    // ClassificationNotFound (existence semantics preserved).
    [Fact]
    public async Task PositionCategories_Create_WithUnknownClassification_ShouldReturnNotFound()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var response = await client.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/position-categories",
            new
            {
                code = "CAT-NOCLASS",
                name = "CAT-NOCLASS",
                description = (string?)null,
                classificationPublicId = Guid.NewGuid(), // does not exist
                sortOrder = 10
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
            marketSalaryReference = "Mercado",
            valuationNotes = "Notas",
            effectiveFromUtc = (DateTime?)null,
            effectiveToUtc = (DateTime?)null,
            allowInlineCatalogCreate = false
        });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JobProfileItem>(JsonOptions);
        Assert.NotNull(payload);
        
        var reqResponse = await client.PostJsonAsync($"/api/v1/job-profiles/{payload!.Id}/requirements", new
        {
            requirementType,
            requirementTypeCatalogItemPublicId = (Guid?)null,
            catalogItemPublicId = (Guid?)null,
            catalogCode = (string?)null,
            catalogName = (string?)null,
            description,
            sortOrder = 1
        });
        reqResponse.EnsureSuccessStatusCode();

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

        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/job-profiles/{profile.Id}")
        {
            Content = JsonContent.Create(new
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
                marketSalaryReference = "Mercado",
                valuationNotes = "Notas",
                effectiveFromUtc = (DateTime?)null,
                effectiveToUtc = (DateTime?)null,
                allowInlineCatalogCreate = false
            })
        };
        updateRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{profile.ConcurrencyToken}\"");
        var updateResponse = await client.SendAsync(updateRequest);
        updateResponse.EnsureSuccessStatusCode();

        var reqResponse = await client.PostJsonAsync($"/api/v1/job-profiles/{profile.Id}/requirements", new
        {
            requirementType,
            catalogItemPublicId = (Guid?)null,
            catalogCode = (string?)null,
            catalogName = (string?)null,
            description,
            sortOrder = 1
        });
        reqResponse.EnsureSuccessStatusCode();
    }

    private async Task<OrgStructureCatalogItem> EnsureOrgUnitTypeAsync(HttpClient client, Guid companyId, string code)
    {
        var listResponse = await client.GetAsync(
            $"/api/v1/companies/{companyId}/organization-structure-catalogs/unit-types?page=1&pageSize=100&q={Uri.EscapeDataString(code)}");
        listResponse.EnsureSuccessStatusCode();

        var listPayload = await listResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<OrgStructureCatalogItem>>(JsonOptions);
        Assert.NotNull(listPayload);

        var existing = listPayload!.Items.FirstOrDefault(item => item.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var createResponse = await client.PostJsonAsync($"/api/v1/companies/{companyId}/organization-structure-catalogs/unit-types", new
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
            $"/api/v1/companies/{companyId}/organization-units?page=1&pageSize=100&q={Uri.EscapeDataString(orgUnitCode)}");
        listResponse.EnsureSuccessStatusCode();

        var listPayload = await listResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<OrgUnitItem>>(JsonOptions);
        Assert.NotNull(listPayload);

        var existing = listPayload!.Items.FirstOrDefault(item => item.Code.Equals(orgUnitCode, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var createResponse = await client.PostJsonAsync($"/api/v1/companies/{companyId}/organization-units", new
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
