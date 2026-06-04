using System.Net;
using System.Net.Http.Json;
using System.Text;
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
                CommercialAddonBillingModel.PerActiveEmployee,
                CommercialAddon.MassiveMeasurementUnit,
                1.2m,
                null,
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
            billingModel = CommercialAddonBillingModel.PerActiveEmployee,
            measurementUnit = CommercialAddon.MassiveMeasurementUnit,
            unitPrice = 2.5m,
            minimumQuantity = (int?)null,
            minimumMonthlyFee = 35m,
            moduleKeys = new[]
            {
                CommercialModuleKeys.Users
            },
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
            code = "ADDON-RECRUITING",
            name = "Recruiting ATS",
            description = "Recruiting seat addon",
            type = CommercialAddonType.Specialized,
            billingModel = CommercialAddonBillingModel.PerSeat,
            measurementUnit = "recruiter seat",
            unitPrice = 12.5m,
            minimumQuantity = 2,
            minimumMonthlyFee = (decimal?)null,
            moduleKeys = new[]
            {
                CommercialModuleKeys.JobProfiles,
                CommercialModuleKeys.PersonnelFiles
            },
            periodicity = CommercialAddonPeriodicity.Monthly,
            status = CommercialAddonStatus.Draft
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<CommercialAddonEnvelope>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("ADDON-RECRUITING", created!.Code);
        Assert.Equal(CommercialAddonType.Specialized, created.Type);
        Assert.Equal(CommercialAddonBillingModel.PerSeat, created.BillingModel);
        Assert.Equal("recruiter seat", created.MeasurementUnit);
        Assert.Equal(12.5m, created.UnitPrice);
        Assert.Equal(2, created.MinimumQuantity);
        Assert.Null(created.MinimumMonthlyFee);
        Assert.Equal(CommercialAddonStatus.Draft, created.Status);
        Assert.Equal(2, created.ModuleCount);
        Assert.Equal(
            [CommercialModuleKeys.JobProfiles, CommercialModuleKeys.PersonnelFiles],
            created.ModuleKeys.OrderBy(static key => key).ToArray());

        var getResponse = await client.GetAsync($"/api/platform/commercial-addons/{created.PublicId}");
        getResponse.EnsureSuccessStatusCode();

        var fetched = await getResponse.Content.ReadFromJsonAsync<CommercialAddonEnvelope>(JsonOptions);
        Assert.NotNull(fetched);
        Assert.Equal(created.PublicId, fetched!.PublicId);
        Assert.Equal(created.ModuleKeys.OrderBy(static key => key), fetched.ModuleKeys.OrderBy(static key => key));

        // The create exposes the current concurrency token in the ETag header.
        Assert.Equal($"\"{created.ConcurrencyToken:D}\"", createResponse.Headers.ETag!.Tag);

        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/platform/commercial-addons/{created.PublicId}")
        {
            Content = JsonBody(new
            {
                code = "ADDON-RECRUITING",
                name = "Recruiting ATS Plus",
                description = "Recruiting addon updated",
                type = CommercialAddonType.Specialized,
                billingModel = CommercialAddonBillingModel.PerVolume,
                measurementUnit = "vacante",
                unitPrice = 1.5m,
                minimumQuantity = 10,
                minimumMonthlyFee = (decimal?)null,
                moduleKeys = new[] { CommercialModuleKeys.PersonnelFiles },
                periodicity = CommercialAddonPeriodicity.Annual
            })
        };
        updateRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{created.ConcurrencyToken}\"");
        var updateResponse = await client.SendAsync(updateRequest);
        updateResponse.EnsureSuccessStatusCode();

        var updated = await updateResponse.Content.ReadFromJsonAsync<CommercialAddonEnvelope>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Recruiting ATS Plus", updated!.Name);
        Assert.Equal(CommercialAddonBillingModel.PerVolume, updated.BillingModel);
        Assert.Equal("vacante", updated.MeasurementUnit);
        Assert.Equal(10, updated.MinimumQuantity);
        Assert.Null(updated.MinimumMonthlyFee);
        Assert.Equal(CommercialAddonPeriodicity.Annual, updated.Periodicity);
        Assert.Equal(1, updated.ModuleCount);
        Assert.Equal([CommercialModuleKeys.PersonnelFiles], updated.ModuleKeys.ToArray());
        Assert.NotEqual(created.ConcurrencyToken, updated.ConcurrencyToken);
        Assert.Equal($"\"{updated.ConcurrencyToken:D}\"", updateResponse.Headers.ETag!.Tag);

        using var activateRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/platform/commercial-addons/{created.PublicId}/activate");
        activateRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{updated.ConcurrencyToken}\"");
        var activateResponse = await client.SendAsync(activateRequest);
        activateResponse.EnsureSuccessStatusCode();

        var activated = await activateResponse.Content.ReadFromJsonAsync<CommercialAddonEnvelope>(JsonOptions);
        Assert.NotNull(activated);
        Assert.Equal(CommercialAddonStatus.Active, activated!.Status);
        Assert.Equal($"\"{activated.ConcurrencyToken:D}\"", activateResponse.Headers.ETag!.Tag);

        using var inactivateRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/platform/commercial-addons/{created.PublicId}/inactivate");
        inactivateRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{activated.ConcurrencyToken}\"");
        var inactivateResponse = await client.SendAsync(inactivateRequest);
        inactivateResponse.EnsureSuccessStatusCode();

        var inactivated = await inactivateResponse.Content.ReadFromJsonAsync<CommercialAddonEnvelope>(JsonOptions);
        Assert.NotNull(inactivated);
        Assert.Equal(CommercialAddonStatus.Inactive, inactivated!.Status);

        var filteredResponse = await client.GetAsync("/api/platform/commercial-addons?type=Specialized&billingModel=PerVolume&status=Inactive&q=recruiting&page=1&pageSize=10");
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

    [Fact]
    public async Task CommercialAddons_Update_WithoutIfMatch_ShouldReturn400()
    {
        using var client = await CreateAdminClientAsync();
        var created = await CreateRecruitingAddonAsync(client);

        var response = await client.PutJsonAsync($"/api/platform/commercial-addons/{created.PublicId}", new
        {
            code = "ADDON-RECRUITING",
            name = "Recruiting ATS Plus",
            description = (string?)null,
            type = CommercialAddonType.Specialized,
            billingModel = CommercialAddonBillingModel.PerSeat,
            measurementUnit = "recruiter seat",
            unitPrice = 12.5m,
            minimumQuantity = 2,
            minimumMonthlyFee = (decimal?)null,
            moduleKeys = new[] { CommercialModuleKeys.PersonnelFiles },
            periodicity = CommercialAddonPeriodicity.Monthly
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CommercialAddons_Update_WithStaleIfMatch_ShouldReturn409Conflict()
    {
        using var client = await CreateAdminClientAsync();
        var created = await CreateRecruitingAddonAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/platform/commercial-addons/{created.PublicId}")
        {
            Content = JsonBody(new
            {
                code = "ADDON-RECRUITING",
                name = "Recruiting ATS Plus",
                description = (string?)null,
                type = CommercialAddonType.Specialized,
                billingModel = CommercialAddonBillingModel.PerSeat,
                measurementUnit = "recruiter seat",
                unitPrice = 12.5m,
                minimumQuantity = 2,
                minimumMonthlyFee = (decimal?)null,
                moduleKeys = new[] { CommercialModuleKeys.PersonnelFiles },
                periodicity = CommercialAddonPeriodicity.Monthly
            })
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{Guid.NewGuid()}\"");

        var response = await client.SendAsync(request);
        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        await factory.ResetDatabaseAsync(dbContext =>
            PlatformTestSeed.SeedPlatformOperatorAsync(
                dbContext,
                AdminUserId,
                "platform.addons@clarihr.test",
                "hashed-password",
                PlatformOperatorRole.Admin));

        return factory.CreateClientFor(TestUserContext.PlatformAuthenticatedWithoutTenant(AdminUserId));
    }

    private async Task<CommercialAddonEnvelope> CreateRecruitingAddonAsync(HttpClient client)
    {
        var createResponse = await client.PostJsonAsync("/api/platform/commercial-addons", new
        {
            code = "ADDON-RECRUITING",
            name = "Recruiting ATS",
            description = "Recruiting seat addon",
            type = CommercialAddonType.Specialized,
            billingModel = CommercialAddonBillingModel.PerSeat,
            measurementUnit = "recruiter seat",
            unitPrice = 12.5m,
            minimumQuantity = 2,
            minimumMonthlyFee = (decimal?)null,
            moduleKeys = new[] { CommercialModuleKeys.JobProfiles },
            periodicity = CommercialAddonPeriodicity.Monthly,
            status = CommercialAddonStatus.Draft
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        return (await createResponse.Content.ReadFromJsonAsync<CommercialAddonEnvelope>(JsonOptions))!;
    }

    private static StringContent JsonBody(object body) =>
        new(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

    private static async Task AssertProblemDetailsAsync(HttpResponseMessage response, HttpStatusCode expectedStatus, string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
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
        CommercialAddonBillingModel BillingModel,
        string MeasurementUnit,
        decimal UnitPrice,
        int? MinimumQuantity,
        decimal? MinimumMonthlyFee,
        CommercialAddonPeriodicity Periodicity,
        CommercialAddonStatus Status,
        int ModuleCount,
        DateTime CreatedAtUtc,
        DateTime? ModifiedAtUtc);

    private sealed record CommercialAddonEnvelope(
        Guid PublicId,
        string Code,
        string Name,
        string? Description,
        CommercialAddonType Type,
        CommercialAddonBillingModel BillingModel,
        string MeasurementUnit,
        decimal UnitPrice,
        int? MinimumQuantity,
        decimal? MinimumMonthlyFee,
        CommercialAddonPeriodicity Periodicity,
        CommercialAddonStatus Status,
        int ModuleCount,
        Guid ConcurrencyToken,
        DateTime CreatedAtUtc,
        DateTime? ModifiedAtUtc,
        IReadOnlyCollection<string> ModuleKeys);
}
