using System.Security.Claims;

namespace CLARIHR.Api.Middleware;

internal static class RequestIdentityContextResolver
{
    private static readonly string[] TenantClaimTypes =
    [
        "tid",
        "tenantid",
        "http://schemas.microsoft.com/identity/claims/tenantid"
    ];

    private static readonly string[] UserClaimTypes =
    [
        ClaimTypes.NameIdentifier,
        "sub",
        "uid"
    ];

    public static string ResolveTenantId(ClaimsPrincipal? user, string fallback = "unknown")
    {
        var tenantValue = FindFirstClaimValue(user, TenantClaimTypes);
        if (!string.IsNullOrWhiteSpace(tenantValue))
        {
            return tenantValue;
        }

        var inferredTenant = user?.Claims
            .FirstOrDefault(static claim =>
                claim.Type.EndsWith("/tenantid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(claim.Type, "tenant_id", StringComparison.OrdinalIgnoreCase))
            ?.Value;

        return string.IsNullOrWhiteSpace(inferredTenant) ? fallback : inferredTenant;
    }

    public static Guid? ResolveTenantGuid(ClaimsPrincipal? user)
    {
        var tenantValue = ResolveTenantId(user, fallback: string.Empty);
        return Guid.TryParse(tenantValue, out var tenantId)
            ? tenantId
            : null;
    }

    public static string ResolveUserId(ClaimsPrincipal? user, string fallback = "anonymous")
    {
        var userValue = FindFirstClaimValue(user, UserClaimTypes);
        if (!string.IsNullOrWhiteSpace(userValue))
        {
            return userValue;
        }

        var inferredUserId = user?.Claims
            .FirstOrDefault(static claim =>
                claim.Type.EndsWith("/nameidentifier", StringComparison.OrdinalIgnoreCase))
            ?.Value;

        return string.IsNullOrWhiteSpace(inferredUserId) ? fallback : inferredUserId;
    }

    private static string? FindFirstClaimValue(ClaimsPrincipal? user, IReadOnlyCollection<string> claimTypes)
    {
        if (user is null)
        {
            return null;
        }

        foreach (var claimType in claimTypes)
        {
            var claimValue = user.FindFirstValue(claimType);
            if (!string.IsNullOrWhiteSpace(claimValue))
            {
                return claimValue;
            }
        }

        return null;
    }
}
