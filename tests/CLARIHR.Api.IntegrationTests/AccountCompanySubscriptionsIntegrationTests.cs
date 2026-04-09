using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.Platform;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Api.IntegrationTests;

public sealed class AccountCompanySubscriptionsIntegrationTests(IntegrationTestWebApplicationFactory factory)
    : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = IntegrationTestJson.CreateOptions();
    private static readonly Guid SeedActorUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task AccountCompanySubscription_OwnerFlow_ShouldUpgradeAcquireAddonAndDowngradeToFree()
    {
        Guid freePlanPublicId = Guid.Empty;
        Guid proPlanPublicId = Guid.Empty;
        Guid addonPublicId = Guid.Empty;

        var scenario = await factory.ResetDatabaseAsync(async dbContext =>
        {
            var freePlanId = await dbContext.CommercialPlans
                .Where(plan => plan.NormalizedCode == "FREE")
                .Select(plan => plan.Id)
                .SingleAsync();

            var allowedFreeModules = new HashSet<string>(StringComparer.Ordinal)
            {
                CommercialModuleKeys.Rbac,
                CommercialModuleKeys.Users,
                CommercialModuleKeys.OrgStructureCatalogs
            };

            var extraFreeEntitlements = await dbContext.PlanEntitlements
                .Where(entitlement => entitlement.CommercialPlanId == freePlanId && !allowedFreeModules.Contains(entitlement.ModuleKey))
                .ToListAsync();
            dbContext.PlanEntitlements.RemoveRange(extraFreeEntitlements);

            var freePlan = await dbContext.CommercialPlans.SingleAsync(plan => plan.Id == freePlanId);
            freePlanPublicId = freePlan.PublicId;

            var proPlan = CommercialPlan.Create(
                "PRO",
                "Professional",
                "Plan profesional",
                150m,
                4m,
                CommercialPlanStatus.Active,
                isSystemPlan: false,
                [CommercialModuleKeys.JobProfiles, CommercialModuleKeys.OrgUnits],
                []);
            dbContext.CommercialPlans.Add(proPlan);

            var addon = CommercialAddon.Create(
                "ADDON-PAYROLL",
                "Payroll Booster",
                "Payroll addon",
                CommercialAddonType.Massive,
                CommercialAddonBillingModel.PerActiveEmployee,
                CommercialAddon.MassiveMeasurementUnit,
                2.5m,
                null,
                15m,
                [CommercialModuleKeys.SalaryTabulator],
                CommercialAddonPeriodicity.Monthly,
                CommercialAddonStatus.Active);
            dbContext.CommercialAddons.Add(addon);

            await dbContext.SaveChangesAsync();
            proPlanPublicId = proPlan.PublicId;
            addonPublicId = addon.PublicId;
        });

        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var initialOverviewResponse = await client.GetAsync($"/api/account/companies/{scenario.TenantId}/subscription");
        initialOverviewResponse.EnsureSuccessStatusCode();

        var initialOverview = await initialOverviewResponse.Content.ReadFromJsonAsync<AccountCompanySubscriptionOverviewEnvelope>(JsonOptions);
        Assert.NotNull(initialOverview);
        Assert.Equal("FREE", initialOverview!.CurrentPlan.Code);
        Assert.DoesNotContain(initialOverview.EffectiveModules, static module => module.ModuleKey == CommercialModuleKeys.JobProfiles);
        Assert.DoesNotContain(initialOverview.EffectiveModules, static module => module.ModuleKey == CommercialModuleKeys.SalaryTabulator);

        var plansResponse = await client.GetAsync($"/api/account/companies/{scenario.TenantId}/subscription/plans");
        Assert.True(plansResponse.IsSuccessStatusCode, await plansResponse.Content.ReadAsStringAsync());

        var plans = await plansResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<AccountCompanySubscriptionPlanEnvelope>>(JsonOptions);
        Assert.NotNull(plans);
        Assert.Contains(plans!, static plan => plan.Code == "FREE");
        Assert.DoesNotContain(plans!, static plan => plan.Code == "MASTER");
        Assert.Contains(
            plans!,
            static plan =>
                plan.Code == "PRO" &&
                plan.ModuleKeys.Contains(CommercialModuleKeys.JobProfiles) &&
                plan.ModuleKeys.Contains(CommercialModuleKeys.OrgUnits));

        var freeMarketplaceResponse = await client.GetAsync($"/api/account/companies/{scenario.TenantId}/subscription/addons/marketplace");
        freeMarketplaceResponse.EnsureSuccessStatusCode();

        var freeMarketplace = await freeMarketplaceResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<AccountCompanyMarketplaceAddonEnvelope>>(JsonOptions);
        Assert.NotNull(freeMarketplace);
        var blockedAddon = Assert.Single(freeMarketplace!, addon => addon.CommercialAddonId == addonPublicId);
        Assert.False(blockedAddon.CanAcquire);
        Assert.False(blockedAddon.IsOwned);
        Assert.Contains("FREE", blockedAddon.BlockedReason, StringComparison.OrdinalIgnoreCase);

        var planPreviewResponse = await client.PostJsonAsync(
            $"/api/account/companies/{scenario.TenantId}/subscription/preview",
            new { commercialPlanId = proPlanPublicId });
        planPreviewResponse.EnsureSuccessStatusCode();

        var planPreview = await planPreviewResponse.Content.ReadFromJsonAsync<AccountCompanyPlanPreviewEnvelope>(JsonOptions);
        Assert.NotNull(planPreview);
        Assert.True(planPreview!.IsEligible);
        Assert.Equal("FREE", planPreview.CurrentPlan.Code);
        Assert.Equal("PRO", planPreview.TargetPlan.Code);
        Assert.Contains(CommercialModuleKeys.JobProfiles, planPreview.AddedModuleKeys);
        Assert.Contains(CommercialModuleKeys.OrgUnits, planPreview.AddedModuleKeys);

        var upgradeResponse = await client.PutJsonAsync(
            $"/api/account/companies/{scenario.TenantId}/subscription",
            new
            {
                commercialPlanId = proPlanPublicId,
                observations = "Upgrade owner"
            });
        upgradeResponse.EnsureSuccessStatusCode();

        var upgradedOverview = await upgradeResponse.Content.ReadFromJsonAsync<AccountCompanySubscriptionOverviewEnvelope>(JsonOptions);
        Assert.NotNull(upgradedOverview);
        Assert.Equal("PRO", upgradedOverview!.CurrentPlan.Code);
        Assert.Contains(
            upgradedOverview.EffectiveModules,
            static module => module.ModuleKey == CommercialModuleKeys.JobProfiles && module.GrantedByPlan && !module.GrantedByAddon);

        var paidMarketplaceResponse = await client.GetAsync($"/api/account/companies/{scenario.TenantId}/subscription/addons/marketplace");
        paidMarketplaceResponse.EnsureSuccessStatusCode();

        var paidMarketplace = await paidMarketplaceResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<AccountCompanyMarketplaceAddonEnvelope>>(JsonOptions);
        Assert.NotNull(paidMarketplace);
        var acquirableAddon = Assert.Single(paidMarketplace!, addon => addon.CommercialAddonId == addonPublicId);
        Assert.True(acquirableAddon.CanAcquire);
        Assert.Null(acquirableAddon.BlockedReason);

        var addonPreviewResponse = await client.PostJsonAsync(
            $"/api/account/companies/{scenario.TenantId}/subscription/addons/preview",
            new
            {
                commercialAddonId = addonPublicId,
                action = SubscriptionAddonChangeAction.Activate
            });
        addonPreviewResponse.EnsureSuccessStatusCode();

        var addonPreview = await addonPreviewResponse.Content.ReadFromJsonAsync<AccountCompanyAddonPreviewEnvelope>(JsonOptions);
        Assert.NotNull(addonPreview);
        Assert.True(addonPreview!.IsEligible);
        Assert.Contains(CommercialModuleKeys.SalaryTabulator, addonPreview.AddedModuleKeys);

        var addonApplyResponse = await client.PostJsonAsync(
            $"/api/account/companies/{scenario.TenantId}/subscription/addons",
            new
            {
                commercialAddonId = addonPublicId,
                action = SubscriptionAddonChangeAction.Activate,
                observations = "Owner addon activation"
            });
        addonApplyResponse.EnsureSuccessStatusCode();

        var withAddonOverview = await addonApplyResponse.Content.ReadFromJsonAsync<AccountCompanySubscriptionOverviewEnvelope>(JsonOptions);
        Assert.NotNull(withAddonOverview);
        Assert.Single(withAddonOverview!.ActiveAddons);
        Assert.Contains(
            withAddonOverview.EffectiveModules,
            static module => module.ModuleKey == CommercialModuleKeys.SalaryTabulator && module.GrantedByAddon);

        var downgradeResponse = await client.PutJsonAsync(
            $"/api/account/companies/{scenario.TenantId}/subscription",
            new
            {
                commercialPlanId = freePlanPublicId,
                observations = "Downgrade owner"
            });
        Assert.True(downgradeResponse.IsSuccessStatusCode, await downgradeResponse.Content.ReadAsStringAsync());

        var downgradedOverview = await downgradeResponse.Content.ReadFromJsonAsync<AccountCompanySubscriptionOverviewEnvelope>(JsonOptions);
        Assert.NotNull(downgradedOverview);
        Assert.Equal("FREE", downgradedOverview!.CurrentPlan.Code);
        Assert.Empty(downgradedOverview.ActiveAddons);
        Assert.DoesNotContain(downgradedOverview.EffectiveModules, static module => module.ModuleKey == CommercialModuleKeys.JobProfiles);
        Assert.DoesNotContain(downgradedOverview.EffectiveModules, static module => module.ModuleKey == CommercialModuleKeys.SalaryTabulator);
    }

    [Fact]
    public async Task AccountCompanySubscription_NonOwner_ShouldReturnForbidden()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.TargetUserId, scenario.TenantId));

        var response = await client.GetAsync($"/api/account/companies/{scenario.TenantId}/subscription");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AccountCompanySubscription_OwnerWithoutPlatformOperator_ShouldRejectMasterPreviewAndChange()
    {
        Guid masterPlanPublicId = Guid.Empty;

        var scenario = await factory.ResetDatabaseAsync(async dbContext =>
        {
            masterPlanPublicId = await dbContext.CommercialPlans
                .Where(plan => plan.NormalizedCode == "MASTER")
                .Select(plan => plan.PublicId)
                .SingleAsync();
        });

        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var plansResponse = await client.GetAsync($"/api/account/companies/{scenario.TenantId}/subscription/plans");
        plansResponse.EnsureSuccessStatusCode();

        var plans = await plansResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<AccountCompanySubscriptionPlanEnvelope>>(JsonOptions);
        Assert.NotNull(plans);
        Assert.DoesNotContain(plans!, static plan => plan.Code == "MASTER");

        var previewResponse = await client.PostJsonAsync(
            $"/api/account/companies/{scenario.TenantId}/subscription/preview",
            new { commercialPlanId = masterPlanPublicId });
        await AssertProblemDetailsAsync(
            previewResponse,
            HttpStatusCode.Forbidden,
            "ACCOUNT_COMPANY_SUBSCRIPTION_MASTER_FORBIDDEN");

        var changeResponse = await client.PutJsonAsync(
            $"/api/account/companies/{scenario.TenantId}/subscription",
            new
            {
                commercialPlanId = masterPlanPublicId,
                observations = "Attempt MASTER without platform operator"
            });
        await AssertProblemDetailsAsync(
            changeResponse,
            HttpStatusCode.Forbidden,
            "ACCOUNT_COMPANY_SUBSCRIPTION_MASTER_FORBIDDEN");
    }

    [Fact]
    public async Task AccountCompanySubscription_OwnerPlatformOperator_ShouldSeeAndPreviewMaster()
    {
        Guid masterPlanPublicId = Guid.Empty;

        var scenario = await factory.ResetDatabaseAsync(async dbContext =>
        {
            masterPlanPublicId = await dbContext.CommercialPlans
                .Where(plan => plan.NormalizedCode == "MASTER")
                .Select(plan => plan.PublicId)
                .SingleAsync();

            var actorUser = await dbContext.AuthUsers
                .SingleAsync(user => user.PublicId == SeedActorUserId);
            dbContext.PlatformOperators.Add(PlatformOperator.Create(actorUser.Id, PlatformOperatorRole.Admin));
        });

        using var client = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var plansResponse = await client.GetAsync($"/api/account/companies/{scenario.TenantId}/subscription/plans");
        plansResponse.EnsureSuccessStatusCode();

        var plans = await plansResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<AccountCompanySubscriptionPlanEnvelope>>(JsonOptions);
        Assert.NotNull(plans);
        var masterPlan = Assert.Single(plans!, plan => plan.Code == "MASTER");
        Assert.Equal(CommercialModuleCatalog.DefaultMasterModuleKeys.Count, masterPlan.ModuleCount);
        Assert.Equal(
            CommercialModuleCatalog.DefaultMasterModuleKeys.OrderBy(static key => key, StringComparer.Ordinal),
            masterPlan.ModuleKeys.OrderBy(static key => key, StringComparer.Ordinal));

        var previewResponse = await client.PostJsonAsync(
            $"/api/account/companies/{scenario.TenantId}/subscription/preview",
            new { commercialPlanId = masterPlanPublicId });
        previewResponse.EnsureSuccessStatusCode();

        var preview = await previewResponse.Content.ReadFromJsonAsync<AccountCompanyPlanPreviewEnvelope>(JsonOptions);
        Assert.NotNull(preview);
        Assert.Equal("MASTER", preview!.TargetPlan.Code);
        Assert.True(preview.IsEligible);
    }

    private static async Task AssertProblemDetailsAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatusCode,
        string expectedCode)
    {
        Assert.Equal(expectedStatusCode, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)expectedStatusCode, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
    }

    private sealed record AccountCompanySubscriptionOverviewEnvelope(
        Guid CompanyId,
        string CompanyName,
        string CompanySlug,
        string PlanCode,
        AccountCompanySubscriptionPlanEnvelope CurrentPlan,
        IReadOnlyCollection<AccountCompanySubscriptionAddonEnvelope> ActiveAddons,
        IReadOnlyCollection<AccountCompanyEffectiveModuleEnvelope> EffectiveModules);

    private sealed record AccountCompanySubscriptionPlanEnvelope(
        Guid CommercialPlanId,
        string Code,
        string Name,
        string? Description,
        decimal BaseMonthlyFee,
        decimal PricePerActiveEmployee,
        int CurrentVersionNumber,
        string CurrencyCode,
        int ModuleCount,
        IReadOnlyCollection<string> ModuleKeys,
        bool IsCurrent);

    private sealed record AccountCompanySubscriptionAddonEnvelope(
        Guid CompanyAddonId,
        Guid CommercialAddonId,
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
        CompanyAddonStatus Status,
        int ModuleCount,
        IReadOnlyCollection<string> ModuleKeys);

    private sealed record AccountCompanyEffectiveModuleEnvelope(
        string ModuleKey,
        string DisplayName,
        string Description,
        string Source,
        bool GrantedByPlan,
        bool GrantedByAddon);

    private sealed record AccountCompanyPlanPreviewEnvelope(
        Guid CompanyId,
        AccountCompanySubscriptionPlanEnvelope CurrentPlan,
        AccountCompanySubscriptionPlanEnvelope TargetPlan,
        IReadOnlyCollection<string> AddedModuleKeys,
        IReadOnlyCollection<string> RemovedModuleKeys,
        IReadOnlyCollection<string> AddonDeactivationWarnings,
        bool IsEligible,
        IReadOnlyCollection<string> IneligibilityReasons);

    private sealed record AccountCompanyMarketplaceAddonEnvelope(
        Guid CommercialAddonId,
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
        int ModuleCount,
        IReadOnlyCollection<string> ModuleKeys,
        bool IsOwned,
        bool CanAcquire,
        string? BlockedReason);

    private sealed record AccountCompanyAddonPreviewEnvelope(
        Guid CompanyId,
        Guid CommercialAddonId,
        string AddonCode,
        string AddonName,
        SubscriptionAddonChangeAction Action,
        IReadOnlyCollection<string> AddedModuleKeys,
        IReadOnlyCollection<string> RemovedModuleKeys,
        bool IsEligible,
        IReadOnlyCollection<string> IneligibilityReasons,
        IReadOnlyCollection<string> Warnings);
}
