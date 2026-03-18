using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.IdentityAccess;

internal sealed class RbacCatalogBackfillService(ApplicationDbContext dbContext)
{
    public async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        await EnsureGlobalResourcesAsync(cancellationToken);
        await EnsureTenantMatrixPermissionsAsync(cancellationToken);
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

    private async Task EnsureTenantMatrixPermissionsAsync(CancellationToken cancellationToken)
    {
        var tenantIds = await dbContext.Companies
            .AsNoTracking()
            .Select(company => company.PublicId)
            .ToArrayAsync(cancellationToken);

        foreach (var tenantId in tenantIds)
        {
            await EnsureTenantMatrixPermissionsAsync(tenantId, cancellationToken);
        }
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
}
