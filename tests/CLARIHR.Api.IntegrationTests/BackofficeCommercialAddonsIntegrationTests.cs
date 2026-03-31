using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.Platform;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

public sealed class BackofficeCommercialAddonsIntegrationTests(BackofficeIntegrationTestWebApplicationFactory factory)
    : IClassFixture<BackofficeIntegrationTestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = IntegrationTestJson.CreateOptions();
    private static readonly Guid AdminUserId = Guid.Parse("90000000-0000-0000-0000-000000000011");
    private static readonly Guid ReadOnlyUserId = Guid.Parse("90000000-0000-0000-0000-000000000012");

    [Fact]
    public async Task CommercialAddons_Search_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/platform/commercial-addons");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CommercialAddons_Search_WithCoreClientType_ShouldReturnForbidden()
    {
        await factory.ResetDatabaseAsync(dbContext =>
            PlatformTestSeed.SeedPlatformOperatorAsync(
                dbContext,
                AdminUserId,
                "platform.addons@clarihr.test",
                "hashed-password"));
        using var client = factory.CreateClientFor(TestUserContext.AuthenticatedWithoutTenant(AdminUserId));

        var response = await client.GetAsync("/api/platform/commercial-addons");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CommercialAddons_ReadOnlyOperator_ShouldReadButNotWrite()
    {
        Guid addonPublicId = Guid.Empty;

        await factory.ResetDatabaseAsync(async dbContext =>
        {
            await PlatformTestSeed.SeedPlatformOperatorAsync(
                dbContext,
                ReadOnlyUserId,
                "platform.readonly@clarihr.test",
                "hashed-password",
                PlatformOperatorRole.ReadOnly);

            var addon = CommercialAddon.Create(
                "ADDON-ATTENDANCE",
                "Attendance",
                "Attendance addon",
                CommercialAddonType.Massive,
                1.2m,
                20m,
                CommercialAddonPeriodicity.Monthly,
                CommercialAddonStatus.Active);
            dbContext.CommercialAddons.Add(addon);
            await dbContext.SaveChangesAsync();
            addonPublicId = addon.PublicId;
        });

        using var client = factory.CreateClientFor(TestUserContext.PlatformAuthenticatedWithoutTenant(ReadOnlyUserId));

        var listResponse = await client.GetAsync("/api/platform/commercial-addons?status=Active&page=1&pageSize=20");
        listResponse.EnsureSuccessStatusCode();

        var listPayload = await listResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<CommercialAddonSummaryEnvelope>>(JsonOptions);
        Assert.NotNull(listPayload);
        Assert.Contains(listPayload!.Items, item => item.PublicId == addonPublicId);

        var detailResponse = await client.GetAsync($"/api/platform/commercial-addons/{addonPublicId}");
        detailResponse.EnsureSuccessStatusCode();

        var detailPayload = await detailResponse.Content.ReadFromJsonAsync<CommercialAddonEnvelope>(JsonOptions);
        Assert.NotNull(detailPayload);
        Assert.Equal(addonPublicId, detailPayload!.PublicId);

        var createResponse = await client.PostJsonAsync("/api/platform/commercial-addons", new
        {
            code = "ADDON-PAYROLL-ES",
            name = "Payroll ES",
            description = "Payroll addon",
            type = CommercialAddonType.Massive,
            pricePerActiveEmployee = 2.5m,
            minimumMonthlyFee = 35m,
            periodicity = CommercialAddonPeriodicity.Monthly,
            status = CommercialAddonStatus.Draft
        });

        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
    }

    [Fact]
    public async Task CommercialAddons_CrudLifecycle_WithPlatformAdmin_ShouldSucceed_AndPersistAudit()
    {
        await factory.ResetDatabaseAsync(dbContext =>
            PlatformTestSeed.SeedPlatformOperatorAsync(
                dbContext,
                AdminUserId,
                "platform.addons@clarihr.test",
                "hashed-password",
                PlatformOperatorRole.Admin));
        using var client = factory.CreateClientFor(TestUserContext.PlatformAuthenticatedWithoutTenant(AdminUserId));

        var createResponse = await client.PostJsonAsync("/api/platform/commercial-addons", new
        {
            code = "ADDON-ATTENDANCE",
            name = "Attendance",
            description = "Attendance addon",
            type = CommercialAddonType.Massive,
            pricePerActiveEmployee = 1.2m,
            minimumMonthlyFee = 20m,
            periodicity = CommercialAddonPeriodicity.Monthly,
            status = CommercialAddonStatus.Draft
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<CommercialAddonEnvelope>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("ADDON-ATTENDANCE", created!.Code);
        Assert.Equal(CommercialAddonType.Massive, created.Type);
        Assert.Equal(20m, created.MinimumMonthlyFee);
        Assert.Equal(CommercialAddonStatus.Draft, created.Status);

        var getResponse = await client.GetAsync($"/api/platform/commercial-addons/{created.PublicId}");
        getResponse.EnsureSuccessStatusCode();

        var fetched = await getResponse.Content.ReadFromJsonAsync<CommercialAddonEnvelope>(JsonOptions);
        Assert.NotNull(fetched);
        Assert.Equal(created.PublicId, fetched!.PublicId);

        var updateResponse = await client.PutJsonAsync($"/api/platform/commercial-addons/{created.PublicId}", new
        {
            code = "ADDON-ATTENDANCE",
            name = "Attendance Plus",
            description = "Attendance addon updated",
            type = CommercialAddonType.Massive,
            pricePerActiveEmployee = 1.5m,
            minimumMonthlyFee = (decimal?)null,
            periodicity = CommercialAddonPeriodicity.Annual,
            concurrencyToken = created.ConcurrencyToken
        });
        updateResponse.EnsureSuccessStatusCode();

        var updated = await updateResponse.Content.ReadFromJsonAsync<CommercialAddonEnvelope>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Attendance Plus", updated!.Name);
        Assert.Null(updated.MinimumMonthlyFee);
        Assert.Equal(CommercialAddonPeriodicity.Annual, updated.Periodicity);

        var activateResponse = await client.PatchAsJsonAsync(
            $"/api/platform/commercial-addons/{created.PublicId}/activate",
            new { concurrencyToken = updated.ConcurrencyToken });
        activateResponse.EnsureSuccessStatusCode();

        var activated = await activateResponse.Content.ReadFromJsonAsync<CommercialAddonEnvelope>(JsonOptions);
        Assert.NotNull(activated);
        Assert.Equal(CommercialAddonStatus.Active, activated!.Status);

        var inactivateResponse = await client.PatchAsJsonAsync(
            $"/api/platform/commercial-addons/{created.PublicId}/inactivate",
            new { concurrencyToken = activated.ConcurrencyToken });
        inactivateResponse.EnsureSuccessStatusCode();

        var inactivated = await inactivateResponse.Content.ReadFromJsonAsync<CommercialAddonEnvelope>(JsonOptions);
        Assert.NotNull(inactivated);
        Assert.Equal(CommercialAddonStatus.Inactive, inactivated!.Status);

        var filteredResponse = await client.GetAsync("/api/platform/commercial-addons?status=Inactive&q=attendance&page=1&pageSize=10");
        filteredResponse.EnsureSuccessStatusCode();

        var filtered = await filteredResponse.Content.ReadFromJsonAsync<PagedResponseEnvelope<CommercialAddonSummaryEnvelope>>(JsonOptions);
        Assert.NotNull(filtered);
        Assert.Contains(filtered!.Items, item => item.PublicId == created.PublicId);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await dbContext.CommercialAddons.AnyAsync(addon => addon.PublicId == created.PublicId));
        Assert.True(await dbContext.PlatformAuditLogs.AnyAsync(log =>
            log.EntityId == created.PublicId &&
            log.EventType == AuditEventTypes.CommercialAddonInactivated));
    }

    private sealed record PagedResponseEnvelope<TItem>(
        IReadOnlyCollection<TItem> Items,
        int PageNumber,
        int PageSize,
        int TotalCount);

    private sealed record CommercialAddonSummaryEnvelope(
        Guid PublicId,
        string Code,
        string Name,
        string? Description,
        CommercialAddonType Type,
        decimal PricePerActiveEmployee,
        decimal? MinimumMonthlyFee,
        CommercialAddonPeriodicity Periodicity,
        CommercialAddonStatus Status,
        DateTime CreatedAtUtc,
        DateTime? ModifiedAtUtc);

    private sealed record CommercialAddonEnvelope(
        Guid PublicId,
        string Code,
        string Name,
        string? Description,
        CommercialAddonType Type,
        decimal PricePerActiveEmployee,
        decimal? MinimumMonthlyFee,
        CommercialAddonPeriodicity Periodicity,
        CommercialAddonStatus Status,
        Guid ConcurrencyToken,
        DateTime CreatedAtUtc,
        DateTime? ModifiedAtUtc);
}
