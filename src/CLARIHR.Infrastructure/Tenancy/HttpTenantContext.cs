using System.Security.Claims;
using CLARIHR.Application.Abstractions.Tenancy;
using Microsoft.AspNetCore.Http;

namespace CLARIHR.Infrastructure.Tenancy;

internal sealed class HttpTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    private static readonly string[] TenantClaimTypes =
    [
        "tid",
        "tenantid",
        "http://schemas.microsoft.com/identity/claims/tenantid"
    ];

    public Guid? TenantId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user is null)
            {
                return null;
            }

            foreach (var claimType in TenantClaimTypes)
            {
                var tenantClaim = user.FindFirstValue(claimType);
                if (Guid.TryParse(tenantClaim, out var tenantId))
                {
                    return tenantId;
                }
            }

            var fallbackTenantClaim = user.Claims
                .FirstOrDefault(static claim =>
                    claim.Type.EndsWith("/tenantid", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(claim.Type, "tenant_id", StringComparison.OrdinalIgnoreCase))
                ?.Value;

            return Guid.TryParse(fallbackTenantClaim, out var fallbackTenantId)
                ? fallbackTenantId
                : null;
        }
    }
}
