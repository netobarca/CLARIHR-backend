using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Domain.Platform;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

public sealed class BackofficeBankCatalogsIntegrationTests(BackofficeIntegrationTestWebApplicationFactory factory)
    : IClassFixture<BackofficeIntegrationTestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = IntegrationTestJson.CreateOptions();
    private static readonly Guid AdminUserId = Guid.Parse("90000000-0000-0000-0000-000000000041");

    [Fact]
    public async Task BankCatalogs_CrudLifecycle_WithPlatformAdmin_ShouldSucceed_AndPersistAudit()
    {
        await factory.ResetDatabaseAsync(dbContext =>
            PlatformTestSeed.SeedPlatformOperatorAsync(
                dbContext,
                AdminUserId,
                "platform.banks@clarihr.test",
                "hashed-password",
                PlatformOperatorRole.Admin));

        using var client = factory.CreateClientFor(TestUserContext.PlatformAuthenticatedWithoutTenant(AdminUserId));

        var searchResponse = await client.GetAsync("/api/platform/bank-catalogs?countryCode=SV&page=1&pageSize=20");
        searchResponse.EnsureSuccessStatusCode();

        var searchPayload = await searchResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<BankCatalogItemEnvelope>>(JsonOptions);
        Assert.NotNull(searchPayload);
        Assert.NotEmpty(searchPayload!.Items);

        var createResponse = await client.PostJsonAsync("/api/platform/bank-catalogs", new
        {
            countryCode = "SV",
            code = "BANCO_PRUEBA",
            name = "Banco de Prueba",
            alias = "Prueba",
            swiftCode = "BPRUSVSS",
            routingCode = "987654321",
            sortOrder = 500
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<BankCatalogItemEnvelope>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("BANCO_PRUEBA", created!.Code);
        Assert.Equal("Banco de Prueba", created.Name);
        Assert.Equal("Prueba", created.Alias);
        Assert.Equal("BPRUSVSS", created.SwiftCode);
        Assert.Equal("987654321", created.RoutingCode);
        Assert.True(created.IsActive);

        // The create exposes the current concurrency token in the ETag header.
        Assert.Equal($"\"{created.ConcurrencyToken:D}\"", createResponse.Headers.ETag!.Tag);

        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/platform/bank-catalogs/{created.PublicId}")
        {
            Content = JsonBody(new
            {
                countryCode = "SV",
                code = "BANCO_PRUEBA",
                name = "Banco de Prueba Actualizado",
                alias = "Prueba 2",
                swiftCode = "BPRUSVXX",
                routingCode = "123123123",
                sortOrder = 501
            })
        };
        updateRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{created.ConcurrencyToken}\"");
        var updateResponse = await client.SendAsync(updateRequest);
        updateResponse.EnsureSuccessStatusCode();

        var updated = await updateResponse.Content.ReadFromJsonAsync<BankCatalogItemEnvelope>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Banco de Prueba Actualizado", updated!.Name);
        Assert.Equal("Prueba 2", updated.Alias);
        Assert.Equal("BPRUSVXX", updated.SwiftCode);
        Assert.NotEqual(created.ConcurrencyToken, updated.ConcurrencyToken);
        Assert.Equal($"\"{updated.ConcurrencyToken:D}\"", updateResponse.Headers.ETag!.Tag);

        // PATCH (RFC-6902): rename + clear the optional swift code.
        using var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/platform/bank-catalogs/{created.PublicId}")
        {
            Content = PatchBody(new object[]
            {
                new { op = "replace", path = "/name", value = "Banco Patcheado" },
                new { op = "remove", path = "/swiftCode" }
            })
        };
        patchRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{updated.ConcurrencyToken}\"");
        var patchResponse = await client.SendAsync(patchRequest);
        patchResponse.EnsureSuccessStatusCode();

        var patched = await patchResponse.Content.ReadFromJsonAsync<BankCatalogItemEnvelope>(JsonOptions);
        Assert.NotNull(patched);
        Assert.Equal("Banco Patcheado", patched!.Name);
        Assert.Null(patched.SwiftCode);
        Assert.NotEqual(updated.ConcurrencyToken, patched.ConcurrencyToken);
        Assert.Equal($"\"{patched.ConcurrencyToken:D}\"", patchResponse.Headers.ETag!.Tag);

        using var inactivateRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/platform/bank-catalogs/{created.PublicId}/inactivate");
        inactivateRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{patched.ConcurrencyToken}\"");
        var inactivateResponse = await client.SendAsync(inactivateRequest);
        inactivateResponse.EnsureSuccessStatusCode();

        var inactivated = await inactivateResponse.Content.ReadFromJsonAsync<BankCatalogItemEnvelope>(JsonOptions);
        Assert.NotNull(inactivated);
        Assert.False(inactivated!.IsActive);

        using var activateRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/platform/bank-catalogs/{created.PublicId}/activate");
        activateRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{inactivated.ConcurrencyToken}\"");
        var activateResponse = await client.SendAsync(activateRequest);
        activateResponse.EnsureSuccessStatusCode();

        var activated = await activateResponse.Content.ReadFromJsonAsync<BankCatalogItemEnvelope>(JsonOptions);
        Assert.NotNull(activated);
        Assert.True(activated!.IsActive);
        Assert.Equal($"\"{activated.ConcurrencyToken:D}\"", activateResponse.Headers.ETag!.Tag);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var auditEvents = await dbContext.PlatformAuditLogs
            .AsNoTracking()
            .Where(item => item.EntityId == created.PublicId)
            .Select(item => item.EventType)
            .ToListAsync();

        Assert.Contains(AuditEventTypes.BankCatalogItemCreated, auditEvents);
        Assert.Contains(AuditEventTypes.BankCatalogItemUpdated, auditEvents);
        Assert.Contains(AuditEventTypes.BankCatalogItemInactivated, auditEvents);
        Assert.Contains(AuditEventTypes.BankCatalogItemActivated, auditEvents);
    }

    [Fact]
    public async Task BankCatalogs_Update_WithoutIfMatch_ShouldReturn400()
    {
        using var client = await CreateAdminClientAsync();
        var created = await CreateBankAsync(client, "BANCO_NOIFMATCH");

        var response = await client.PutJsonAsync($"/api/platform/bank-catalogs/{created.PublicId}", new
        {
            countryCode = "SV",
            code = "BANCO_NOIFMATCH",
            name = "Sin If-Match",
            alias = (string?)null,
            swiftCode = (string?)null,
            routingCode = (string?)null,
            sortOrder = 10
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BankCatalogs_Patch_WithStaleIfMatch_ShouldReturn409Conflict()
    {
        using var client = await CreateAdminClientAsync();
        var created = await CreateBankAsync(client, "BANCO_STALE");

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/platform/bank-catalogs/{created.PublicId}")
        {
            Content = PatchBody(new[] { new { op = "replace", path = "/name", value = "x" } })
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{Guid.NewGuid()}\"");

        var response = await client.SendAsync(request);
        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task BankCatalogs_Patch_WithCountryCodePath_ShouldReturn400()
    {
        using var client = await CreateAdminClientAsync();
        var created = await CreateBankAsync(client, "BANCO_COUNTRYPATCH");

        // The country is the immutable catalog scope; a /countryCode patch op must be rejected (400).
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/platform/bank-catalogs/{created.PublicId}")
        {
            Content = PatchBody(new[] { new { op = "replace", path = "/countryCode", value = "US" } })
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{created.ConcurrencyToken}\"");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        await factory.ResetDatabaseAsync(dbContext =>
            PlatformTestSeed.SeedPlatformOperatorAsync(
                dbContext,
                AdminUserId,
                "platform.banks@clarihr.test",
                "hashed-password",
                PlatformOperatorRole.Admin));

        return factory.CreateClientFor(TestUserContext.PlatformAuthenticatedWithoutTenant(AdminUserId));
    }

    private async Task<BankCatalogItemEnvelope> CreateBankAsync(HttpClient client, string code)
    {
        var createResponse = await client.PostJsonAsync("/api/platform/bank-catalogs", new
        {
            countryCode = "SV",
            code,
            name = "Banco de Prueba",
            alias = "Prueba",
            swiftCode = "BPRUSVSS",
            routingCode = "987654321",
            sortOrder = 500
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        return (await createResponse.Content.ReadFromJsonAsync<BankCatalogItemEnvelope>(JsonOptions))!;
    }

    private static StringContent JsonBody(object body) =>
        new(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

    private static StringContent PatchBody(object operations) =>
        new(JsonSerializer.Serialize(operations, JsonOptions), Encoding.UTF8, "application/json-patch+json");

    private static async Task AssertProblemDetailsAsync(HttpResponseMessage response, HttpStatusCode expectedStatus, string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
    }

    private sealed record BankCatalogItemEnvelope(
        Guid PublicId,
        string CountryCode,
        string Code,
        string Name,
        string? Alias,
        string? SwiftCode,
        string? RoutingCode,
        bool IsActive,
        int SortOrder,
        Guid ConcurrencyToken);

    private sealed record PagedResponseEnvelope<TItem>(
        IReadOnlyCollection<TItem> Items,
        int PageNumber,
        int PageSize,
        int TotalCount);
}
