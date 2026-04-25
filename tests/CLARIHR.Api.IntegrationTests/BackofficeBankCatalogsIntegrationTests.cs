using System.Net;
using System.Net.Http.Json;
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

        var updateResponse = await client.PutJsonAsync($"/api/platform/bank-catalogs/{created.PublicId}", new
        {
            countryCode = "SV",
            code = "BANCO_PRUEBA",
            name = "Banco de Prueba Actualizado",
            alias = "Prueba 2",
            swiftCode = "BPRUSVXX",
            routingCode = "123123123",
            sortOrder = 501,
            concurrencyToken = created.ConcurrencyToken
        });
        updateResponse.EnsureSuccessStatusCode();

        var updated = await updateResponse.Content.ReadFromJsonAsync<BankCatalogItemEnvelope>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Banco de Prueba Actualizado", updated!.Name);
        Assert.Equal("Prueba 2", updated.Alias);
        Assert.Equal("BPRUSVXX", updated.SwiftCode);

        var inactivateResponse = await client.PatchAsJsonAsync(
            $"/api/platform/bank-catalogs/{created.PublicId}/inactivate",
            new { concurrencyToken = updated.ConcurrencyToken });
        inactivateResponse.EnsureSuccessStatusCode();

        var inactivated = await inactivateResponse.Content.ReadFromJsonAsync<BankCatalogItemEnvelope>(JsonOptions);
        Assert.NotNull(inactivated);
        Assert.False(inactivated!.IsActive);

        var activateResponse = await client.PatchAsJsonAsync(
            $"/api/platform/bank-catalogs/{created.PublicId}/activate",
            new { concurrencyToken = inactivated.ConcurrencyToken });
        activateResponse.EnsureSuccessStatusCode();

        var activated = await activateResponse.Content.ReadFromJsonAsync<BankCatalogItemEnvelope>(JsonOptions);
        Assert.NotNull(activated);
        Assert.True(activated!.IsActive);

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
