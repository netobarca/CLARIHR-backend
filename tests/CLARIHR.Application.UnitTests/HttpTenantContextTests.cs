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
        var context = CreateTenantContext(new Claim("tid", tenantId.ToString()));

        var result = context.TenantId;

        Assert.Equal(tenantId, result);
    }

    [Fact]
    public void TenantId_ShouldResolve_FromMappedTenantIdClaim()
    {
        var tenantId = Guid.NewGuid();
        var context = CreateTenantContext(new Claim("http://schemas.microsoft.com/identity/claims/tenantid", tenantId.ToString()));

        var result = context.TenantId;

        Assert.Equal(tenantId, result);
    }

    [Fact]
    public void TenantId_ShouldReturnNull_WhenClaimIsMissingOrInvalid()
    {
        var context = CreateTenantContext(new Claim("tid", "invalid-guid"));

        var result = context.TenantId;

        Assert.Null(result);
    }

    [Fact]
    public void TenantId_ShouldPreferAmbientTenant_WhenPresent()
    {
        var claimTenantId = Guid.NewGuid();
        var ambientTenantId = Guid.NewGuid();
        var ambientTenantContext = new AmbientTenantContext();
        using var _ = ambientTenantContext.Push(ambientTenantId);
        var context = new HttpTenantContext(
            CreateHttpContextAccessor(new Claim("tid", claimTenantId.ToString())),
            ambientTenantContext);

        var result = context.TenantId;

        Assert.Equal(ambientTenantId, result);
    }

    private static HttpTenantContext CreateTenantContext(params Claim[] claims) =>
        new(CreateHttpContextAccessor(claims), new AmbientTenantContext());

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
