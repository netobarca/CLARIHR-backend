using CLARIHR.Application.Features.JobProfileCatalogTypes;
using CLARIHR.Application.Features.Provisioning.Common;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.LegalRepresentatives;
using CLARIHR.Infrastructure.CatalogTypes;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

public sealed class MigrationSeedingIntegrationTests(IntegrationTestWebApplicationFactory factory)
    : IClassFixture<IntegrationTestWebApplicationFactory>
{
    [Fact]
    public async Task ResetDatabaseAsync_ShouldApplyMigrationsAndSeedGlobalCatalogs()
    {
        _ = await factory.ResetDatabaseAsync();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        Assert.NotEmpty(appliedMigrations);

        var seededPlanEntitlements = await dbContext.PlanEntitlements
            .AsNoTracking()
            .OrderBy(entitlement => entitlement.PlanCode)
            .ThenBy(entitlement => entitlement.ModuleKey)
            .Select(entitlement => new { entitlement.PlanCode, entitlement.ModuleKey, entitlement.IsEnabled })
            .ToListAsync();
        Assert.Equal(
            ProvisioningConstants.FreePlanEnabledModules.Length + ProvisioningConstants.MasterPlanEnabledModules.Length,
            seededPlanEntitlements.Count);

        var freePlanEntitlements = seededPlanEntitlements
            .Where(entitlement => entitlement.PlanCode == ProvisioningConstants.FreePlanCode)
            .ToArray();
        Assert.Equal(ProvisioningConstants.FreePlanEnabledModules.Length, freePlanEntitlements.Length);
        Assert.All(
            ProvisioningConstants.FreePlanEnabledModules,
            moduleKey => Assert.Contains(
                freePlanEntitlements,
                entitlement => entitlement.PlanCode == ProvisioningConstants.FreePlanCode &&
                               entitlement.ModuleKey == moduleKey &&
                               entitlement.IsEnabled));

        var masterPlanEntitlements = seededPlanEntitlements
            .Where(entitlement => entitlement.PlanCode == ProvisioningConstants.MasterPlanCode)
            .ToArray();
        Assert.Equal(ProvisioningConstants.MasterPlanEnabledModules.Length, masterPlanEntitlements.Length);
        Assert.All(
            ProvisioningConstants.MasterPlanEnabledModules,
            moduleKey => Assert.Contains(
                masterPlanEntitlements,
                entitlement => entitlement.PlanCode == ProvisioningConstants.MasterPlanCode &&
                               entitlement.ModuleKey == moduleKey &&
                               entitlement.IsEnabled));
        Assert.Equal(
            freePlanEntitlements.Select(entitlement => entitlement.ModuleKey).OrderBy(static key => key, StringComparer.Ordinal),
            masterPlanEntitlements.Select(entitlement => entitlement.ModuleKey).OrderBy(static key => key, StringComparer.Ordinal));

        var freeCommercialPlan = await dbContext.CommercialPlans
            .AsNoTracking()
            .SingleAsync(plan => plan.Code == ProvisioningConstants.FreePlanCode);
        Assert.Equal(CommercialPlanStatus.Active, freeCommercialPlan.Status);
        Assert.True(freeCommercialPlan.IsSystemPlan);
        Assert.Equal(0m, freeCommercialPlan.BaseMonthlyFee);
        Assert.Equal(0m, freeCommercialPlan.PricePerActiveEmployee);

        var masterCommercialPlan = await dbContext.CommercialPlans
            .AsNoTracking()
            .SingleAsync(plan => plan.Code == ProvisioningConstants.MasterPlanCode);
        Assert.Equal(CommercialPlanStatus.Active, masterCommercialPlan.Status);
        Assert.True(masterCommercialPlan.IsSystemPlan);
        Assert.Equal(0m, masterCommercialPlan.BaseMonthlyFee);
        Assert.Equal(0m, masterCommercialPlan.PricePerActiveEmployee);

        var freeCommercialPlanLimits = await dbContext.CommercialPlanLimits
            .AsNoTracking()
            .Where(limit => limit.CommercialPlanId == freeCommercialPlan.Id)
            .ToListAsync();
        Assert.Empty(freeCommercialPlanLimits);

        var masterCommercialPlanLimits = await dbContext.CommercialPlanLimits
            .AsNoTracking()
            .Where(limit => limit.CommercialPlanId == masterCommercialPlan.Id)
            .ToListAsync();
        Assert.Empty(masterCommercialPlanLimits);

        var legacyPlanAliases = ProvisioningConstants.EnterpriseLegacyPlanAliases
            .Select(alias => alias.ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);
        var legacyPlans = await dbContext.CommercialPlans
            .AsNoTracking()
            .Where(plan => legacyPlanAliases.Contains(plan.NormalizedCode))
            .ToListAsync();
        Assert.Empty(legacyPlans);

        var seededDocumentTypes = await dbContext.IdentificationTypeCatalogItems
            .AsNoTracking()
            .Where(item => item.CountryCode == "SV")
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Code)
            .Select(item => item.Code)
            .ToListAsync();
        Assert.Contains("DUI", seededDocumentTypes);
        Assert.Contains("NIT", seededDocumentTypes);
        Assert.Contains("PASSPORT", seededDocumentTypes);

        var seededPositionTitles = await dbContext.LegalRepresentativePositionTitleCatalogItems
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .Select(item => item.Code)
            .ToListAsync();
        Assert.Equal(LegalRepresentativePositionTitleCatalog.Items.Count, seededPositionTitles.Count);
        Assert.All(LegalRepresentativePositionTitleCatalog.Items, item => Assert.Contains(item.Code, seededPositionTitles));

        var seededRepresentationTypes = await dbContext.LegalRepresentativeRepresentationTypeCatalogItems
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .Select(item => item.Code)
            .ToListAsync();
        Assert.Equal(LegalRepresentativeRepresentationTypeCatalog.Items.Count, seededRepresentationTypes.Count);
        Assert.All(LegalRepresentativeRepresentationTypeCatalog.Items, item => Assert.Contains(item.Code, seededRepresentationTypes));
    }

    // §D6 (doc technical-debt/07): the catalog-type registry is populated ONLY by
    // CatalogTypeDescriptorSeedService at app startup — the migration/reset path
    // does not seed it. If EnsureSeededAsync regresses, the catalog manifest
    // silently degrades (every field isActive:false → frontend hides all catalog
    // fields with no error). This exercises EnsureSeededAsync directly against real
    // Postgres: it must fully populate the registry, be idempotent, and its new
    // post-seed fail-fast must pass on a correct seed (it throws otherwise).
    [Fact]
    public async Task EnsureSeededAsync_ShouldPopulateEveryCanonicalType_AndBeIdempotent()
    {
        _ = await factory.ResetDatabaseAsync();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var seedService = scope.ServiceProvider
            .GetRequiredService<CatalogTypeDescriptorSeedService>();

        // Precondition: the reset path does NOT seed catalog types — this is the
        // exact silent-degradation state §D6 guards against.
        Assert.Empty(await dbContext.CatalogTypeDescriptors.AsNoTracking().ToListAsync());

        // First run seeds everything; the post-seed fail-fast must not throw.
        await seedService.EnsureSeededAsync(CancellationToken.None);

        var afterFirst = await dbContext.CatalogTypeDescriptors
            .AsNoTracking()
            .Select(item => item.NormalizedCode)
            .ToListAsync();
        Assert.Equal(JobProfileCatalogBindingMap.CanonicalTypes.Count, afterFirst.Count);
        Assert.All(
            JobProfileCatalogBindingMap.CanonicalTypes,
            definition => Assert.Contains(
                definition.RegistryCode.Trim().ToUpperInvariant(),
                afterFirst));

        // Second run must be idempotent: no duplicates, fail-fast still passes.
        await seedService.EnsureSeededAsync(CancellationToken.None);

        var afterSecond = await dbContext.CatalogTypeDescriptors
            .AsNoTracking()
            .Select(item => item.NormalizedCode)
            .ToListAsync();
        Assert.Equal(JobProfileCatalogBindingMap.CanonicalTypes.Count, afterSecond.Count);
    }
}
