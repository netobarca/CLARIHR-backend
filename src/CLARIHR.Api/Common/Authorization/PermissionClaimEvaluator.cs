using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace CLARIHR.Api.Common.Authorization;

internal static class PermissionClaimEvaluator
{
    private const string PermissionClaimType = "permission";
    private const string PermissionsClaimType = "permissions";

    public static bool HasAnyPermission(AuthorizationHandlerContext context, params string[] requiredCodes)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var grantedClaims = context.User.Claims
            .Where(static claim => claim.Type is PermissionClaimType or PermissionsClaimType)
            .Select(static claim => claim.Value);

        var grantedSet = new HashSet<string>(grantedClaims, StringComparer.OrdinalIgnoreCase);

        foreach (var code in requiredCodes)
        {
            if (grantedSet.Contains(code))
            {
                return true;
            }
        }

        return false;
    }
}
