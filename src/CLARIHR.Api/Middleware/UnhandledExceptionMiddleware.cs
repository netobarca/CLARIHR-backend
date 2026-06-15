using CLARIHR.Application.Abstractions.Localization;
using CLARIHR.Infrastructure.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    // PostgreSQL SQLSTATE for foreign_key_violation — a referential conflict (e.g. deleting a row
    // still referenced elsewhere) that should degrade to 409 instead of an unexpected 500.
    private const string PostgresForeignKeyViolationSqlState = "23503";

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

                var isConcurrencyConflict = exception is DbUpdateConcurrencyException;
                // Safety net: a foreign-key violation not pre-checked as a domain rule (e.g. deleting a
                // catalog item still referenced elsewhere) bubbles up here as a DbUpdateException whose
                // inner provider exception carries the PG SQLSTATE. Translate it to a clean 409 so the
                // client gets a meaningful conflict instead of "unexpected error".
                var isReferentialConflict = exception is DbUpdateException
                    && exception.InnerException is System.Data.Common.DbException { SqlState: PostgresForeignKeyViolationSqlState };
                var isConflict = isConcurrencyConflict || isReferentialConflict;

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
                    logger.Log(
                        isConflict ? LogLevel.Warning : LogLevel.Error,
                        exception,
                        "Unhandled exception processing request {Method} {Path} | Tenant: {TenantId} | User: {UserId} | TraceId: {TraceIdentifier}",
                        context.Request.Method,
                        context.Request.Path.Value,
                        tenantId,
                        userId,
                        context.TraceIdentifier);
                }

                var statusCode = isConflict
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status500InternalServerError;
                context.Response.StatusCode = statusCode;

                var localizer = context.RequestServices.GetService<IBackendMessageLocalizer>();

                string title;
                string detail;
                string code;
                if (isConcurrencyConflict)
                {
                    title = "Concurrency conflict";
                    detail = "The resource was modified by another request. Refresh and try again.";
                    code = "CONCURRENCY_CONFLICT";
                }
                else if (isReferentialConflict)
                {
                    title = localizer?.Localize("common.referenced", "Resource in use") ?? "Resource in use";
                    detail = localizer?.Localize(
                        "common.referenced.detail",
                        "The resource cannot be modified or deleted because it is referenced by other records.")
                        ?? "The resource cannot be modified or deleted because it is referenced by other records.";
                    code = "REFERENCED_BY_OTHER_RESOURCES";
                }
                else
                {
                    title = localizer?.Localize("common.unexpected", "Unexpected error") ?? "Unexpected error";
                    detail = hostEnvironment.IsDevelopment()
                        ? exception.Message
                        : localizer?.Localize("common.unexpected", "An unexpected error occurred.") ?? "An unexpected error occurred.";
                    code = "common.unexpected";
                }

                var problemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = title,
                    Detail = detail,
                    Type = $"https://httpstatuses.com/{statusCode}"
                };

                problemDetails.Extensions["code"] = code;
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
