using CLARIHR.Application.Abstractions.Localization;
using CLARIHR.Infrastructure.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Security.Claims;

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
            var originalCulture = CultureInfo.CurrentCulture;
            var originalUiCulture = CultureInfo.CurrentUICulture;
            var preferredLanguage = context.User.FindFirstValue(RequestLanguageResolver.LanguageClaimType);
            var acceptLanguageHeader = context.Request.Headers.AcceptLanguage.ToString();
            var culture = RequestLanguageResolver.ResolveCulture(preferredLanguage, acceptLanguageHeader);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            try
            {
                // Extract enriched request context for structured logging.
                var tenantId = RequestIdentityContextResolver.ResolveTenantId(context.User);
                var userId = RequestIdentityContextResolver.ResolveUserId(context.User);
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
                var localizer = context.RequestServices.GetService<IBackendMessageLocalizer>();
                var title = localizer?.Localize("common.unexpected", "Unexpected error") ?? "Unexpected error";
                var detail = hostEnvironment.IsDevelopment()
                    ? exception.Message
                    : localizer?.Localize("common.unexpected", "An unexpected error occurred.") ?? "An unexpected error occurred.";

                var problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = title,
                    Detail = detail,
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
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUiCulture;
            }
        }
    }
}
