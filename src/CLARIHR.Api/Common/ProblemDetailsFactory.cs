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
                "Unexpected failure mapped to 500 for {Method} {Path}: error code '{ErrorCode}' (type {ErrorType}), "
                    + "traceId {TraceId}. Message: {Message}",
                httpContext.Request.Method,
                httpContext.Request.Path.Value,
                error.Code,
                error.Type,
                httpContext.TraceIdentifier,
                error.Message);

            // H-26 — the client used to receive the storage engine's own words: "Cannot write DateTime with
            // Kind=Unspecified to PostgreSQL type 'timestamp with time zone'… (Parameter 'value')". A 500 says
            // that something broke on our side and nothing else; the detail now lives only in the log above,
            // findable by the traceId that travels in the response.
            error = error with { Message = "An unexpected error occurred." };
        }

        var localizer = requestServices is null
            ? null
            : requestServices.GetService<IBackendMessageLocalizer>();

        ProblemDetails problemDetails = error.Type == ErrorType.Validation
            ? CreateValidationProblemDetails(error, localizer, PublicFieldNameMap.For(httpContext))
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

    /// <summary>
    /// 00005 / B-02 — la clave de cada error se traduce al <b>nombre público</b> del campo antes de salir.
    /// <para>
    /// Los validadores de FluentValidation corren sobre el <i>query object</i> o el comando, así que emiten el
    /// nombre interno de la propiedad: <c>search</c> donde el cliente mandó <c>q</c>, <c>pageNumber</c> donde
    /// mandó <c>page</c>, <c>locationGroupId</c> donde mandó <c>locationGroupPublicId</c>. El frontend mapea
    /// <c>errors[clave]</c> a su control, y con el nombre interno no encuentra ninguno.
    /// </para>
    /// <para>
    /// ⚠️ Este es el camino que producía el defecto. El otro —model-binding de MVC— ya normalizaba en
    /// <c>ProblemDetailsDefaults.NormalizeValidationErrors</c>, y por eso el defecto no se veía allí.
    /// </para>
    /// </summary>
    private static ValidationProblemDetails CreateValidationProblemDetails(
        Error error,
        IBackendMessageLocalizer? localizer,
        PublicFieldNameMap publicNames)
    {
        var validationProblemDetails = new ValidationProblemDetails();
        foreach (var validationError in error.ValidationErrors ?? new Dictionary<string, string[]>())
        {
            var key = publicNames.Resolve(validationError.Key);
            var messages = validationError.Value
                .Select(message => LocalizeValidationMessage(localizer, message))
                .Distinct(StringComparer.Ordinal);

            validationProblemDetails.Errors[key] = validationProblemDetails.Errors.TryGetValue(key, out var existing)
                ? existing.Concat(messages).Distinct(StringComparer.Ordinal).ToArray()
                : messages.ToArray();
        }

        return validationProblemDetails;
    }

    private static string LocalizeValidationMessage(IBackendMessageLocalizer? localizer, string message) =>
        localizer?.LocalizeValidationMessage(message) ?? message;
}
