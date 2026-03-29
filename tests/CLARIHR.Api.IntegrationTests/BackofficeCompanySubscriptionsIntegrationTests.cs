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
    public async Task CompanySubscriptions_Replace_ShouldUpdateActiveSubscriptionKeepHistoryAndAudit()
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
            Assert.Equal("FREE", currentDocument.RootElement.GetProperty("planCode").GetString());
        }

        var replaceResponse = await client.PutJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscription",
            new { commercialPlanPublicId = proPlanPublicId });
        replaceResponse.EnsureSuccessStatusCode();

        await using (var replaceStream = await replaceResponse.Content.ReadAsStreamAsync())
        using (var replaceDocument = await JsonDocument.ParseAsync(replaceStream))
        {
            Assert.Equal("PRO", replaceDocument.RootElement.GetProperty("planCode").GetString());
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
        Assert.True(await dbContext.PlatformAuditLogs.AnyAsync());
    }

    [Fact]
    public async Task CompanySubscriptions_Replace_WithInactivePlan_ShouldReturnConflict()
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
            new { commercialPlanPublicId = inactivePlanPublicId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        Assert.Equal("PLATFORM_COMPANY_SUBSCRIPTION_PLAN_INACTIVE", document.RootElement.GetProperty("code").GetString());
    }
}
