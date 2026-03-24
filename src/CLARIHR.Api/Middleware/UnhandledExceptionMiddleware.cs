using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Middleware;

/// <summary>
/// Captures unhandled exceptions and writes enriched error logs.
/// Includes tenant, user, remote IP, and trace context for troubleshooting.
/// </summary>
internal sealed class UnhandledExceptionMiddleware(
    RequestDelegate next,
    IProblemDetailsService problemDetailsService,
    IHostEnvironment hostEnvironment,
    ILogger<UnhandledExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            // Extract enriched request context for structured logging.
            var tenantId = context.User.FindFirst("tid")?.Value ?? context.User.FindFirst("tenantid")?.Value ?? "unknown";
            var userId = context.User.FindFirst("sub")?.Value ?? context.User.FindFirst("uid")?.Value ?? "anonymous";
            var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            using (logger.BeginScope(new Dictionary<string, object>
            {
                { "TenantId", tenantId },
                { "UserId", userId },
                { "RemoteIp", remoteIp },
                { "TraceId", context.TraceIdentifier },
                { "Method", context.Request.Method },
                { "Path", context.Request.Path.Value ?? "/" }
            }))
            {
                logger.LogError(
                    exception,
                    "Unhandled exception processing request {Method} {Path} | Tenant: {TenantId} | User: {UserId} | TraceId: {TraceIdentifier}",
                    context.Request.Method,
                    context.Request.Path.Value,
                    tenantId,
                    userId,
                    context.TraceIdentifier);
            }

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Unexpected error",
                Detail = hostEnvironment.IsDevelopment()
                    ? exception.Message
                    : "An unexpected error occurred.",
                Type = "https://httpstatuses.com/500"
            };

            problemDetails.Extensions["code"] = "common.unexpected";
            problemDetails.Extensions["traceId"] = context.TraceIdentifier;

            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = problemDetails
            });
        }
    }
}
