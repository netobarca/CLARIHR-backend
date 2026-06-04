using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CLARIHR.Domain.Platform;

namespace CLARIHR.Api.IntegrationTests;

public sealed class BackofficeDocumentTypeCatalogsIntegrationTests(BackofficeIntegrationTestWebApplicationFactory factory)
    : IClassFixture<BackofficeIntegrationTestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = IntegrationTestJson.CreateOptions();
    private static readonly Guid AdminUserId = Guid.Parse("90000000-0000-0000-0000-000000000051");

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        await factory.ResetDatabaseAsync(dbContext =>
            PlatformTestSeed.SeedPlatformOperatorAsync(
                dbContext,
                AdminUserId,
                "platform.doctypes@clarihr.test",
                "hashed-password",
                PlatformOperatorRole.Admin));

        return factory.CreateClientFor(TestUserContext.PlatformAuthenticatedWithoutTenant(AdminUserId));
    }

    private async Task<DocumentTypeCatalogItemEnvelope> CreateItemAsync(HttpClient client, string code = "DOC_PRUEBA", string name = "Documento de Prueba", int sortOrder = 500)
    {
        var createResponse = await client.PostJsonAsync("/api/platform/document-type-catalogs", new { code, name, sortOrder });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        // The current concurrency token is exposed in the ETag header on 201.
        Assert.NotNull(createResponse.Headers.ETag);
        var created = await createResponse.Content.ReadFromJsonAsync<DocumentTypeCatalogItemEnvelope>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal($"\"{created!.ConcurrencyToken:D}\"", createResponse.Headers.ETag!.Tag);
        return created;
    }

    [Fact]
    public async Task DocumentTypeCatalogs_CrudLifecycle_WithIfMatch_ShouldRotateTokenAndReturnETag()
    {
        using var client = await CreateAdminClientAsync();

        var created = await CreateItemAsync(client);
        Assert.Equal("DOC_PRUEBA", created.Code);
        Assert.True(created.IsActive);

        // PUT with valid If-Match.
        using var putRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/platform/document-type-catalogs/{created.Id}")
        {
            Content = JsonBody(new { code = "DOC_PRUEBA", name = "Documento Actualizado", sortOrder = 501 })
        };
        putRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{created.ConcurrencyToken}\"");
        var putResponse = await client.SendAsync(putRequest);
        putResponse.EnsureSuccessStatusCode();
        var updated = await putResponse.Content.ReadFromJsonAsync<DocumentTypeCatalogItemEnvelope>(JsonOptions);
        Assert.Equal("Documento Actualizado", updated!.Name);
        Assert.Equal(501, updated.SortOrder);
        Assert.NotEqual(created.ConcurrencyToken, updated.ConcurrencyToken);
        Assert.Equal($"\"{updated.ConcurrencyToken:D}\"", putResponse.Headers.ETag!.Tag);

        // PATCH (RFC-6902) /name with valid If-Match.
        using var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/platform/document-type-catalogs/{created.Id}")
        {
            Content = PatchBody(new[] { new { op = "replace", path = "/name", value = "Documento Patcheado" } })
        };
        patchRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{updated.ConcurrencyToken}\"");
        var patchResponse = await client.SendAsync(patchRequest);
        patchResponse.EnsureSuccessStatusCode();
        var patched = await patchResponse.Content.ReadFromJsonAsync<DocumentTypeCatalogItemEnvelope>(JsonOptions);
        Assert.Equal("Documento Patcheado", patched!.Name);
        Assert.NotEqual(updated.ConcurrencyToken, patched.ConcurrencyToken);
        Assert.Equal($"\"{patched.ConcurrencyToken:D}\"", patchResponse.Headers.ETag!.Tag);

        // Inactivate with valid If-Match.
        using var inactivateRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/platform/document-type-catalogs/{created.Id}/inactivate");
        inactivateRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{patched.ConcurrencyToken}\"");
        var inactivateResponse = await client.SendAsync(inactivateRequest);
        inactivateResponse.EnsureSuccessStatusCode();
        var inactivated = await inactivateResponse.Content.ReadFromJsonAsync<DocumentTypeCatalogItemEnvelope>(JsonOptions);
        Assert.False(inactivated!.IsActive);
        Assert.NotEqual(patched.ConcurrencyToken, inactivated.ConcurrencyToken);

        // Activate with valid If-Match.
        using var activateRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/platform/document-type-catalogs/{created.Id}/activate");
        activateRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{inactivated.ConcurrencyToken}\"");
        var activateResponse = await client.SendAsync(activateRequest);
        activateResponse.EnsureSuccessStatusCode();
        var activated = await activateResponse.Content.ReadFromJsonAsync<DocumentTypeCatalogItemEnvelope>(JsonOptions);
        Assert.True(activated!.IsActive);
        Assert.Equal($"\"{activated.ConcurrencyToken:D}\"", activateResponse.Headers.ETag!.Tag);
    }

    [Fact]
    public async Task DocumentTypeCatalogs_Put_WithoutIfMatch_ShouldReturn400()
    {
        using var client = await CreateAdminClientAsync();
        var created = await CreateItemAsync(client, code: "DOC_NOIFMATCH");

        var response = await client.PutJsonAsync($"/api/platform/document-type-catalogs/{created.Id}", new
        {
            code = "DOC_NOIFMATCH",
            name = "Sin If-Match",
            sortOrder = 10
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DocumentTypeCatalogs_Put_WithStaleIfMatch_ShouldReturn409Conflict()
    {
        using var client = await CreateAdminClientAsync();
        var created = await CreateItemAsync(client, code: "DOC_STALE");

        using var putRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/platform/document-type-catalogs/{created.Id}")
        {
            Content = JsonBody(new { code = "DOC_STALE", name = "Obsoleto", sortOrder = 11 })
        };
        putRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{Guid.NewGuid()}\"");

        var response = await client.SendAsync(putRequest);
        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task DocumentTypeCatalogs_Patch_WithStaleIfMatch_ShouldReturn409Conflict()
    {
        using var client = await CreateAdminClientAsync();
        var created = await CreateItemAsync(client, code: "DOC_PATCHSTALE");

        using var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/platform/document-type-catalogs/{created.Id}")
        {
            Content = PatchBody(new[] { new { op = "replace", path = "/name", value = "x" } })
        };
        patchRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{Guid.NewGuid()}\"");

        var response = await client.SendAsync(patchRequest);
        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task DocumentTypeCatalogs_Patch_WithIsActivePath_ShouldReturn400()
    {
        using var client = await CreateAdminClientAsync();
        var created = await CreateItemAsync(client, code: "DOC_STATUSPATCH");

        // Activation state changes go through /activate and /inactivate; a /isActive patch op must be 400.
        using var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/platform/document-type-catalogs/{created.Id}")
        {
            Content = PatchBody(new[] { new { op = "replace", path = "/isActive", value = false } })
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

    private sealed record DocumentTypeCatalogItemEnvelope(
        Guid Id,
        string Code,
        string Name,
        int SortOrder,
        bool IsActive,
        Guid ConcurrencyToken,
        DateTime CreatedAtUtc,
        DateTime? ModifiedAtUtc);
}
