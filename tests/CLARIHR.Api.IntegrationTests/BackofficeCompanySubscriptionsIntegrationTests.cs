using System.Net;
using System.Reflection;
using System.Text.Json;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.Platform;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

public sealed class BackofficeCompanySubscriptionsIntegrationTests(BackofficeIntegrationTestWebApplicationFactory factory)
    : IClassFixture<BackofficeIntegrationTestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = IntegrationTestJson.CreateOptions();
    private static readonly Guid PlatformOperatorUserId = Guid.Parse("90000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task CompanySubscriptions_ActivateImmediate_ShouldUpdateActiveSubscriptionKeepHistoryAndAudit()
    {
        Guid proPlanPublicId = Guid.Empty;

        var scenario = await factory.ResetDatabaseAsync(async dbContext =>
        {
            await PlatformTestSeed.SeedPlatformOperatorAsync(
                dbContext,
                PlatformOperatorUserId,
                "platform.subscription@clarihr.test",
                "hashed-password",
                PlatformOperatorRole.Admin);

            var proPlan = CommercialPlan.Create(
                "PRO",
                "Professional",
                "Plan profesional",
                150m,
                4m,
                CommercialPlanStatus.Active,
                isSystemPlan: false,
                []);
            dbContext.CommercialPlans.Add(proPlan);
            await dbContext.SaveChangesAsync();
            proPlanPublicId = proPlan.PublicId;
        });

        using var client = factory.CreateClientFor(TestUserContext.PlatformAuthenticatedWithoutTenant(PlatformOperatorUserId));

        var currentResponse = await client.GetAsync($"/api/platform/companies/{scenario.TenantId}/subscription");
        await EnsureSuccessAsync(currentResponse);

        await using (var currentStream = await currentResponse.Content.ReadAsStreamAsync())
        using (var currentDocument = await JsonDocument.ParseAsync(currentStream))
        {
            Assert.Equal("FREE", currentDocument.RootElement.GetProperty("currentSubscription").GetProperty("planCode").GetString());
        }

        var replaceResponse = await client.PutJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscription",
            new
            {
                commercialPlanId = proPlanPublicId,
                startDateUtc = DateTime.UtcNow.Date,
                periodicity = CompanySubscriptionPeriodicity.Monthly
            });
        await EnsureSuccessAsync(replaceResponse);

        await using (var replaceStream = await replaceResponse.Content.ReadAsStreamAsync())
        using (var replaceDocument = await JsonDocument.ParseAsync(replaceStream))
        {
            Assert.Equal("PRO", replaceDocument.RootElement.GetProperty("planCode").GetString());
            Assert.Equal("Active", replaceDocument.RootElement.GetProperty("status").GetString());
        }

        var historyResponse = await client.GetAsync($"/api/platform/companies/{scenario.TenantId}/subscriptions?page=1&pageSize=10");
        await EnsureSuccessAsync(historyResponse);

        await using (var historyStream = await historyResponse.Content.ReadAsStreamAsync())
        using (var historyDocument = await JsonDocument.ParseAsync(historyStream))
        {
            Assert.Equal(2, historyDocument.RootElement.GetProperty("totalCount").GetInt32());
        }

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var companyId = await dbContext.Companies
            .Where(company => company.PublicId == scenario.TenantId)
            .Select(company => company.Id)
            .SingleAsync();

        var subscriptions = await dbContext.CompanySubscriptions
            .Where(subscription => subscription.CompanyId == companyId)
            .OrderBy(subscription => subscription.CreatedUtc)
            .ToListAsync();

        Assert.Equal(2, subscriptions.Count);
        Assert.Single(subscriptions, subscription => subscription.Status == SubscriptionStatus.Active && subscription.PlanCode == "PRO");
        Assert.Single(subscriptions, subscription => subscription.Status == SubscriptionStatus.Cancelled && subscription.PlanCode == "FREE");
        Assert.True(await dbContext.PlatformAuditLogs.AnyAsync(log => log.EventType == "COMPANY_SUBSCRIPTION_ACTIVATED"));
        Assert.True(await dbContext.Companies.AnyAsync(company => company.Id == companyId && company.IsBillable));
    }

    [Fact]
    public async Task CompanySubscriptions_ActivateImmediate_WithInactivePlan_ShouldReturnConflict()
    {
        Guid inactivePlanPublicId = Guid.Empty;

        var scenario = await factory.ResetDatabaseAsync(async dbContext =>
        {
            await PlatformTestSeed.SeedPlatformOperatorAsync(
                dbContext,
                PlatformOperatorUserId,
                "platform.subscription@clarihr.test",
                "hashed-password",
                PlatformOperatorRole.Admin);

            var inactivePlan = CommercialPlan.Create(
                "INACTIVE",
                "Inactive",
                "Plan inactivo",
                10m,
                1m,
                CommercialPlanStatus.Inactive,
                isSystemPlan: false,
                []);
            dbContext.CommercialPlans.Add(inactivePlan);
            await dbContext.SaveChangesAsync();
            inactivePlanPublicId = inactivePlan.PublicId;
        });

        using var client = factory.CreateClientFor(TestUserContext.PlatformAuthenticatedWithoutTenant(PlatformOperatorUserId));

        var response = await client.PutJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscription",
            new
            {
                commercialPlanId = inactivePlanPublicId,
                startDateUtc = DateTime.UtcNow.Date,
                periodicity = CompanySubscriptionPeriodicity.Monthly
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        Assert.Equal("PLATFORM_COMPANY_SUBSCRIPTION_PLAN_INACTIVE", document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task CompanySubscriptions_ActivateScheduled_ShouldExposeScheduledReplacementAndPreview()
    {
        Guid proPlanPublicId = Guid.Empty;

        var scenario = await factory.ResetDatabaseAsync(async dbContext =>
        {
            await PlatformTestSeed.SeedPlatformOperatorAsync(
                dbContext,
                PlatformOperatorUserId,
                "platform.subscription@clarihr.test",
                "hashed-password",
                PlatformOperatorRole.Admin);

            var proPlan = CommercialPlan.Create(
                "PRO",
                "Professional",
                "Plan profesional",
                150m,
                4m,
                CommercialPlanStatus.Active,
                isSystemPlan: false,
                []);
            dbContext.CommercialPlans.Add(proPlan);
            await dbContext.SaveChangesAsync();
            proPlanPublicId = proPlan.PublicId;
        });

        using var client = factory.CreateClientFor(TestUserContext.PlatformAuthenticatedWithoutTenant(PlatformOperatorUserId));
        var futureDate = DateTime.UtcNow.Date.AddDays(7);

        var previewResponse = await client.PostJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscription/preview",
            new
            {
                commercialPlanId = proPlanPublicId,
                startDateUtc = futureDate,
                periodicity = CompanySubscriptionPeriodicity.Annual
            });
        await EnsureSuccessAsync(previewResponse);

        await using (var previewStream = await previewResponse.Content.ReadAsStreamAsync())
        using (var previewDocument = await JsonDocument.ParseAsync(previewStream))
        {
            Assert.True(previewDocument.RootElement.GetProperty("isEligible").GetBoolean());
            Assert.Equal("Scheduled", previewDocument.RootElement.GetProperty("resolvedStatus").GetString());
            Assert.Equal("PRO", previewDocument.RootElement.GetProperty("planCode").GetString());
        }

        var activateResponse = await client.PutJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscription",
            new
            {
                commercialPlanId = proPlanPublicId,
                startDateUtc = futureDate,
                periodicity = CompanySubscriptionPeriodicity.Annual
            });
        await EnsureSuccessAsync(activateResponse);

        await using (var activateStream = await activateResponse.Content.ReadAsStreamAsync())
        using (var activateDocument = await JsonDocument.ParseAsync(activateStream))
        {
            Assert.Equal("Scheduled", activateDocument.RootElement.GetProperty("status").GetString());
            Assert.Equal("Annual", activateDocument.RootElement.GetProperty("periodicity").GetString());
            Assert.Equal("PRO", activateDocument.RootElement.GetProperty("planCode").GetString());
        }

        var overviewResponse = await client.GetAsync($"/api/platform/companies/{scenario.TenantId}/subscription");
        await EnsureSuccessAsync(overviewResponse);

        await using (var overviewStream = await overviewResponse.Content.ReadAsStreamAsync())
        using (var overviewDocument = await JsonDocument.ParseAsync(overviewStream))
        {
            Assert.Equal("FREE", overviewDocument.RootElement.GetProperty("currentSubscription").GetProperty("planCode").GetString());
            Assert.Equal("PRO", overviewDocument.RootElement.GetProperty("scheduledReplacement").GetProperty("planCode").GetString());
            Assert.False(overviewDocument.RootElement.GetProperty("isBillable").GetBoolean());
        }

        var listResponse = await client.GetAsync("/api/platform/company-subscriptions?status=Scheduled&page=1&pageSize=20");
        await EnsureSuccessAsync(listResponse);

        await using var listStream = await listResponse.Content.ReadAsStreamAsync();
        using var listDocument = await JsonDocument.ParseAsync(listStream);
        Assert.True(listDocument.RootElement.GetProperty("totalCount").GetInt32() >= 1);
    }

    [Fact]
    public async Task CompanySubscriptions_ManualStatusChange_ShouldSuspendReactivateAndExposeHistory()
    {
        var scenario = await factory.ResetDatabaseAsync(async dbContext =>
        {
            await PlatformTestSeed.SeedPlatformOperatorAsync(
                dbContext,
                PlatformOperatorUserId,
                "platform.subscription@clarihr.test",
                "hashed-password",
                PlatformOperatorRole.Admin);
        });

        using var client = factory.CreateClientFor(TestUserContext.PlatformAuthenticatedWithoutTenant(PlatformOperatorUserId));
        var subscriptionId = await GetCurrentSubscriptionIdAsync(client, scenario.TenantId);

        var suspendResponse = await client.PatchJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscriptions/{subscriptionId}/status",
            new
            {
                targetStatus = SubscriptionStatus.Suspended,
                reasonCode = SubscriptionStatusChangeReasonCode.ManualSuspension,
                observations = "Mora administrativa"
            });
        await EnsureSuccessAsync(suspendResponse);

        await using (var suspendStream = await suspendResponse.Content.ReadAsStreamAsync())
        using (var suspendDocument = await JsonDocument.ParseAsync(suspendStream))
        {
            Assert.Equal("Suspended", suspendDocument.RootElement.GetProperty("status").GetString());
            Assert.Equal("ManualSuspension", suspendDocument.RootElement.GetProperty("currentStatusReasonCode").GetString());
            Assert.False(suspendDocument.RootElement.GetProperty("canOperate").GetBoolean());
            Assert.False(suspendDocument.RootElement.GetProperty("canGenerateCharges").GetBoolean());
        }

        var reactivateResponse = await client.PatchJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscriptions/{subscriptionId}/status",
            new
            {
                targetStatus = SubscriptionStatus.Active,
                reasonCode = SubscriptionStatusChangeReasonCode.AuthorizedReactivation,
                observations = "Pago validado"
            });
        await EnsureSuccessAsync(reactivateResponse);

        await using (var reactivateStream = await reactivateResponse.Content.ReadAsStreamAsync())
        using (var reactivateDocument = await JsonDocument.ParseAsync(reactivateStream))
        {
            Assert.Equal("Active", reactivateDocument.RootElement.GetProperty("status").GetString());
            Assert.Equal("AuthorizedReactivation", reactivateDocument.RootElement.GetProperty("currentStatusReasonCode").GetString());
            Assert.True(reactivateDocument.RootElement.GetProperty("canOperate").GetBoolean());
            Assert.True(reactivateDocument.RootElement.GetProperty("canGenerateCharges").GetBoolean());
        }

        var historyResponse = await client.GetAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscriptions/{subscriptionId}/status-history?page=1&pageSize=10");
        await EnsureSuccessAsync(historyResponse);

        await using (var historyStream = await historyResponse.Content.ReadAsStreamAsync())
        using (var historyDocument = await JsonDocument.ParseAsync(historyStream))
        {
            Assert.Equal(3, historyDocument.RootElement.GetProperty("totalCount").GetInt32());
            var items = historyDocument.RootElement.GetProperty("items");
            Assert.Equal("Active", items[0].GetProperty("newStatus").GetString());
            Assert.Equal("Suspended", items[1].GetProperty("newStatus").GetString());
            Assert.Equal("Active", items[2].GetProperty("newStatus").GetString());
        }

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await dbContext.PlatformAuditLogs.AnyAsync(log => log.EventType == "COMPANY_SUBSCRIPTION_STATUS_CHANGED"));
    }

    [Fact]
    public async Task CompanySubscriptions_ManualStatusChange_WithReadOnlyOperator_ShouldReturnForbidden()
    {
        var readOnlyUserId = Guid.Parse("90000000-0000-0000-0000-000000000003");

        var scenario = await factory.ResetDatabaseAsync(async dbContext =>
        {
            await PlatformTestSeed.SeedPlatformOperatorAsync(
                dbContext,
                readOnlyUserId,
                "platform.readonly@clarihr.test",
                "hashed-password",
                PlatformOperatorRole.ReadOnly);
        });

        using var client = factory.CreateClientFor(TestUserContext.PlatformAuthenticatedWithoutTenant(readOnlyUserId));
        var subscriptionId = await GetCurrentSubscriptionIdAsync(client, scenario.TenantId);

        var response = await client.PatchJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscriptions/{subscriptionId}/status",
            new
            {
                targetStatus = SubscriptionStatus.Cancelled,
                reasonCode = SubscriptionStatusChangeReasonCode.CommercialCancellation,
                observations = "Intento sin permiso"
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CompanySubscriptions_ExpirationProcessor_ShouldExpireDueSubscriptionsAndUpdateOverview()
    {
        Guid proPlanPublicId = Guid.Empty;

        var scenario = await factory.ResetDatabaseAsync(async dbContext =>
        {
            await PlatformTestSeed.SeedPlatformOperatorAsync(
                dbContext,
                PlatformOperatorUserId,
                "platform.subscription@clarihr.test",
                "hashed-password",
                PlatformOperatorRole.Admin);

            var proPlan = CommercialPlan.Create(
                "PRO",
                "Professional",
                "Plan profesional",
                150m,
                4m,
                CommercialPlanStatus.Active,
                isSystemPlan: false,
                []);
            dbContext.CommercialPlans.Add(proPlan);
            await dbContext.SaveChangesAsync();
            proPlanPublicId = proPlan.PublicId;
        });

        using var client = factory.CreateClientFor(TestUserContext.PlatformAuthenticatedWithoutTenant(PlatformOperatorUserId));
        var activationResponse = await client.PutJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscription",
            new
            {
                commercialPlanId = proPlanPublicId,
                startDateUtc = DateTime.UtcNow.Date,
                expiresAtUtc = DateTime.UtcNow.Date,
                periodicity = CompanySubscriptionPeriodicity.Monthly
            });
        await EnsureSuccessAsync(activationResponse);

        await InvokeExpirationProcessorAsync();

        var overviewResponse = await client.GetAsync($"/api/platform/companies/{scenario.TenantId}/subscription");
        await EnsureSuccessAsync(overviewResponse);

        await using (var overviewStream = await overviewResponse.Content.ReadAsStreamAsync())
        using (var overviewDocument = await JsonDocument.ParseAsync(overviewStream))
        {
            var currentSubscription = overviewDocument.RootElement.GetProperty("currentSubscription");
            Assert.Equal("Expired", currentSubscription.GetProperty("status").GetString());
            Assert.Equal("ExpirationReached", currentSubscription.GetProperty("currentStatusReasonCode").GetString());
            Assert.False(currentSubscription.GetProperty("canOperate").GetBoolean());
            Assert.False(currentSubscription.GetProperty("canGenerateCharges").GetBoolean());
            Assert.False(overviewDocument.RootElement.GetProperty("isBillable").GetBoolean());
        }

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await dbContext.PlatformAuditLogs.AnyAsync(log => log.EventType == "COMPANY_SUBSCRIPTION_EXPIRATION_PROCESSED"));
    }

    private async Task<Guid> GetCurrentSubscriptionIdAsync(HttpClient client, Guid companyPublicId)
    {
        var response = await client.GetAsync($"/api/platform/companies/{companyPublicId}/subscription");
        await EnsureSuccessAsync(response);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement
            .GetProperty("currentSubscription")
            .GetProperty("subscriptionPublicId")
            .GetGuid();
    }

    private async Task InvokeExpirationProcessorAsync()
    {
        using var scope = factory.Services.CreateScope();
        var processorType = typeof(ApplicationDbContext).Assembly.GetType(
            "CLARIHR.Infrastructure.Companies.CompanySubscriptionLifecycleProcessor",
            throwOnError: true)!;
        var processor = scope.ServiceProvider.GetRequiredService(processorType);
        var expireMethod = processorType.GetMethod(
            "ExpireDueSubscriptionsAsync",
            BindingFlags.Instance | BindingFlags.Public)!;

        var invocation = expireMethod.Invoke(processor, [CancellationToken.None]);
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
