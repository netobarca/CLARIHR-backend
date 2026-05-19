using CoreSecurityHeadersMiddleware = CLARIHR.Api.Middleware.SecurityHeadersMiddleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Drift-proof guardrail for the §D3 won't-fix decision (doc
/// <c>technical-debt/07-job-profile-catalog-manifest-audit-2026-05-18.md</c>):
/// <c>GET /api/v1/job-profiles/catalog-manifest</c> deliberately has NO ETag/304.
///
/// The rationale holds only while the global <c>Cache-Control: no-store</c>
/// (SecurityHeadersMiddleware) covers the manifest path: under no-store the client
/// never stores a copy to revalidate, so a 304 path would be unreachable and an
/// ETag would be dead weight. This test pins that precondition to the real route.
/// If the global no-store ever stops covering this endpoint, this guardrail goes
/// red — forcing a deliberate re-evaluation of §D3 instead of letting an auditor
/// re-flag the missing ETag as fresh debt every pass.
/// </summary>
public sealed class CatalogManifestConditionalCachingDecisionTests
{
    private const string CatalogManifestPath = "/api/v1/job-profiles/catalog-manifest";

    [Fact]
    public async Task CatalogManifest_ShouldStayNoStore_SoTheNoETagDecisionStaysValid()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = CatalogManifestPath;
        var middleware = new CoreSecurityHeadersMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        Assert.Equal(
            "Authorization",
            context.Response.Headers.Vary.ToString());
        // If the asserts above ever fail, the §D3 "no ETag/304" decision is no
        // longer self-justifying: revisit conditional caching for the manifest
        // (weak ETag = hash(registry projection + tenantId)) before relaxing this.
    }
}
