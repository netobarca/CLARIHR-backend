namespace CLARIHR.Backoffice.Api.Middleware;

internal sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();

            var tenantId = context.User.FindFirst("tid")?.Value ?? context.User.FindFirst("tenantid")?.Value ?? "unknown";
            var userId = context.User.FindFirst("sub")?.Value ?? context.User.FindFirst("uid")?.Value ?? "anonymous";
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
