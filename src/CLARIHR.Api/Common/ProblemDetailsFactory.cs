using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Abstractions.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CLARIHR.Api.Common;

internal static class ProblemDetailsFactory
{
    public static ObjectResult Create(HttpContext httpContext, Error error) =>
        new(CreateProblemDetails(httpContext, error))
        {
            StatusCode = MapStatusCode(error.Type)
        };

    public static ProblemDetails CreateProblemDetails(HttpContext httpContext, Error error)
    {
        using var _ = ProblemDetailsLocalizationScope.UseFrom(httpContext);

        var statusCode = MapStatusCode(error.Type);
        var requestServices = httpContext.RequestServices;

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            var logger = requestServices?
                .GetService<ILoggerFactory>()
                ?.CreateLogger("CLARIHR.Api.Common.ProblemDetailsFactory");
            logger?.LogWarning(
                "Unexpected failure mapped to 500 for {Method} {Path}: error code '{ErrorCode}' (type {ErrorType}), traceId {TraceId}.",
                httpContext.Request.Method,
                httpContext.Request.Path.Value,
                error.Code,
                error.Type,
                httpContext.TraceIdentifier);
        }

        var localizer = requestServices is null
            ? null
            : requestServices.GetService<IBackendMessageLocalizer>();

        ProblemDetails problemDetails = error.Type == ErrorType.Validation
            ? CreateValidationProblemDetails(error, localizer)
            : new ProblemDetails();

        var localizedMessage = localizer?.Localize(error.Code, error.Message, error.MessageArguments)
            ?? error.Message;

        problemDetails.Title = localizedMessage;
        problemDetails.Detail = localizedMessage;
        problemDetails.Status = statusCode;
        problemDetails.Type = $"https://httpstatuses.com/{statusCode}";
        // The error's own structured payload goes in FIRST, so it can never shadow the reserved keys below.
        if (error.Extensions is { Count: > 0 })
        {
            foreach (var (key, value) in error.Extensions)
            {
                problemDetails.Extensions[key] = value;
            }
        }

        problemDetails.Extensions["code"] = error.Code;
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        if (error.Details is { Count: > 0 })
        {
            problemDetails.Extensions["details"] = error.Details;
        }

        return problemDetails;
    }

    public static int MapStatusCode(ErrorType errorType) =>
        errorType switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.UnprocessableEntity => StatusCodes.Status422UnprocessableEntity,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.MethodNotAllowed => StatusCodes.Status405MethodNotAllowed,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
            ErrorType.PayloadTooLarge => StatusCodes.Status413PayloadTooLarge,
            ErrorType.Gone => StatusCodes.Status410Gone,
            ErrorType.ServiceUnavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };

    private static ValidationProblemDetails CreateValidationProblemDetails(
        Error error,
        IBackendMessageLocalizer? localizer)
    {
        var validationProblemDetails = new ValidationProblemDetails();
        foreach (var validationError in error.ValidationErrors ?? new Dictionary<string, string[]>())
        {
            validationProblemDetails.Errors[validationError.Key] = validationError.Value
                .Select(message => LocalizeValidationMessage(localizer, message))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        return validationProblemDetails;
    }

    private static string LocalizeValidationMessage(IBackendMessageLocalizer? localizer, string message) =>
        localizer?.LocalizeValidationMessage(message) ?? message;
}
