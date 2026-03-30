using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Provisioning.Common;
using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.IdentityAccess;

internal sealed class RbacCatalogBackfillService(ApplicationDbContext dbContext)
{
    public async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        await EnsureGlobalResourcesAsync(cancellationToken);
        var tenantIds = await dbContext.Companies
            .AsNoTracking()
            .Select(company => company.PublicId)
            .ToArrayAsync(cancellationToken);

        foreach (var tenantId in tenantIds)
        {
            await EnsureTenantMatrixPermissionsAsync(tenantId, cancellationToken);
            await EnsureTenantAdminPermissionsAsync(tenantId, cancellationToken);
            await EnsureTenantSystemAdminRoleAssignmentsAsync(tenantId, cancellationToken);
        }
    }

    private async Task EnsureGlobalResourcesAsync(CancellationToken cancellationToken)
    {
        var existingKeys = new HashSet<string>(
            await dbContext.RbacResources
            .AsNoTracking()
            .Select(resource => resource.NormalizedResourceKey)
            .ToListAsync(cancellationToken),
            StringComparer.Ordinal);

        var missingResources = PermissionMatrixCatalog.Screens
            .Where(screen => !existingKeys.Contains(screen.ResourceKey.ToUpperInvariant()))
            .Select(screen => RbacResource.Create(screen.ResourceKey, screen.DisplayName))
            .ToArray();

        if (missingResources.Length == 0)
        {
            return;
        }

        dbContext.RbacResources.AddRange(missingResources);
        _ = await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureTenantMatrixPermissionsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var expectedCodes = PermissionMatrixCatalog.AllMatrixCodes
            .Select(static code => code.ToUpperInvariant())
            .ToArray();

        var existingCodes = new HashSet<string>(
            await dbContext.IamPermissions
            .AsNoTracking()
            .Where(permission => permission.TenantId == tenantId && expectedCodes.Contains(permission.NormalizedCode))
            .Select(permission => permission.NormalizedCode)
            .ToListAsync(cancellationToken),
            StringComparer.Ordinal);

        var missingPermissions = PermissionMatrixCatalog.Screens
            .SelectMany(static screen => screen.SupportedActions.Select(action => (Screen: screen.ScreenKey, Action: action)))
            .Where(definition => !existingCodes.Contains(PermissionMatrixCatalog.BuildPermissionCode(definition.Screen, definition.Action).ToUpperInvariant()))
            .Select(definition =>
            {
                var permission = PermissionMatrixCatalog.CreatePermission(definition.Screen, definition.Action);
                permission.SetTenantId(tenantId);
                return permission;
            })
            .ToArray();

        if (missingPermissions.Length == 0)
        {
            return;
        }

        dbContext.IamPermissions.AddRange(missingPermissions);
        _ = await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureTenantAdminPermissionsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var allExpectedCodes = ProvisioningConstants.CompanyAdminPermissions
            .Select(static definition => definition.Code.ToUpperInvariant())
            .ToArray();

        var existingCodes = new HashSet<string>(
            await dbContext.IamPermissions
                .AsNoTracking()
                .Where(permission => permission.TenantId == tenantId && allExpectedCodes.Contains(permission.NormalizedCode))
                .Select(permission => permission.NormalizedCode)
                .ToListAsync(cancellationToken),
            StringComparer.Ordinal);

        var missingDefinitions = ProvisioningConstants.CompanyAdminPermissions
            .Where(definition => !existingCodes.Contains(definition.Code.ToUpperInvariant()))
            .ToArray();

        if (missingDefinitions.Length == 0)
        {
            return;
        }

        var newPermissions = new List<IamPermission>();
        foreach (var definition in missingDefinitions)
        {
            var permission = IamPermission.CreateScreenAction(
                definition.Code,
                definition.Name,
                definition.Description,
                definition.Module,
                definition.Screen,
                definition.Action);
            permission.SetTenantId(tenantId);
            newPermissions.Add(permission);
        }

        dbContext.IamPermissions.AddRange(newPermissions);
        _ = await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureTenantSystemAdminRoleAssignmentsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var normalizedAdminName = ProvisioningConstants.CompanyAdminRoleName.ToUpperInvariant();

        var adminRole = await dbContext.IamRoles
            .Include(role => role.PermissionAssignments)
            .FirstOrDefaultAsync(
                role => role.TenantId == tenantId && role.IsSystemRole && role.NormalizedName == normalizedAdminName,
                cancellationToken);

        if (adminRole is null)
        {
            return;
        }

        var allTenantPermissions = await dbContext.IamPermissions
            .AsNoTracking()
            .Where(permission => permission.TenantId == tenantId)
            .ToArrayAsync(cancellationToken);

        var assignedPermissionIds = adminRole.PermissionAssignments
            .Select(static assignment => assignment.PermissionId)
            .ToHashSet();
        var tenantPermissionIds = allTenantPermissions
            .Select(static permission => permission.Id)
            .ToHashSet();

        if (assignedPermissionIds.SetEquals(tenantPermissionIds))
        {
            return;
        }

        adminRole.SyncPermissions(allTenantPermissions);

        foreach (var assignment in adminRole.PermissionAssignments)
        {
            assignment.SetTenantId(tenantId);
        }

        _ = await dbContext.SaveChangesAsync(cancellationToken);
    }
}
