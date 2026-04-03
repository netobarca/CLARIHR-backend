using System.Net;
using System.Reflection;
using System.Text.Json;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.Platform;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

public sealed class BackofficeCompanySubscriptionAddonsIntegrationTests(BackofficeIntegrationTestWebApplicationFactory factory)
    : IClassFixture<BackofficeIntegrationTestWebApplicationFactory>
{
    private static readonly Guid AdminUserId = Guid.Parse("90000000-0000-0000-0000-000000000021");
    private static readonly Guid ReadOnlyUserId = Guid.Parse("90000000-0000-0000-0000-000000000022");

    [Fact]
    public async Task CompanySubscriptionAddons_ReadOnlyOperator_ShouldReadButNotWrite()
    {
        Guid addonPublicId = Guid.Empty;

        var scenario = await factory.ResetDatabaseAsync(async dbContext =>
        {
            await PlatformTestSeed.SeedPlatformOperatorAsync(
                dbContext,
                ReadOnlyUserId,
                "platform.addons.readonly@clarihr.test",
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

        var eligibleResponse = await client.GetAsync($"/api/platform/companies/{scenario.TenantId}/subscription/addons/eligible?page=1&pageSize=10");
        await EnsureSuccessAsync(eligibleResponse);

        await using (var eligibleStream = await eligibleResponse.Content.ReadAsStreamAsync())
        using (var eligibleDocument = await JsonDocument.ParseAsync(eligibleStream))
        {
            Assert.Equal(1, eligibleDocument.RootElement.GetProperty("totalCount").GetInt32());
            Assert.Equal(addonPublicId, eligibleDocument.RootElement.GetProperty("items")[0].GetProperty("commercialAddonPublicId").GetGuid());
        }

        var listResponse = await client.GetAsync($"/api/platform/companies/{scenario.TenantId}/subscription/addons?page=1&pageSize=10");
        await EnsureSuccessAsync(listResponse);

        var createResponse = await client.PostJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscription/addon-changes",
            new
            {
                commercialAddonId = addonPublicId,
                action = SubscriptionAddonChangeAction.Activate,
                mode = SubscriptionAddonChangeMode.Immediate,
                reasonCode = SubscriptionAddonChangeReasonCode.CustomerRequest,
                observations = "Read only should not create"
            });

        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
    }

    [Fact]
    public async Task CompanySubscriptionAddons_ImmediateActivation_ShouldPersistStateHistoryAndAudit()
    {
        Guid addonPublicId = Guid.Empty;

        var scenario = await factory.ResetDatabaseAsync(async dbContext =>
        {
            await PlatformTestSeed.SeedPlatformOperatorAsync(
                dbContext,
                AdminUserId,
                "platform.addons.admin@clarihr.test",
                "hashed-password",
                PlatformOperatorRole.Admin);

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

        using var client = factory.CreateClientFor(TestUserContext.PlatformAuthenticatedWithoutTenant(AdminUserId));

        var previewResponse = await client.PostJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscription/addon-changes/preview",
            new
            {
                commercialAddonId = addonPublicId,
                action = SubscriptionAddonChangeAction.Activate,
                mode = SubscriptionAddonChangeMode.Immediate
            });
        await EnsureSuccessAsync(previewResponse);

        await using (var previewStream = await previewResponse.Content.ReadAsStreamAsync())
        using (var previewDocument = await JsonDocument.ParseAsync(previewStream))
        {
            Assert.True(previewDocument.RootElement.GetProperty("isEligible").GetBoolean());
            Assert.Equal("Inactive", previewDocument.RootElement.GetProperty("currentStatus").GetString());
            Assert.Equal("Active", previewDocument.RootElement.GetProperty("resultingStatus").GetString());
            Assert.Equal(20m, previewDocument.RootElement.GetProperty("estimatedNextChargeImpact").GetDecimal());
        }

        var createResponse = await client.PostJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscription/addon-changes",
            new
            {
                commercialAddonId = addonPublicId,
                action = SubscriptionAddonChangeAction.Activate,
                mode = SubscriptionAddonChangeMode.Immediate,
                reasonCode = SubscriptionAddonChangeReasonCode.CustomerRequest,
                observations = "Activacion inmediata"
            });
        await EnsureSuccessAsync(createResponse);

        Guid addonChangePublicId;
        await using (var createStream = await createResponse.Content.ReadAsStreamAsync())
        using (var createDocument = await JsonDocument.ParseAsync(createStream))
        {
            addonChangePublicId = createDocument.RootElement.GetProperty("addonChangePublicId").GetGuid();
            Assert.Equal("Applied", createDocument.RootElement.GetProperty("status").GetString());
            Assert.Equal("Active", createDocument.RootElement.GetProperty("resultingStatus").GetString());
        }

        var addonsResponse = await client.GetAsync($"/api/platform/companies/{scenario.TenantId}/subscription/addons?page=1&pageSize=10");
        await EnsureSuccessAsync(addonsResponse);

        await using (var addonsStream = await addonsResponse.Content.ReadAsStreamAsync())
        using (var addonsDocument = await JsonDocument.ParseAsync(addonsStream))
        {
            Assert.Equal(1, addonsDocument.RootElement.GetProperty("totalCount").GetInt32());
            Assert.Equal("Active", addonsDocument.RootElement.GetProperty("items")[0].GetProperty("status").GetString());
        }

        var changesResponse = await client.GetAsync($"/api/platform/companies/{scenario.TenantId}/subscription/addon-changes?page=1&pageSize=10");
        await EnsureSuccessAsync(changesResponse);

        await using (var changesStream = await changesResponse.Content.ReadAsStreamAsync())
        using (var changesDocument = await JsonDocument.ParseAsync(changesStream))
        {
            Assert.Equal(addonChangePublicId, changesDocument.RootElement.GetProperty("items")[0].GetProperty("addonChangePublicId").GetGuid());
        }

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await dbContext.PlatformAuditLogs.AnyAsync(log =>
            log.EntityId == addonChangePublicId &&
            log.EventType == AuditEventTypes.CompanySubscriptionAddonChangeApplied));
    }

    [Fact]
    public async Task CompanySubscriptionAddons_ScheduledActivation_ShouldAllowCancellation()
    {
        Guid addonPublicId = Guid.Empty;

        var scenario = await factory.ResetDatabaseAsync(async dbContext =>
        {
            await PlatformTestSeed.SeedPlatformOperatorAsync(
                dbContext,
                AdminUserId,
                "platform.addons.admin@clarihr.test",
                "hashed-password",
                PlatformOperatorRole.Admin);

            var addon = CommercialAddon.Create(
                "ADDON-RECRUITING",
                "Recruiting",
                "Recruiting addon",
                CommercialAddonType.Specialized,
                CommercialAddonBillingModel.PerSeat,
                "recruiter seat",
                12.5m,
                2,
                null,
                CommercialAddonPeriodicity.Monthly,
                CommercialAddonStatus.Active);
            dbContext.CommercialAddons.Add(addon);
            await dbContext.SaveChangesAsync();
            addonPublicId = addon.PublicId;
        });

        using var client = factory.CreateClientFor(TestUserContext.PlatformAuthenticatedWithoutTenant(AdminUserId));
        var effectiveDate = DateTime.UtcNow.Date.AddDays(7);

        var createResponse = await client.PostJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscription/addon-changes",
            new
            {
                commercialAddonId = addonPublicId,
                action = SubscriptionAddonChangeAction.Activate,
                mode = SubscriptionAddonChangeMode.SpecificDate,
                requestedEffectiveDateUtc = effectiveDate,
                reasonCode = SubscriptionAddonChangeReasonCode.CommercialTrial,
                observations = "Activacion programada"
            });
        await EnsureSuccessAsync(createResponse);

        Guid addonChangePublicId;
        await using (var createStream = await createResponse.Content.ReadAsStreamAsync())
        using (var createDocument = await JsonDocument.ParseAsync(createStream))
        {
            addonChangePublicId = createDocument.RootElement.GetProperty("addonChangePublicId").GetGuid();
            Assert.Equal("Scheduled", createDocument.RootElement.GetProperty("status").GetString());
            Assert.Equal("PendingActivation", createDocument.RootElement.GetProperty("resultingStatus").GetString());
        }

        var cancelResponse = await client.PostJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscription/addon-changes/{addonChangePublicId}/cancel",
            new { observations = "Cambio de decision" });
        await EnsureSuccessAsync(cancelResponse);

        await using (var cancelStream = await cancelResponse.Content.ReadAsStreamAsync())
        using (var cancelDocument = await JsonDocument.ParseAsync(cancelStream))
        {
            Assert.Equal("Cancelled", cancelDocument.RootElement.GetProperty("status").GetString());
            Assert.Equal("Cambio de decision", cancelDocument.RootElement.GetProperty("cancellationObservations").GetString());
        }

        var addonsResponse = await client.GetAsync($"/api/platform/companies/{scenario.TenantId}/subscription/addons?page=1&pageSize=10");
        await EnsureSuccessAsync(addonsResponse);

        await using (var addonsStream = await addonsResponse.Content.ReadAsStreamAsync())
        using (var addonsDocument = await JsonDocument.ParseAsync(addonsStream))
        {
            Assert.Equal("Inactive", addonsDocument.RootElement.GetProperty("items")[0].GetProperty("status").GetString());
        }
    }

    [Fact]
    public async Task CompanySubscriptionAddons_Processor_ShouldApplyScheduledActivationAndDeactivation()
    {
        Guid addonPublicId = Guid.Empty;

        var scenario = await factory.ResetDatabaseAsync(async dbContext =>
        {
            await PlatformTestSeed.SeedPlatformOperatorAsync(
                dbContext,
                AdminUserId,
                "platform.addons.admin@clarihr.test",
                "hashed-password",
                PlatformOperatorRole.Admin);

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

        using var client = factory.CreateClientFor(TestUserContext.PlatformAuthenticatedWithoutTenant(AdminUserId));
        var futureDate = DateTime.UtcNow.Date.AddDays(2);

        var activationResponse = await client.PostJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscription/addon-changes",
            new
            {
                commercialAddonId = addonPublicId,
                action = SubscriptionAddonChangeAction.Activate,
                mode = SubscriptionAddonChangeMode.SpecificDate,
                requestedEffectiveDateUtc = futureDate,
                reasonCode = SubscriptionAddonChangeReasonCode.CustomerRequest,
                observations = "Activar por processor"
            });
        await EnsureSuccessAsync(activationResponse);

        Guid activationChangePublicId;
        await using (var activationStream = await activationResponse.Content.ReadAsStreamAsync())
        using (var activationDocument = await JsonDocument.ParseAsync(activationStream))
        {
            activationChangePublicId = activationDocument.RootElement.GetProperty("addonChangePublicId").GetGuid();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            _ = await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE company_commercial_addon_changes SET effective_date_utc = {DateTime.UtcNow.Date} WHERE public_id = {activationChangePublicId}");
        }

        await InvokeAddonChangeProcessorAsync();

        var afterActivationResponse = await client.GetAsync($"/api/platform/companies/{scenario.TenantId}/subscription/addons?page=1&pageSize=10");
        await EnsureSuccessAsync(afterActivationResponse);

        await using (var afterActivationStream = await afterActivationResponse.Content.ReadAsStreamAsync())
        using (var afterActivationDocument = await JsonDocument.ParseAsync(afterActivationStream))
        {
            Assert.Equal("Active", afterActivationDocument.RootElement.GetProperty("items")[0].GetProperty("status").GetString());
        }

        var deactivationResponse = await client.PostJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscription/addon-changes",
            new
            {
                commercialAddonId = addonPublicId,
                action = SubscriptionAddonChangeAction.Deactivate,
                mode = SubscriptionAddonChangeMode.SpecificDate,
                requestedEffectiveDateUtc = DateTime.UtcNow.Date.AddDays(3),
                reasonCode = SubscriptionAddonChangeReasonCode.CostReduction,
                observations = "Desactivar por processor"
            });
        await EnsureSuccessAsync(deactivationResponse);

        Guid deactivationChangePublicId;
        await using (var deactivationStream = await deactivationResponse.Content.ReadAsStreamAsync())
        using (var deactivationDocument = await JsonDocument.ParseAsync(deactivationStream))
        {
            deactivationChangePublicId = deactivationDocument.RootElement.GetProperty("addonChangePublicId").GetGuid();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            _ = await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE company_commercial_addon_changes SET effective_date_utc = {DateTime.UtcNow.Date} WHERE public_id = {deactivationChangePublicId}");
        }

        await InvokeAddonChangeProcessorAsync();

        var finalResponse = await client.GetAsync($"/api/platform/companies/{scenario.TenantId}/subscription/addons?page=1&pageSize=10");
        await EnsureSuccessAsync(finalResponse);

        await using (var finalStream = await finalResponse.Content.ReadAsStreamAsync())
        using (var finalDocument = await JsonDocument.ParseAsync(finalStream))
        {
            Assert.Equal("Inactive", finalDocument.RootElement.GetProperty("items")[0].GetProperty("status").GetString());
        }
    }

    [Fact]
    public async Task CompanySubscriptionAddons_ScheduledConflict_ShouldReturnConflict()
    {
        Guid addonPublicId = Guid.Empty;

        var scenario = await factory.ResetDatabaseAsync(async dbContext =>
        {
            await PlatformTestSeed.SeedPlatformOperatorAsync(
                dbContext,
                AdminUserId,
                "platform.addons.admin@clarihr.test",
                "hashed-password",
                PlatformOperatorRole.Admin);

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

        using var client = factory.CreateClientFor(TestUserContext.PlatformAuthenticatedWithoutTenant(AdminUserId));

        var firstResponse = await client.PostJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscription/addon-changes",
            new
            {
                commercialAddonId = addonPublicId,
                action = SubscriptionAddonChangeAction.Activate,
                mode = SubscriptionAddonChangeMode.SpecificDate,
                requestedEffectiveDateUtc = DateTime.UtcNow.Date.AddDays(4),
                reasonCode = SubscriptionAddonChangeReasonCode.CustomerRequest,
                observations = "Primer cambio programado"
            });
        await EnsureSuccessAsync(firstResponse);

        var secondResponse = await client.PostJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscription/addon-changes",
            new
            {
                commercialAddonId = addonPublicId,
                action = SubscriptionAddonChangeAction.Activate,
                mode = SubscriptionAddonChangeMode.SpecificDate,
                requestedEffectiveDateUtc = DateTime.UtcNow.Date.AddDays(5),
                reasonCode = SubscriptionAddonChangeReasonCode.CustomerRequest,
                observations = "Cambio conflictivo"
            });

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);

        await using var stream = await secondResponse.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        Assert.Equal("PLATFORM_COMPANY_SUBSCRIPTION_ADDON_PENDING_CONFLICT", document.RootElement.GetProperty("code").GetString());
    }

    private async Task InvokeAddonChangeProcessorAsync()
    {
        using var scope = factory.Services.CreateScope();
        var processorType = typeof(ApplicationDbContext).Assembly.GetType(
            "CLARIHR.Infrastructure.Companies.CompanySubscriptionLifecycleProcessor",
            throwOnError: true)!;
        var processor = scope.ServiceProvider.GetRequiredService(processorType);
        var applyMethod = processorType.GetMethod(
            "ApplyDueScheduledAddonChangesAsync",
            BindingFlags.Instance | BindingFlags.Public)!;

        var invocation = applyMethod.Invoke(processor, [CancellationToken.None]);
        var task = Assert.IsAssignableFrom<Task<int>>(invocation);
        var processedCount = await task;

        Assert.True(processedCount >= 1);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected success status code but received {(int)response.StatusCode}: {body}");
    }
}
