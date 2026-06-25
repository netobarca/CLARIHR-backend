using CLARIHR.Application.Features.Provisioning.Common;
using CLARIHR.Domain.Companies;
using CLARIHR.Infrastructure;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CLARIHR.Api.IntegrationTests;

public sealed class RbacCatalogBackfillIntegrationTests(IntegrationTestWebApplicationFactory factory)
    : IClassFixture<IntegrationTestWebApplicationFactory>
{
    [Fact]
    public async Task InfrastructureInitialization_ShouldRestoreFreePlanModules_ForLegacyFreeTenants()
    {
        var scenario = await factory.ResetDatabaseAsync();

        using (var mutationScope = factory.Services.CreateScope())
        {
            var dbContext = mutationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var freePlan = await dbContext.CommercialPlans
                .Include(plan => plan.Entitlements)
                .SingleAsync(plan => plan.NormalizedCode == ProvisioningConstants.FreePlanCode);

            var entitlementsToRemove = freePlan.Entitlements
                .Where(entitlement =>
                    entitlement.ModuleKey == CommercialModuleKeys.Rbac ||
                    entitlement.ModuleKey == CommercialModuleKeys.PersonnelFiles)
                .ToArray();

            dbContext.PlanEntitlements.RemoveRange(entitlementsToRemove);
            await dbContext.SaveChangesAsync();
        }

        using (var precheckScope = factory.Services.CreateScope())
        {
            var dbContext = precheckScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var freePlanModulesBefore = await dbContext.PlanEntitlements
                .AsNoTracking()
                .Where(entitlement =>
                    entitlement.PlanCode == ProvisioningConstants.FreePlanCode &&
                    entitlement.IsEnabled)
                .Select(entitlement => entitlement.ModuleKey)
                .ToListAsync();

            Assert.DoesNotContain(CommercialModuleKeys.Rbac, freePlanModulesBefore);
            Assert.DoesNotContain(CommercialModuleKeys.PersonnelFiles, freePlanModulesBefore);
        }

        await factory.Services.InitializeInfrastructureAsync(NullLogger.Instance);

        using (var verificationScope = factory.Services.CreateScope())
        {
            var dbContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var freePlanModules = await dbContext.PlanEntitlements
                .AsNoTracking()
                .Where(entitlement =>
                    entitlement.PlanCode == ProvisioningConstants.FreePlanCode &&
                    entitlement.IsEnabled)
                .Select(entitlement => entitlement.ModuleKey)
                .ToListAsync();

            Assert.Equal(
                CommercialModuleCatalog.DefaultFreeModuleKeys.OrderBy(static moduleKey => moduleKey, StringComparer.Ordinal),
                freePlanModules.OrderBy(static moduleKey => moduleKey, StringComparer.Ordinal));
        }
    }
}
