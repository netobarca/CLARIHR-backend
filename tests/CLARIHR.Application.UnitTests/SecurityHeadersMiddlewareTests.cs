using CoreSecurityHeadersMiddleware = CLARIHR.Api.Middleware.SecurityHeadersMiddleware;
using BackofficeSecurityHeadersMiddleware = CLARIHR.Backoffice.Api.Middleware.SecurityHeadersMiddleware;
using Microsoft.AspNetCore.Http;

namespace CLARIHR.Application.UnitTests;

public sealed class SecurityHeadersMiddlewareTests
{
    [Theory]
    [InlineData("/api/auth/login")]
    [InlineData("/api/v1/personnel-file-documents/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/file")]
    public async Task CoreApiSecurityHeaders_WhenPathStartsWithApi_ShouldDisableCaching(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var middleware = new CoreSecurityHeadersMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        AssertNoStoreHeaders(context);
    }

    [Theory]
    [InlineData("/api/platform/auth/login")]
    [InlineData("/api/platform/companies/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/subscriptions")]
    public async Task BackofficeSecurityHeaders_WhenPathStartsWithApi_ShouldDisableCaching(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var middleware = new BackofficeSecurityHeadersMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        AssertNoStoreHeaders(context);
    }

    private static void AssertNoStoreHeaders(HttpContext context)
    {
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        Assert.Equal("no-cache", context.Response.Headers.Pragma.ToString());
        Assert.Equal("0", context.Response.Headers.Expires.ToString());
    }
}
