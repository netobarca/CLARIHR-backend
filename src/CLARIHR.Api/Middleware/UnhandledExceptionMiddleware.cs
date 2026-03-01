using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Middleware;

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
            logger.LogError(exception, "Unhandled exception processing request {TraceIdentifier}", context.TraceIdentifier);

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
