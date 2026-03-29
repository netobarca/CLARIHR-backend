using CLARIHR.Application.Common.Errors;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Backoffice.Api.Common;

internal static class ProblemDetailsFactory
{
    public static ObjectResult Create(HttpContext httpContext, Error error) =>
        new(CreateProblemDetails(httpContext, error))
        {
            StatusCode = MapStatusCode(error.Type)
        };

    public static ProblemDetails CreateProblemDetails(HttpContext httpContext, Error error)
    {
        var statusCode = MapStatusCode(error.Type);

        ProblemDetails problemDetails = error.Type == ErrorType.Validation
            ? CreateValidationProblemDetails(error)
            : new ProblemDetails();

        problemDetails.Title = error.Message;
        problemDetails.Detail = error.Message;
        problemDetails.Status = statusCode;
        problemDetails.Type = $"https://httpstatuses.com/{statusCode}";
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
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status500InternalServerError
        };

    private static ValidationProblemDetails CreateValidationProblemDetails(Error error)
    {
        var validationProblemDetails = new ValidationProblemDetails();
        foreach (var validationError in error.ValidationErrors ?? new Dictionary<string, string[]>())
        {
            validationProblemDetails.Errors[validationError.Key] = validationError.Value;
        }

        return validationProblemDetails;
    }
}
