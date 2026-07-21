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
                observations = "Mora administrativa",
                concurrencyToken = await GetCurrentSubscriptionConcurrencyTokenAsync(client, scenario.TenantId)
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
                observations = "Pago validado",
                effectiveDateUtc = DateTime.UtcNow.Date,
                concurrencyToken = await GetCurrentSubscriptionConcurrencyTokenAsync(client, scenario.TenantId)
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
    public async Task CompanySubscriptions_ChangeStatusWithoutConcurrencyToken_ShouldReturnBadRequest()
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

        // No concurrencyToken in the body → PatchJsonAsync sends no If-Match header → the strong-token
        // binder rejects the write with 400 before the handler is ever reached.
        var response = await client.PatchJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscriptions/{subscriptionId}/status",
            new
            {
                targetStatus = SubscriptionStatus.Suspended,
                reasonCode = SubscriptionStatusChangeReasonCode.ManualSuspension,
                observations = "Sin token"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CompanySubscriptions_ChangeStatusWithStaleConcurrencyToken_ShouldReturnConflict()
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
        var staleToken = await GetCurrentSubscriptionConcurrencyTokenAsync(client, scenario.TenantId);

        // Rotate the token with a real suspend, then try to act again with the now-stale value.
        var suspendResponse = await client.PatchJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscriptions/{subscriptionId}/status",
            new
            {
                targetStatus = SubscriptionStatus.Suspended,
                reasonCode = SubscriptionStatusChangeReasonCode.ManualSuspension,
                observations = "Mora",
                concurrencyToken = staleToken
            });
        await EnsureSuccessAsync(suspendResponse);

        var staleResponse = await client.PatchJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscriptions/{subscriptionId}/status",
            new
            {
                targetStatus = SubscriptionStatus.Active,
                reasonCode = SubscriptionStatusChangeReasonCode.AuthorizedReactivation,
                observations = "Token viejo",
                effectiveDateUtc = DateTime.UtcNow.Date,
                concurrencyToken = staleToken
            });

        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);

        await using var staleStream = await staleResponse.Content.ReadAsStreamAsync();
        using var staleDocument = await JsonDocument.ParseAsync(staleStream);
        Assert.Equal("PLATFORM_COMPANY_SUBSCRIPTION_CONCURRENCY_CONFLICT", staleDocument.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task CompanySubscriptions_StatusChangePreviewAndScheduling_ShouldExposePendingStatusChangeAndApplyViaProcessor()
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
                observations = "Bloqueo temporal",
                effectiveDateUtc = (DateTime?)null,
                concurrencyToken = await GetCurrentSubscriptionConcurrencyTokenAsync(client, scenario.TenantId)
            });
        await EnsureSuccessAsync(suspendResponse);

        var futureDate = DateTime.UtcNow.Date.AddDays(3);

        var previewResponse = await client.PostJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscriptions/{subscriptionId}/status/preview",
            new
            {
                targetStatus = SubscriptionStatus.Active,
                reasonCode = SubscriptionStatusChangeReasonCode.AuthorizedReactivation,
                observations = "Pago confirmado",
                effectiveDateUtc = futureDate
            });
        await EnsureSuccessAsync(previewResponse);

        await using (var previewStream = await previewResponse.Content.ReadAsStreamAsync())
        using (var previewDocument = await JsonDocument.ParseAsync(previewStream))
        {
            Assert.True(previewDocument.RootElement.GetProperty("isEligible").GetBoolean());
            Assert.Equal("Suspended", previewDocument.RootElement.GetProperty("currentStatus").GetString());
            Assert.Equal("Active", previewDocument.RootElement.GetProperty("targetStatus").GetString());
            Assert.Equal(futureDate, previewDocument.RootElement.GetProperty("effectiveDateUtc").GetDateTime());
            Assert.Equal("FREE", previewDocument.RootElement.GetProperty("planCode").GetString());
        }

        var scheduleResponse = await client.PatchJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscriptions/{subscriptionId}/status",
            new
            {
                targetStatus = SubscriptionStatus.Active,
                reasonCode = SubscriptionStatusChangeReasonCode.AuthorizedReactivation,
                observations = "Pago confirmado",
                effectiveDateUtc = futureDate,
                concurrencyToken = await GetCurrentSubscriptionConcurrencyTokenAsync(client, scenario.TenantId)
            });
        await EnsureSuccessAsync(scheduleResponse);

        await using (var scheduleStream = await scheduleResponse.Content.ReadAsStreamAsync())
        using (var scheduleDocument = await JsonDocument.ParseAsync(scheduleStream))
        {
            Assert.Equal("Suspended", scheduleDocument.RootElement.GetProperty("status").GetString());
            var pending = scheduleDocument.RootElement.GetProperty("pendingStatusChange");
            Assert.Equal("Active", pending.GetProperty("targetStatus").GetString());
            Assert.Equal("AuthorizedReactivation", pending.GetProperty("reasonCode").GetString());
            Assert.Equal(futureDate, pending.GetProperty("effectiveDateUtc").GetDateTime());
        }

        Guid statusChangeRequestId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var subscriptionInternalId = await dbContext.CompanySubscriptions
                .Where(subscription => subscription.PublicId == subscriptionId)
                .Select(subscription => subscription.Id)
                .SingleAsync();

            statusChangeRequestId = await dbContext.CompanySubscriptionStatusChangeRequests
                .Where(request => request.CompanySubscriptionId == subscriptionInternalId)
                .Select(request => request.PublicId)
                .SingleAsync();
        }

        var overviewBeforeApplyResponse = await client.GetAsync($"/api/platform/companies/{scenario.TenantId}/subscription");
        await EnsureSuccessAsync(overviewBeforeApplyResponse);

        await using (var overviewBeforeApplyStream = await overviewBeforeApplyResponse.Content.ReadAsStreamAsync())
        using (var overviewBeforeApplyDocument = await JsonDocument.ParseAsync(overviewBeforeApplyStream))
        {
            var currentSubscription = overviewBeforeApplyDocument.RootElement.GetProperty("currentSubscription");
            Assert.Equal("Suspended", currentSubscription.GetProperty("status").GetString());
            Assert.Equal(
                futureDate,
                currentSubscription.GetProperty("pendingStatusChange").GetProperty("effectiveDateUtc").GetDateTime());
            Assert.False(overviewBeforeApplyDocument.RootElement.GetProperty("isBillable").GetBoolean());
        }

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            _ = await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE company_subscription_status_change_requests SET effective_date_utc = {DateTime.UtcNow.Date} WHERE public_id = {statusChangeRequestId}");
        }

        _ = await InvokeStatusChangeProcessorAsync();

        var overviewAfterApplyResponse = await client.GetAsync($"/api/platform/companies/{scenario.TenantId}/subscription");
        await EnsureSuccessAsync(overviewAfterApplyResponse);

        await using (var overviewAfterApplyStream = await overviewAfterApplyResponse.Content.ReadAsStreamAsync())
        using (var overviewAfterApplyDocument = await JsonDocument.ParseAsync(overviewAfterApplyStream))
        {
            var currentSubscription = overviewAfterApplyDocument.RootElement.GetProperty("currentSubscription");
            Assert.Equal("Active", currentSubscription.GetProperty("status").GetString());
            Assert.Equal("AuthorizedReactivation", currentSubscription.GetProperty("currentStatusReasonCode").GetString());
            Assert.True(currentSubscription.GetProperty("canOperate").GetBoolean());
            Assert.True(currentSubscription.GetProperty("canGenerateCharges").GetBoolean());
            Assert.False(overviewAfterApplyDocument.RootElement.GetProperty("isBillable").GetBoolean());

            if (currentSubscription.TryGetProperty("pendingStatusChange", out var pendingStatusChange))
            {
                Assert.Equal(JsonValueKind.Null, pendingStatusChange.ValueKind);
            }
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
            Assert.Equal("AuthorizedReactivation", items[0].GetProperty("reasonCode").GetString());
        }

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.True(await dbContext.PlatformAuditLogs.AnyAsync(log => log.EventType == "COMPANY_SUBSCRIPTION_STATUS_CHANGE_REQUESTED"));
            Assert.True(await dbContext.PlatformAuditLogs.AnyAsync(log => log.EventType == "COMPANY_SUBSCRIPTION_STATUS_CHANGE_APPLIED"));
        }
    }

    [Fact]
    public async Task CompanySubscriptions_StatusChangeScheduling_WhenDuplicatePendingRequestExists_ShouldReturnConflict()
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
                observations = "Mora",
                effectiveDateUtc = (DateTime?)null,
                concurrencyToken = await GetCurrentSubscriptionConcurrencyTokenAsync(client, scenario.TenantId)
            });
        await EnsureSuccessAsync(suspendResponse);

        var firstScheduleResponse = await client.PatchJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscriptions/{subscriptionId}/status",
            new
            {
                targetStatus = SubscriptionStatus.Active,
                reasonCode = SubscriptionStatusChangeReasonCode.AuthorizedReactivation,
                observations = "Regularizacion inicial",
                effectiveDateUtc = DateTime.UtcNow.Date.AddDays(2),
                concurrencyToken = await GetCurrentSubscriptionConcurrencyTokenAsync(client, scenario.TenantId)
            });
        await EnsureSuccessAsync(firstScheduleResponse);

        var duplicateResponse = await client.PatchJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscriptions/{subscriptionId}/status",
            new
            {
                targetStatus = SubscriptionStatus.Active,
                reasonCode = SubscriptionStatusChangeReasonCode.AuthorizedReactivation,
                observations = "Intento duplicado",
                effectiveDateUtc = DateTime.UtcNow.Date.AddDays(3),
                concurrencyToken = await GetCurrentSubscriptionConcurrencyTokenAsync(client, scenario.TenantId)
            });

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        await using var duplicateStream = await duplicateResponse.Content.ReadAsStreamAsync();
        using var duplicateDocument = await JsonDocument.ParseAsync(duplicateStream);
        Assert.Equal("PLATFORM_COMPANY_SUBSCRIPTION_STATUS_CHANGE_PENDING_CONFLICT", duplicateDocument.RootElement.GetProperty("code").GetString());
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
                observations = "Intento sin permiso",
                concurrencyToken = await GetCurrentSubscriptionConcurrencyTokenAsync(client, scenario.TenantId)
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

    [Fact]
    public async Task CompanySubscriptions_PlanChangeImmediate_ShouldApplySwapPersistHistoryAndAudit()
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

        var previewResponse = await client.PostJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscription/plan-changes/preview",
            new
            {
                commercialPlanId = proPlanPublicId,
                mode = SubscriptionPlanChangeMode.Immediate
            });
        await EnsureSuccessAsync(previewResponse);

        await using (var previewStream = await previewResponse.Content.ReadAsStreamAsync())
        using (var previewDocument = await JsonDocument.ParseAsync(previewStream))
        {
            Assert.True(previewDocument.RootElement.GetProperty("isEligible").GetBoolean());
            Assert.Equal("FREE", previewDocument.RootElement.GetProperty("currentPlanCode").GetString());
            Assert.Equal("PRO", previewDocument.RootElement.GetProperty("targetPlanCode").GetString());
            Assert.Equal("Immediate", previewDocument.RootElement.GetProperty("mode").GetString());
        }

        var createResponse = await client.PostJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscription/plan-changes",
            new
            {
                commercialPlanId = proPlanPublicId,
                mode = SubscriptionPlanChangeMode.Immediate,
                reasonCode = SubscriptionPlanChangeReasonCode.UpgradeCommercial,
                observations = "Upgrade por crecimiento"
            });
        await EnsureSuccessAsync(createResponse);

        Guid planChangeId;
        await using (var createStream = await createResponse.Content.ReadAsStreamAsync())
        using (var createDocument = await JsonDocument.ParseAsync(createStream))
        {
            planChangeId = createDocument.RootElement.GetProperty("planChangePublicId").GetGuid();
            Assert.Equal("Applied", createDocument.RootElement.GetProperty("status").GetString());
            Assert.Equal("PRO", createDocument.RootElement.GetProperty("targetPlanCode").GetString());
            Assert.Equal("FREE", createDocument.RootElement.GetProperty("currentPlanCode").GetString());
        }

        var historyResponse = await client.GetAsync($"/api/platform/companies/{scenario.TenantId}/subscription/plan-changes?page=1&pageSize=10");
        await EnsureSuccessAsync(historyResponse);

        await using (var historyStream = await historyResponse.Content.ReadAsStreamAsync())
        using (var historyDocument = await JsonDocument.ParseAsync(historyStream))
        {
            Assert.Equal(1, historyDocument.RootElement.GetProperty("totalCount").GetInt32());
            Assert.Equal(planChangeId, historyDocument.RootElement.GetProperty("items")[0].GetProperty("planChangePublicId").GetGuid());
        }

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var companyId = await dbContext.Companies
            .Where(company => company.PublicId == scenario.TenantId)
            .Select(company => company.Id)
            .SingleAsync();

        Assert.Equal(
            1,
            await dbContext.CompanySubscriptionPlanChanges.CountAsync(planChange => planChange.CompanyId == companyId));
        Assert.True(await dbContext.PlatformAuditLogs.AnyAsync(log => log.EventType == "COMPANY_SUBSCRIPTION_PLAN_CHANGE_APPLIED"));

        var subscriptions = await dbContext.CompanySubscriptions
            .Where(subscription => subscription.CompanyId == companyId)
            .OrderBy(subscription => subscription.CreatedUtc)
            .ToListAsync();

        Assert.Equal(2, subscriptions.Count);
        Assert.Single(subscriptions, subscription => subscription.Status == SubscriptionStatus.Active && subscription.PlanCode == "PRO");
        Assert.Single(subscriptions, subscription => subscription.Status == SubscriptionStatus.Cancelled && subscription.PlanCode == "FREE");
    }

    [Fact]
    public async Task CompanySubscriptions_PlanChangeScheduled_ShouldListAndAllowCancellation()
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
        var effectiveDate = DateTime.UtcNow.Date.AddDays(7);

        var createResponse = await client.PostJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscription/plan-changes",
            new
            {
                commercialPlanId = proPlanPublicId,
                mode = SubscriptionPlanChangeMode.SpecificDate,
                requestedEffectiveDateUtc = effectiveDate,
                reasonCode = SubscriptionPlanChangeReasonCode.CommercialStrategyMigration,
                observations = "Migracion programada"
            });
        await EnsureSuccessAsync(createResponse);

        Guid planChangeId;
        Guid concurrencyToken;
        await using (var createStream = await createResponse.Content.ReadAsStreamAsync())
        using (var createDocument = await JsonDocument.ParseAsync(createStream))
        {
            planChangeId = createDocument.RootElement.GetProperty("planChangePublicId").GetGuid();
            concurrencyToken = createDocument.RootElement.GetProperty("concurrencyToken").GetGuid();
            Assert.Equal("Scheduled", createDocument.RootElement.GetProperty("status").GetString());
            Assert.Equal(effectiveDate, createDocument.RootElement.GetProperty("effectiveDateUtc").GetDateTime());
        }

        var cancelResponse = await client.PatchJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscription/plan-changes/{planChangeId}/cancel",
            new
            {
                observations = "Cambio de decision comercial",
                concurrencyToken = concurrencyToken
            });
        await EnsureSuccessAsync(cancelResponse);

        await using (var cancelStream = await cancelResponse.Content.ReadAsStreamAsync())
        using (var cancelDocument = await JsonDocument.ParseAsync(cancelStream))
        {
            Assert.Equal("Cancelled", cancelDocument.RootElement.GetProperty("status").GetString());
            Assert.Equal(
                "Cambio de decision comercial",
                cancelDocument.RootElement.GetProperty("cancellationObservations").GetString());
        }

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await dbContext.PlatformAuditLogs.AnyAsync(log => log.EventType == "COMPANY_SUBSCRIPTION_PLAN_CHANGE_CANCELLED"));
    }

    [Fact]
    public async Task CompanySubscriptions_PlanChangeProcessor_ShouldApplyDueScheduledPlanChange()
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
        var futureDate = DateTime.UtcNow.Date.AddDays(2);

        var createResponse = await client.PostJsonAsync(
            $"/api/platform/companies/{scenario.TenantId}/subscription/plan-changes",
            new
            {
                commercialPlanId = proPlanPublicId,
                mode = SubscriptionPlanChangeMode.SpecificDate,
                requestedEffectiveDateUtc = futureDate,
                reasonCode = SubscriptionPlanChangeReasonCode.UpgradeCommercial,
                observations = "Aplicar por processor"
            });
        await EnsureSuccessAsync(createResponse);

        Guid planChangeId;
        await using (var createStream = await createResponse.Content.ReadAsStreamAsync())
        using (var createDocument = await JsonDocument.ParseAsync(createStream))
        {
            planChangeId = createDocument.RootElement.GetProperty("planChangePublicId").GetGuid();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            _ = await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE company_subscription_plan_changes SET effective_date_utc = {DateTime.UtcNow.Date} WHERE public_id = {planChangeId}");
        }

        await InvokePlanChangeProcessorAsync();

        var historyResponse = await client.GetAsync($"/api/platform/companies/{scenario.TenantId}/subscription/plan-changes?page=1&pageSize=10");
        await EnsureSuccessAsync(historyResponse);

        await using (var historyStream = await historyResponse.Content.ReadAsStreamAsync())
        using (var historyDocument = await JsonDocument.ParseAsync(historyStream))
        {
            Assert.Equal("Applied", historyDocument.RootElement.GetProperty("items")[0].GetProperty("status").GetString());
        }

        var overviewResponse = await client.GetAsync($"/api/platform/companies/{scenario.TenantId}/subscription");
        await EnsureSuccessAsync(overviewResponse);

        await using (var overviewStream = await overviewResponse.Content.ReadAsStreamAsync())
        using (var overviewDocument = await JsonDocument.ParseAsync(overviewStream))
        {
            Assert.Equal("PRO", overviewDocument.RootElement.GetProperty("currentSubscription").GetProperty("planCode").GetString());
            Assert.Equal("Active", overviewDocument.RootElement.GetProperty("currentSubscription").GetProperty("status").GetString());
        }
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

    // Fetched fresh right before each ChangeStatus PATCH rather than chained from a prior response,
    // so callers don't need to reason about whether the previous action rotated the subscription's token
    // (it does on an immediate transition, it doesn't when only a pending request is scheduled).
    private async Task<Guid> GetCurrentSubscriptionConcurrencyTokenAsync(HttpClient client, Guid companyPublicId)
    {
        var response = await client.GetAsync($"/api/platform/companies/{companyPublicId}/subscription");
        await EnsureSuccessAsync(response);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement
            .GetProperty("currentSubscription")
            .GetProperty("concurrencyToken")
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

    private async Task InvokePlanChangeProcessorAsync()
    {
        using var scope = factory.Services.CreateScope();
        var processorType = typeof(ApplicationDbContext).Assembly.GetType(
            "CLARIHR.Infrastructure.Companies.CompanySubscriptionLifecycleProcessor",
            throwOnError: true)!;
        var processor = scope.ServiceProvider.GetRequiredService(processorType);
        var applyMethod = processorType.GetMethod(
            "ApplyDueScheduledPlanChangesAsync",
            BindingFlags.Instance | BindingFlags.Public)!;

        var invocation = applyMethod.Invoke(processor, [CancellationToken.None]);
        var task = Assert.IsAssignableFrom<Task<int>>(invocation);
        var processedCount = await task;

        Assert.True(processedCount >= 1);
    }

    private async Task<int> InvokeStatusChangeProcessorAsync(bool shouldProcessAtLeastOne = true)
    {
        using var scope = factory.Services.CreateScope();
        var processorType = typeof(ApplicationDbContext).Assembly.GetType(
            "CLARIHR.Infrastructure.Companies.CompanySubscriptionLifecycleProcessor",
            throwOnError: true)!;
        var processor = scope.ServiceProvider.GetRequiredService(processorType);
        var applyMethod = processorType.GetMethod(
            "ApplyDueScheduledStatusChangesAsync",
            BindingFlags.Instance | BindingFlags.Public)!;

        var invocation = applyMethod.Invoke(processor, [CancellationToken.None]);
        var task = Assert.IsAssignableFrom<Task<int>>(invocation);
        var processedCount = await task;

        if (shouldProcessAtLeastOne)
        {
            Assert.True(processedCount >= 1);
        }

        return processedCount;
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
