using System.Security.Claims;
using CLARIHR.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Http;

namespace CLARIHR.Application.UnitTests;

public sealed class HttpTenantContextTests
{
    [Fact]
    public void TenantId_ShouldResolve_FromTidClaim()
    {
        var tenantId = Guid.NewGuid();
        var context = new HttpTenantContext(CreateHttpContextAccessor(new Claim("tid", tenantId.ToString())));

        var result = context.TenantId;

        Assert.Equal(tenantId, result);
    }

    [Fact]
    public void TenantId_ShouldResolve_FromMappedTenantIdClaim()
    {
        var tenantId = Guid.NewGuid();
        var context = new HttpTenantContext(CreateHttpContextAccessor(
            new Claim("http://schemas.microsoft.com/identity/claims/tenantid", tenantId.ToString())));

        var result = context.TenantId;

        Assert.Equal(tenantId, result);
    }

    [Fact]
    public void TenantId_ShouldReturnNull_WhenClaimIsMissingOrInvalid()
    {
        var context = new HttpTenantContext(CreateHttpContextAccessor(new Claim("tid", "invalid-guid")));

        var result = context.TenantId;

        Assert.Null(result);
    }

    private static IHttpContextAccessor CreateHttpContextAccessor(params Claim[] claims)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
        };

        return new HttpContextAccessor
        {
            HttpContext = httpContext
        };
    }
}
