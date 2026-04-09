using CLARIHR.Application.Features.Provisioning.Common;
using CLARIHR.Domain.Companies;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Infrastructure;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CLARIHR.Api.IntegrationTests;

public sealed class RbacCatalogBackfillIntegrationTests(IntegrationTestWebApplicationFactory factory)
    : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private static readonly string[] CompanyUserPermissionCodes =
    [
        "COMPANYUSERS.READ",
        "COMPANYUSERS.ADMIN"
    ];

    [Fact]
    public async Task InfrastructureInitialization_ShouldRestoreAdminRoleAssignments_WhenPermissionsWereReinsertedManually()
    {
        var scenario = await factory.ResetDatabaseAsync();

        using (var setupScope = factory.Services.CreateScope())
        {
            var dbContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var adminRole = IamRole.Create(
                ProvisioningConstants.CompanyAdminRoleName,
                "System tenant administrator.",
                isSystemRole: true);
            adminRole.SetTenantId(scenario.TenantId);

            dbContext.IamRoles.Add(adminRole);
            await dbContext.SaveChangesAsync();
        }

        await factory.Services.InitializeInfrastructureAsync(NullLogger.Instance);

        using (var mutationScope = factory.Services.CreateScope())
        {
            var dbContext = mutationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var seededAdminRole = await LoadAdminRoleAsync(dbContext, scenario.TenantId);
            var seededCodes = seededAdminRole.PermissionAssignments
                .Select(static assignment => assignment.Permission.NormalizedCode)
                .ToArray();

            Assert.All(CompanyUserPermissionCodes, code => Assert.Contains(code, seededCodes));

            var companyUserPermissions = await dbContext.IamPermissions
                .Where(permission => permission.TenantId == scenario.TenantId &&
                                     CompanyUserPermissionCodes.Contains(permission.NormalizedCode))
                .ToListAsync();

            dbContext.IamPermissions.RemoveRange(companyUserPermissions);
            await dbContext.SaveChangesAsync();

            dbContext.IamPermissions.AddRange(CreateCompanyUserPermissions(scenario.TenantId));
            await dbContext.SaveChangesAsync();

            dbContext.ChangeTracker.Clear();

            var roleAfterManualInsert = await LoadAdminRoleAsync(dbContext, scenario.TenantId);
            var codesAfterManualInsert = roleAfterManualInsert.PermissionAssignments
                .Select(static assignment => assignment.Permission.NormalizedCode)
                .ToArray();

            Assert.All(CompanyUserPermissionCodes, code => Assert.DoesNotContain(code, codesAfterManualInsert));
        }

        await factory.Services.InitializeInfrastructureAsync(NullLogger.Instance);

        using var verificationScope = factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var repairedAdminRole = await LoadAdminRoleAsync(verificationDbContext, scenario.TenantId);
        var repairedCodes = repairedAdminRole.PermissionAssignments
            .Select(static assignment => assignment.Permission.NormalizedCode)
            .ToArray();
        var tenantPermissionCount = await verificationDbContext.IamPermissions
            .CountAsync(permission => permission.TenantId == scenario.TenantId);

        Assert.All(CompanyUserPermissionCodes, code => Assert.Contains(code, repairedCodes));
        Assert.Equal(tenantPermissionCount, repairedAdminRole.PermissionAssignments.Count);
    }

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

        var rolesClient = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.SecurityAdminUserId,
                scenario.TenantId,
                IdentityPermissionCodes.ManageAdministration));
        var customFieldsClient = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.SecurityAdminUserId,
                scenario.TenantId,
                PersonnelFilePermissionCodes.Read));

        var rolesDeniedResponse = await rolesClient.GetAsync("/api/iam/roles?pageNumber=1&pageSize=20");
        var customFieldsDeniedResponse = await customFieldsClient.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/personnel-custom-field-definitions");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, rolesDeniedResponse.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, customFieldsDeniedResponse.StatusCode);

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

        var rolesRecoveredResponse = await rolesClient.GetAsync("/api/iam/roles?pageNumber=1&pageSize=20");
        var customFieldsRecoveredResponse = await customFieldsClient.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/personnel-custom-field-definitions");

        Assert.Equal(System.Net.HttpStatusCode.OK, rolesRecoveredResponse.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, customFieldsRecoveredResponse.StatusCode);
    }

    private static async Task<IamRole> LoadAdminRoleAsync(ApplicationDbContext dbContext, Guid tenantId)
    {
        var normalizedAdminName = ProvisioningConstants.CompanyAdminRoleName.ToUpperInvariant();

        return await dbContext.IamRoles
            .Include(role => role.PermissionAssignments)
            .ThenInclude(assignment => assignment.Permission)
            .SingleAsync(role =>
                role.TenantId == tenantId &&
                role.IsSystemRole &&
                role.NormalizedName == normalizedAdminName);
    }

    private static IReadOnlyCollection<IamPermission> CreateCompanyUserPermissions(Guid tenantId) =>
        ProvisioningConstants.CompanyAdminPermissions
            .Where(definition => CompanyUserPermissionCodes.Contains(definition.Code.ToUpperInvariant()))
            .Select(definition =>
            {
                var permission = IamPermission.CreateScreenAction(
                    definition.Code,
                    definition.Name,
                    definition.Description,
                    definition.Module,
                    definition.Screen,
                    definition.Action);
                permission.SetTenantId(tenantId);
                return permission;
            })
            .ToArray();
}
