using System.Diagnostics;

namespace CLARIHR.Api.Middleware;

/// <summary>
/// Logs each HTTP request with enriched operational context.
/// Captures method, path, status code, duration, trace identifier, tenant, user, and remote IP.
/// </summary>
internal sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();

            // Extract enriched request context for structured logging.
            var tenantId = RequestIdentityContextResolver.ResolveTenantId(context.User);
            var userId = RequestIdentityContextResolver.ResolveUserId(context.User);
            var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            using (logger.BeginScope(new Dictionary<string, object>
            {
                { "TenantId", tenantId },
                { "UserId", userId },
                { "RemoteIp", remoteIp },
                { "TraceId", context.TraceIdentifier }
            }))
            {
                logger.LogInformation(
                    "HTTP {Method} {Path} -> {StatusCode} in {ElapsedMs}ms | Tenant: {TenantId} | User: {UserId}",
                    context.Request.Method,
                    context.Request.Path.Value,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds,
                    tenantId,
                    userId);
            }
        }
    }
}
