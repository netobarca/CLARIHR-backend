namespace CLARIHR.Api.Middleware;

internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        // AU-10: clickjacking defense-in-depth (the API is not meant to be framed).
        context.Response.Headers["X-Frame-Options"] = "DENY";

        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers["Cache-Control"] = "no-store";
            context.Response.Headers["Pragma"] = "no-cache";
            context.Response.Headers["Expires"] = "0";

            // AU-10: the API serves only JSON (no executable content); a locked-down CSP is pure
            // defense-in-depth and does not affect the Swagger UI (served under /swagger, not /api).
            context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";

            // Defense-in-depth: API representations are bearer-token bound and can be
            // tenant-specific (e.g. catalog-manifest resolves {companyId} per caller).
            // no-store already forbids storage; Vary keeps even a non-conformant cache
            // that ignores no-store from serving one principal's body to another.
            context.Response.Headers["Vary"] = "Authorization";
        }

        await next(context);
    }
}
