using System.Net;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// H-10 — the catalog manifest is what the frontend reads to know which catalog feeds each Job Profile field,
/// and it hands over a ready-to-call URL. For <c>positionCategoryPublicId</c> it named
/// <c>position-function-types</c>, which is a different table: the field resolves against
/// <c>position_categories</c>. Since the publicIds come from different tables, EVERY option in the list the
/// manifest pointed at was guaranteed to fail with <c>POSITION_CATEGORY_NOT_FOUND</c> — not "the wrong value
/// gets saved", but "nothing offered can ever work".
///
/// The cause was a gap in the manifest's vocabulary, not a typo: it could only describe the three catalog
/// families, and <c>position-categories</c> is its own resource whose rows hang off a *classification*
/// combining three axes. Whoever wrote the binding pointed at the nearest available thing — the first axis.
///
/// The pair of end-to-end tests below is what makes the class of bug impossible to reintroduce silently: they
/// do not compare strings, they follow the URL the manifest publishes and feed the result back into the field
/// it claims to describe.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    [Fact]
    public async Task CatalogManifest_PositionCategoryField_ShouldNameThePositionCategoriesEndpoint()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var field = await GetManifestFieldAsync(client, "jobProfile", "positionCategoryPublicId");

        Assert.Equal("position-categories", field.GetProperty("slug").GetString());
        Assert.Equal(
            $"/api/v1/companies/{scenario.TenantId}/position-categories",
            field.GetProperty("apiEndpointTemplate").GetString());
    }

    [Fact]
    public async Task CatalogManifest_ShouldPublishPositionCategoryClassifications()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var field = await GetManifestFieldAsync(client, "positionCategory", "classificationPublicId");

        Assert.Equal("position-category-classifications", field.GetProperty("slug").GetString());
        Assert.Equal(
            $"/api/v1/companies/{scenario.TenantId}/position-category-classifications",
            field.GetProperty("apiEndpointTemplate").GetString());
    }

    /// <summary>
    /// The test that would have caught H-10 on day one: walk the URL the manifest publishes and hand the id it
    /// returns to the field the manifest says it feeds. A string comparison can be kept in sync by accident;
    /// this cannot pass unless the binding is genuinely correct.
    /// </summary>
    [Fact]
    public async Task CatalogManifest_PositionCategoryEndpoint_ShouldReturnIdsTheProfileFieldAccepts()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-H10-CAT", "Direccion H10", "Direccion");
        _ = await EnsureDefaultPositionCategoryAsync(client, scenario.TenantId);

        var field = await GetManifestFieldAsync(client, "jobProfile", "positionCategoryPublicId");
        var categoryPublicId = await GetFirstPublicIdFromManifestEndpointAsync(client, field);

        var response = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/job-profiles", new
        {
            code = "JP-H10-CAT",
            title = "Perfil H10",
            objective = "Objetivo",
            orgUnitPublicId = orgUnit.Id,
            reportsToJobProfilePublicId = (Guid?)null,
            positionCategoryPublicId = categoryPublicId,
            decisionScope = "Operacion",
            assignedResources = "Equipo",
            responsibilities = "Responsabilidades",
            effectiveFromUtc = (DateTime?)null,
            effectiveToUtc = (DateTime?)null,
            allowInlineCatalogCreate = false
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CatalogManifest_ClassificationsEndpoint_ShouldReturnIdsThePositionCategoryAccepts()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        _ = await EnsureDefaultPositionCategoryAsync(client, scenario.TenantId);

        var field = await GetManifestFieldAsync(client, "positionCategory", "classificationPublicId");
        var classificationPublicId = await GetFirstPublicIdFromManifestEndpointAsync(client, field);

        var response = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/position-categories", new
        {
            code = "CAT-H10",
            name = "Categoria H10",
            description = (string?)null,
            classificationPublicId,
            sortOrder = 10
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>
    /// H-10 (segunda vuelta) — the manifest published `positionCategory.classificationPublicId`, so a frontend
    /// could CHOOSE a classification from the map but not CREATE one: the three axes it is built from were
    /// absent, and all three are required. Covering a form halfway is worse than covering none of it — the
    /// client still needs the special case, and now it can miss that a field is missing.
    /// </summary>
    [Theory]
    [InlineData("positionFunctionTypePublicId", "position-function-types", "/api/v1/companies/{0}/position-description-catalogs/position-function-types/items")]
    [InlineData("positionContractTypePublicId", "position-contract-types", "/api/v1/companies/{0}/position-description-catalogs/position-contract-types/items")]
    [InlineData("orgUnitTypePublicId", "unit-types", "/api/v1/companies/{0}/organization-structure-catalogs/unit-types")]
    public async Task CatalogManifest_ShouldPublishTheThreeClassificationAxes(
        string fieldName,
        string expectedSlug,
        string expectedEndpointTemplate)
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var field = await GetManifestFieldAsync(client, "positionCategoryClassification", fieldName);

        Assert.Equal(expectedSlug, field.GetProperty("slug").GetString());
        Assert.Equal(
            string.Format(expectedEndpointTemplate, scenario.TenantId),
            field.GetProperty("apiEndpointTemplate").GetString());
    }

    /// <summary>
    /// Closes the chain end to end: unit-type / function-type / contract-type → classification → category →
    /// profile, every id taken from the URL the manifest itself published. The axis catalogs are seeded but NO
    /// classification is created first, so the combination is guaranteed new and a `409` on the unique-axes
    /// index cannot be mistaken for the binding being wrong.
    /// </summary>
    [Fact]
    public async Task CatalogManifest_ClassificationAxes_ShouldReturnIdsTheClassificationAccepts()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        _ = await EnsureOrgUnitTypeAsync(client, scenario.TenantId, "Direccion");
        _ = await EnsurePositionDescriptionCatalogItemAsync(client, scenario.TenantId, "position-function-types", "FUNC-H10");
        _ = await EnsurePositionDescriptionCatalogItemAsync(client, scenario.TenantId, "position-contract-types", "CON-H10");

        var functionTypeId = await GetFirstPublicIdFromManifestEndpointAsync(
            client, await GetManifestFieldAsync(client, "positionCategoryClassification", "positionFunctionTypePublicId"));
        var contractTypeId = await GetFirstPublicIdFromManifestEndpointAsync(
            client, await GetManifestFieldAsync(client, "positionCategoryClassification", "positionContractTypePublicId"));
        var orgUnitTypeId = await GetFirstPublicIdFromManifestEndpointAsync(
            client, await GetManifestFieldAsync(client, "positionCategoryClassification", "orgUnitTypePublicId"));

        var response = await client.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/position-category-classifications",
            new
            {
                code = "CLAS-H10",
                name = "Clasificacion H10",
                description = (string?)null,
                positionFunctionTypePublicId = functionTypeId,
                positionContractTypePublicId = contractTypeId,
                orgUnitTypePublicId = orgUnitTypeId,
                sortOrder = 10
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<JsonElement> GetManifestFieldAsync(HttpClient client, string subResource, string fieldName)
    {
        var response = await client.GetAsync("/api/v1/job-profiles/catalog-manifest");
        response.EnsureSuccessStatusCode();

        // Cloned because the JsonDocument is disposed with the payload; the returned element must outlive it.
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var group = document.RootElement
            .GetProperty("subResources")
            .EnumerateArray()
            .SingleOrDefault(item => item.GetProperty("subResource").GetString() == subResource);

        Assert.True(
            group.ValueKind == JsonValueKind.Object,
            $"The manifest publishes no '{subResource}' sub-resource.");

        var field = group.GetProperty("fields")
            .EnumerateArray()
            .SingleOrDefault(item => item.GetProperty("fieldName").GetString() == fieldName);

        Assert.True(
            field.ValueKind == JsonValueKind.Object,
            $"The manifest publishes no '{fieldName}' field under '{subResource}'.");

        return field.Clone();
    }

    private async Task<Guid> GetFirstPublicIdFromManifestEndpointAsync(HttpClient client, JsonElement field)
    {
        var url = field.GetProperty("apiEndpointTemplate").GetString();
        Assert.False(string.IsNullOrWhiteSpace(url));
        Assert.DoesNotContain("{companyId}", url!);

        var response = await client.GetAsync($"{url}?page=1&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = document.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.NotEmpty(items);

        return items[0].GetProperty("publicId").GetGuid();
    }
}
