namespace CLARIHR.Application.Features.IdentityAccess.Common;

public static class RbacAuthorizationEvaluator
{
    public static bool IsAllowed(
        IEnumerable<string> grantedPermissionCodes,
        RbacPermissionScreen screen,
        RbacPermissionAction action)
    {
        var normalizedCodes = Normalize(grantedPermissionCodes);
        var definition = PermissionMatrixCatalog.Get(screen);

        if (normalizedCodes.Contains(IdentityPermissionCodes.ManageAdministration.ToUpperInvariant()) ||
            normalizedCodes.Contains(definition.ManagePermissionCode.ToUpperInvariant()))
        {
            return true;
        }

        var accessCode = PermissionMatrixCatalog.BuildPermissionCode(screen, RbacPermissionAction.Access).ToUpperInvariant();
        if (!normalizedCodes.Contains(accessCode))
        {
            return false;
        }

        return action == RbacPermissionAction.Access ||
               normalizedCodes.Contains(PermissionMatrixCatalog.BuildPermissionCode(screen, action).ToUpperInvariant());
    }

    public static bool IsRbacSecurityAdministrator(IEnumerable<string> grantedPermissionCodes)
    {
        var normalizedCodes = Normalize(grantedPermissionCodes);
        if (normalizedCodes.Contains(IdentityPermissionCodes.ManageAdministration.ToUpperInvariant()))
        {
            return true;
        }

        var canManageRoles = normalizedCodes.Contains(IdentityPermissionCodes.ManageRoles.ToUpperInvariant()) ||
                             HasScreenUpdateAccess(normalizedCodes, RbacPermissionScreen.Roles);
        var canManagePermissions = normalizedCodes.Contains(IdentityPermissionCodes.ManagePermissions.ToUpperInvariant()) ||
                                   HasScreenUpdateAccess(normalizedCodes, RbacPermissionScreen.Permissions);

        return canManageRoles && canManagePermissions;
    }

    private static bool HasScreenUpdateAccess(IReadOnlySet<string> normalizedCodes, RbacPermissionScreen screen)
    {
        var accessCode = PermissionMatrixCatalog.BuildPermissionCode(screen, RbacPermissionAction.Access).ToUpperInvariant();
        var updateCode = PermissionMatrixCatalog.BuildPermissionCode(screen, RbacPermissionAction.Update).ToUpperInvariant();

        return normalizedCodes.Contains(accessCode) && normalizedCodes.Contains(updateCode);
    }

    private static HashSet<string> Normalize(IEnumerable<string> grantedPermissionCodes) =>
        grantedPermissionCodes
            .Where(static code => !string.IsNullOrWhiteSpace(code))
            .Select(static code => code.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);
}
