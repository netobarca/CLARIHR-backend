namespace CLARIHR.Api.Middleware;

internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";

        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers["Cache-Control"] = "no-store";
            context.Response.Headers["Pragma"] = "no-cache";
            context.Response.Headers["Expires"] = "0";

            // Defense-in-depth: API representations are bearer-token bound and can be
            // tenant-specific (e.g. catalog-manifest resolves {companyId} per caller).
            // no-store already forbids storage; Vary keeps even a non-conformant cache
            // that ignores no-store from serving one principal's body to another.
            context.Response.Headers["Vary"] = "Authorization";
        }

        await next(context);
    }
}
