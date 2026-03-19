using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Provisioning.Common;
using CLARIHR.Domain.LegalRepresentatives;
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
            .OrderBy(entitlement => entitlement.ModuleKey)
            .Select(entitlement => new { entitlement.PlanCode, entitlement.ModuleKey, entitlement.IsEnabled })
            .ToListAsync();
        Assert.Equal(ProvisioningConstants.FreePlanEnabledModules.Length, seededPlanEntitlements.Count);
        Assert.All(
            ProvisioningConstants.FreePlanEnabledModules,
            moduleKey => Assert.Contains(
                seededPlanEntitlements,
                entitlement => entitlement.PlanCode == ProvisioningConstants.FreePlanCode &&
                               entitlement.ModuleKey == moduleKey &&
                               entitlement.IsEnabled));

        var seededResources = await dbContext.RbacResources
            .AsNoTracking()
            .OrderBy(resource => resource.ResourceKey)
            .Select(resource => resource.ResourceKey)
            .ToListAsync();
        Assert.Equal(PermissionMatrixCatalog.Screens.Count, seededResources.Count);
        Assert.All(PermissionMatrixCatalog.Screens, screen => Assert.Contains(screen.ResourceKey, seededResources));

        var seededFieldCatalog = await dbContext.FieldCatalogEntries
            .AsNoTracking()
            .OrderBy(entry => entry.NormalizedFieldKey)
            .Select(entry => entry.FieldKey)
            .ToListAsync();
        Assert.Equal(FieldCatalogRegistry.Definitions.Count, seededFieldCatalog.Count);
        Assert.All(FieldCatalogRegistry.Definitions, definition => Assert.Contains(definition.FieldKey, seededFieldCatalog));

        var seededDocumentTypes = await dbContext.LegalRepresentativeDocumentTypeCatalogItems
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .Select(item => item.Code)
            .ToListAsync();
        Assert.Equal(LegalRepresentativeDocumentTypeCatalog.Items.Count, seededDocumentTypes.Count);
        Assert.All(LegalRepresentativeDocumentTypeCatalog.Items, item => Assert.Contains(item.Code, seededDocumentTypes));

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
}
