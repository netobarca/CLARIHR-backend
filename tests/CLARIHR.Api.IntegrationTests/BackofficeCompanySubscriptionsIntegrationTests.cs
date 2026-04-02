using System.Net;
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
        currentResponse.EnsureSuccessStatusCode();

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
        replaceResponse.EnsureSuccessStatusCode();

        await using (var replaceStream = await replaceResponse.Content.ReadAsStreamAsync())
        using (var replaceDocument = await JsonDocument.ParseAsync(replaceStream))
        {
            Assert.Equal("PRO", replaceDocument.RootElement.GetProperty("planCode").GetString());
            Assert.Equal("Active", replaceDocument.RootElement.GetProperty("status").GetString());
        }

        var historyResponse = await client.GetAsync($"/api/platform/companies/{scenario.TenantId}/subscriptions?page=1&pageSize=10");
        historyResponse.EnsureSuccessStatusCode();

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
        previewResponse.EnsureSuccessStatusCode();

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
        activateResponse.EnsureSuccessStatusCode();

        await using (var activateStream = await activateResponse.Content.ReadAsStreamAsync())
        using (var activateDocument = await JsonDocument.ParseAsync(activateStream))
        {
            Assert.Equal("Scheduled", activateDocument.RootElement.GetProperty("status").GetString());
            Assert.Equal("Annual", activateDocument.RootElement.GetProperty("periodicity").GetString());
            Assert.Equal("PRO", activateDocument.RootElement.GetProperty("planCode").GetString());
        }

        var overviewResponse = await client.GetAsync($"/api/platform/companies/{scenario.TenantId}/subscription");
        overviewResponse.EnsureSuccessStatusCode();

        await using (var overviewStream = await overviewResponse.Content.ReadAsStreamAsync())
        using (var overviewDocument = await JsonDocument.ParseAsync(overviewStream))
        {
            Assert.Equal("FREE", overviewDocument.RootElement.GetProperty("currentSubscription").GetProperty("planCode").GetString());
            Assert.Equal("PRO", overviewDocument.RootElement.GetProperty("scheduledReplacement").GetProperty("planCode").GetString());
            Assert.False(overviewDocument.RootElement.GetProperty("isBillable").GetBoolean());
        }

        var listResponse = await client.GetAsync("/api/platform/company-subscriptions?status=Scheduled&page=1&pageSize=20");
        listResponse.EnsureSuccessStatusCode();

        await using var listStream = await listResponse.Content.ReadAsStreamAsync();
        using var listDocument = await JsonDocument.ParseAsync(listStream);
        Assert.True(listDocument.RootElement.GetProperty("totalCount").GetInt32() >= 1);
    }
}
