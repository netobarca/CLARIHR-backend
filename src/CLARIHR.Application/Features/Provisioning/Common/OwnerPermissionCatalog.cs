using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.IdentityAccess;

namespace CLARIHR.Application.Features.Provisioning.Common;

public static class OwnerPermissionCatalog
{
    public static IReadOnlyCollection<string> DefaultOwnerPermissionCodes =>
        ProvisioningConstants.CompanyAdminPermissions
            .Select(static definition => definition.Code)
            .Concat(PermissionMatrixCatalog.AllMatrixCodes)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static IReadOnlyList<IamPermission> CreateDefaultOwnerPermissions(Guid tenantId)
    {
        var adminPermissions = ProvisioningConstants.CompanyAdminPermissions
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
            });

        var matrixPermissions = PermissionMatrixCatalog.Screens
            .SelectMany(static definition => definition.SupportedActions.Select(action => (Screen: definition.ScreenKey, Action: action)))
            .Select(definition =>
            {
                var permission = PermissionMatrixCatalog.CreatePermission(definition.Screen, definition.Action);
                permission.SetTenantId(tenantId);
                return permission;
            });

        return adminPermissions
            .Concat(matrixPermissions)
            .ToArray();
    }
}
