using System.Security.Claims;
using CLARIHR.Application.Abstractions.Tenancy;
using Microsoft.AspNetCore.Http;

namespace CLARIHR.Infrastructure.Tenancy;

internal sealed class HttpTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public Guid? TenantId
    {
        get
        {
            var tenantClaim = httpContextAccessor.HttpContext?.User.FindFirstValue("tid");

            return Guid.TryParse(tenantClaim, out var tenantId)
                ? tenantId
                : null;
        }
    }
}
