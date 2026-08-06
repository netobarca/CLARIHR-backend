using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace CLARIHR.Api.Common.Authorization;

internal static class PermissionClaimEvaluator
{
    // Single claim type. These are put on the principal per request by EffectiveAccessClaimsTransformation,
    // not read out of the access token.
    private const string PermissionClaimType = "permission";

    public static bool HasAnyPermission(AuthorizationHandlerContext context, params string[] requiredCodes)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var grantedClaims = context.User.Claims
            .Where(static claim => claim.Type == PermissionClaimType)
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
