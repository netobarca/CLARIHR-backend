using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CLARIHR.Domain.Platform;

namespace CLARIHR.Api.IntegrationTests;

public sealed class BackofficeEducationCatalogsIntegrationTests(BackofficeIntegrationTestWebApplicationFactory factory)
    : IClassFixture<BackofficeIntegrationTestWebApplicationFactory>
{
    // Career administration is seed-only since RF-009/DP-06 (the "careers" key now yields 404);
    // study-types exercises the same generic CRUD machinery.
    private const string CatalogKey = "study-types";
    private static readonly JsonSerializerOptions JsonOptions = IntegrationTestJson.CreateOptions();
    private static readonly Guid AdminUserId = Guid.Parse("90000000-0000-0000-0000-000000000071");

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        await factory.ResetDatabaseAsync(dbContext =>
            PlatformTestSeed.SeedPlatformOperatorAsync(
                dbContext,
                AdminUserId,
                "platform.education@clarihr.test",
                "hashed-password",
                PlatformOperatorRole.Admin));

        return factory.CreateClientFor(TestUserContext.PlatformAuthenticatedWithoutTenant(AdminUserId));
    }

    private async Task<EducationCatalogItemEnvelope> CreateItemAsync(HttpClient client, string code = "TEST_CAREER", string name = "Test Career", int sortOrder = 500)
    {
        var createResponse = await client.PostJsonAsync($"/api/platform/education-catalogs/{CatalogKey}", new { code, name, sortOrder });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.ETag);
        var created = await createResponse.Content.ReadFromJsonAsync<EducationCatalogItemEnvelope>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal($"\"{created!.ConcurrencyToken:D}\"", createResponse.Headers.ETag!.Tag);
        return created;
    }

    [Fact]
    public async Task EducationCatalogs_CrudLifecycle_WithIfMatch_ShouldRotateTokenAndReturnETag()
    {
        using var client = await CreateAdminClientAsync();

        var created = await CreateItemAsync(client);
        Assert.Equal("TEST_CAREER", created.Code);
        Assert.True(created.IsActive);

        using var putRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/platform/education-catalogs/{CatalogKey}/{created.Id}")
        {
            Content = JsonBody(new { code = "TEST_CAREER", name = "Updated Career", sortOrder = 501 })
        };
        putRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{created.ConcurrencyToken}\"");
        var putResponse = await client.SendAsync(putRequest);
        putResponse.EnsureSuccessStatusCode();
        var updated = await putResponse.Content.ReadFromJsonAsync<EducationCatalogItemEnvelope>(JsonOptions);
        Assert.Equal("Updated Career", updated!.Name);
        Assert.NotEqual(created.ConcurrencyToken, updated.ConcurrencyToken);
        Assert.Equal($"\"{updated.ConcurrencyToken:D}\"", putResponse.Headers.ETag!.Tag);

        using var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/platform/education-catalogs/{CatalogKey}/{created.Id}")
        {
            Content = PatchBody(new[] { new { op = "replace", path = "/name", value = "Patched Career" } })
        };
        patchRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{updated.ConcurrencyToken}\"");
        var patchResponse = await client.SendAsync(patchRequest);
        patchResponse.EnsureSuccessStatusCode();
        var patched = await patchResponse.Content.ReadFromJsonAsync<EducationCatalogItemEnvelope>(JsonOptions);
        Assert.Equal("Patched Career", patched!.Name);
        Assert.NotEqual(updated.ConcurrencyToken, patched.ConcurrencyToken);
        Assert.Equal($"\"{patched.ConcurrencyToken:D}\"", patchResponse.Headers.ETag!.Tag);

        using var inactivateRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/platform/education-catalogs/{CatalogKey}/{created.Id}/inactivate");
        inactivateRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{patched.ConcurrencyToken}\"");
        var inactivateResponse = await client.SendAsync(inactivateRequest);
        inactivateResponse.EnsureSuccessStatusCode();
        var inactivated = await inactivateResponse.Content.ReadFromJsonAsync<EducationCatalogItemEnvelope>(JsonOptions);
        Assert.False(inactivated!.IsActive);

        using var activateRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/platform/education-catalogs/{CatalogKey}/{created.Id}/activate");
        activateRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{inactivated.ConcurrencyToken}\"");
        var activateResponse = await client.SendAsync(activateRequest);
        activateResponse.EnsureSuccessStatusCode();
        var activated = await activateResponse.Content.ReadFromJsonAsync<EducationCatalogItemEnvelope>(JsonOptions);
        Assert.True(activated!.IsActive);
        Assert.Equal($"\"{activated.ConcurrencyToken:D}\"", activateResponse.Headers.ETag!.Tag);
    }

    [Fact]
    public async Task EducationCatalogs_Put_WithoutIfMatch_ShouldReturn400()
    {
        using var client = await CreateAdminClientAsync();
        var created = await CreateItemAsync(client, code: "EDU_NOIFMATCH");

        var response = await client.PutJsonAsync($"/api/platform/education-catalogs/{CatalogKey}/{created.Id}", new
        {
            code = "EDU_NOIFMATCH",
            name = "Sin If-Match",
            sortOrder = 10
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EducationCatalogs_Patch_WithStaleIfMatch_ShouldReturn409Conflict()
    {
        using var client = await CreateAdminClientAsync();
        var created = await CreateItemAsync(client, code: "EDU_STALE");

        using var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/platform/education-catalogs/{CatalogKey}/{created.Id}")
        {
            Content = PatchBody(new[] { new { op = "replace", path = "/name", value = "x" } })
        };
        patchRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{Guid.NewGuid()}\"");

        var response = await client.SendAsync(patchRequest);
        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task EducationCatalogs_Patch_WithIsActivePath_ShouldReturn400()
    {
        using var client = await CreateAdminClientAsync();
        var created = await CreateItemAsync(client, code: "EDU_STATUSPATCH");

        using var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/platform/education-catalogs/{CatalogKey}/{created.Id}")
        {
            Content = PatchBody(new[] { new { op = "replace", path = "/isActive", value = false } })
        };
        patchRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{created.ConcurrencyToken}\"");

        var response = await client.SendAsync(patchRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EducationCatalogs_Search_WithUnknownCatalogKey_ShouldReturn404()
    {
        using var client = await CreateAdminClientAsync();

        var response = await client.GetAsync("/api/platform/education-catalogs/not-a-real-catalog");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static StringContent JsonBody(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    private static StringContent PatchBody(object operations) =>
        new(JsonSerializer.Serialize(operations), Encoding.UTF8, "application/json-patch+json");

    private static async Task AssertProblemDetailsAsync(HttpResponseMessage response, HttpStatusCode expectedStatus, string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
    }

    private sealed record EducationCatalogItemEnvelope(
        Guid Id,
        string CatalogType,
        string Code,
        string Name,
        int SortOrder,
        bool IsActive,
        Guid ConcurrencyToken,
        DateTime CreatedAtUtc,
        DateTime? ModifiedAtUtc);
}
