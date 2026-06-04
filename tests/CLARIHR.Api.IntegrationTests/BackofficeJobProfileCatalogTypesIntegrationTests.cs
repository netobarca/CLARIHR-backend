using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CLARIHR.Domain.Platform;

namespace CLARIHR.Api.IntegrationTests;

public sealed class BackofficeJobProfileCatalogTypesIntegrationTests(BackofficeIntegrationTestWebApplicationFactory factory)
    : IClassFixture<BackofficeIntegrationTestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = IntegrationTestJson.CreateOptions();
    private static readonly Guid AdminUserId = Guid.Parse("90000000-0000-0000-0000-000000000061");

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        await factory.ResetDatabaseAsync(dbContext =>
            PlatformTestSeed.SeedPlatformOperatorAsync(
                dbContext,
                AdminUserId,
                "platform.jpctypes@clarihr.test",
                "hashed-password",
                PlatformOperatorRole.Admin));

        return factory.CreateClientFor(TestUserContext.PlatformAuthenticatedWithoutTenant(AdminUserId));
    }

    private async Task<JobProfileCatalogTypeEnvelope> CreateItemAsync(HttpClient client, string code = "TEST_JPC_TYPE", string name = "Test Type", int sortOrder = 500)
    {
        var createResponse = await client.PostJsonAsync("/api/platform/job-profile-catalog-types", new { code, name, sortOrder });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.ETag);
        var created = await createResponse.Content.ReadFromJsonAsync<JobProfileCatalogTypeEnvelope>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal($"\"{created!.ConcurrencyToken:D}\"", createResponse.Headers.ETag!.Tag);
        return created;
    }

    [Fact]
    public async Task JobProfileCatalogTypes_CrudLifecycle_WithIfMatch_ShouldRotateTokenAndReturnETag()
    {
        using var client = await CreateAdminClientAsync();

        var created = await CreateItemAsync(client);
        Assert.Equal("TEST_JPC_TYPE", created.Code);
        Assert.True(created.IsActive);

        // PUT with valid If-Match (code is immutable; body omits it).
        using var putRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/platform/job-profile-catalog-types/{created.Id}")
        {
            Content = JsonBody(new { name = "Updated Type", sortOrder = 501 })
        };
        putRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{created.ConcurrencyToken}\"");
        var putResponse = await client.SendAsync(putRequest);
        putResponse.EnsureSuccessStatusCode();
        var updated = await putResponse.Content.ReadFromJsonAsync<JobProfileCatalogTypeEnvelope>(JsonOptions);
        Assert.Equal("Updated Type", updated!.Name);
        Assert.Equal("TEST_JPC_TYPE", updated.Code);
        Assert.Equal(501, updated.SortOrder);
        Assert.NotEqual(created.ConcurrencyToken, updated.ConcurrencyToken);
        Assert.Equal($"\"{updated.ConcurrencyToken:D}\"", putResponse.Headers.ETag!.Tag);

        // PATCH (RFC-6902) /name with valid If-Match.
        using var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/platform/job-profile-catalog-types/{created.Id}")
        {
            Content = PatchBody(new[] { new { op = "replace", path = "/name", value = "Patched Type" } })
        };
        patchRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{updated.ConcurrencyToken}\"");
        var patchResponse = await client.SendAsync(patchRequest);
        patchResponse.EnsureSuccessStatusCode();
        var patched = await patchResponse.Content.ReadFromJsonAsync<JobProfileCatalogTypeEnvelope>(JsonOptions);
        Assert.Equal("Patched Type", patched!.Name);
        Assert.NotEqual(updated.ConcurrencyToken, patched.ConcurrencyToken);
        Assert.Equal($"\"{patched.ConcurrencyToken:D}\"", patchResponse.Headers.ETag!.Tag);

        // Inactivate then activate with valid If-Match.
        using var inactivateRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/platform/job-profile-catalog-types/{created.Id}/inactivate");
        inactivateRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{patched.ConcurrencyToken}\"");
        var inactivateResponse = await client.SendAsync(inactivateRequest);
        inactivateResponse.EnsureSuccessStatusCode();
        var inactivated = await inactivateResponse.Content.ReadFromJsonAsync<JobProfileCatalogTypeEnvelope>(JsonOptions);
        Assert.False(inactivated!.IsActive);

        using var activateRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/platform/job-profile-catalog-types/{created.Id}/activate");
        activateRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{inactivated.ConcurrencyToken}\"");
        var activateResponse = await client.SendAsync(activateRequest);
        activateResponse.EnsureSuccessStatusCode();
        var activated = await activateResponse.Content.ReadFromJsonAsync<JobProfileCatalogTypeEnvelope>(JsonOptions);
        Assert.True(activated!.IsActive);
        Assert.Equal($"\"{activated.ConcurrencyToken:D}\"", activateResponse.Headers.ETag!.Tag);
    }

    [Fact]
    public async Task JobProfileCatalogTypes_Put_WithoutIfMatch_ShouldReturn400()
    {
        using var client = await CreateAdminClientAsync();
        var created = await CreateItemAsync(client, code: "JPC_NOIFMATCH");

        var response = await client.PutJsonAsync($"/api/platform/job-profile-catalog-types/{created.Id}", new
        {
            name = "Sin If-Match",
            sortOrder = 10
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task JobProfileCatalogTypes_Put_WithStaleIfMatch_ShouldReturn409Conflict()
    {
        using var client = await CreateAdminClientAsync();
        var created = await CreateItemAsync(client, code: "JPC_STALE");

        using var putRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/platform/job-profile-catalog-types/{created.Id}")
        {
            Content = JsonBody(new { name = "Obsoleto", sortOrder = 11 })
        };
        putRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{Guid.NewGuid()}\"");

        var response = await client.SendAsync(putRequest);
        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task JobProfileCatalogTypes_Patch_WithStaleIfMatch_ShouldReturn409Conflict()
    {
        using var client = await CreateAdminClientAsync();
        var created = await CreateItemAsync(client, code: "JPC_PATCHSTALE");

        using var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/platform/job-profile-catalog-types/{created.Id}")
        {
            Content = PatchBody(new[] { new { op = "replace", path = "/name", value = "x" } })
        };
        patchRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{Guid.NewGuid()}\"");

        var response = await client.SendAsync(patchRequest);
        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task JobProfileCatalogTypes_Patch_WithCodePath_ShouldReturn400()
    {
        using var client = await CreateAdminClientAsync();
        var created = await CreateItemAsync(client, code: "JPC_CODEPATCH");

        // The code is immutable; a /code patch op must be rejected with 400.
        using var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/platform/job-profile-catalog-types/{created.Id}")
        {
            Content = PatchBody(new[] { new { op = "replace", path = "/code", value = "NEW_CODE" } })
        };
        patchRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{created.ConcurrencyToken}\"");

        var response = await client.SendAsync(patchRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    private sealed record JobProfileCatalogTypeEnvelope(
        Guid Id,
        string Code,
        string Name,
        int SortOrder,
        bool IsActive,
        Guid ConcurrencyToken,
        DateTime CreatedAtUtc,
        DateTime? ModifiedAtUtc);
}
