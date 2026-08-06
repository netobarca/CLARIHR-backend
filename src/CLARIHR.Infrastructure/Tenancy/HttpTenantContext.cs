using System.Security.Claims;
using CLARIHR.Application.Abstractions.Tenancy;
using Microsoft.AspNetCore.Http;

namespace CLARIHR.Infrastructure.Tenancy;

internal sealed class HttpTenantContext(
    IHttpContextAccessor httpContextAccessor,
    AmbientTenantContext ambientTenantContext) : ITenantContext
{
    public Guid? TenantId
    {
        get
        {
            if (ambientTenantContext.TenantId.HasValue)
            {
                return ambientTenantContext.TenantId;
            }

            var user = httpContextAccessor.HttpContext?.User;
            if (user is null)
            {
                return null;
            }

            foreach (var claimType in TenantClaimTypes.All)
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
