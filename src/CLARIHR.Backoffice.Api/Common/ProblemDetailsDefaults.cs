using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CLARIHR.Backoffice.Api.Common;

internal static class ProblemDetailsDefaults
{
    private const string ValidationCode = "common.validation";
    private const string ValidationTitle = "One or more validation errors occurred.";
    private const string TraceIdExtensionKey = "traceId";
    private const string CodeExtensionKey = "code";

    public static void Apply(ProblemDetailsContext context)
    {
        var problemDetails = context.ProblemDetails;
        var statusCode = problemDetails.Status ?? context.HttpContext.Response.StatusCode;

        if (statusCode > 0 && problemDetails.Status is null)
        {
            problemDetails.Status = statusCode;
        }

        if (statusCode > 0 &&
            (string.IsNullOrWhiteSpace(problemDetails.Type) ||
             problemDetails.Type.StartsWith("https://tools.ietf.org/html/rfc9110#", StringComparison.OrdinalIgnoreCase)))
        {
            problemDetails.Type = $"https://httpstatuses.com/{statusCode}";
        }

        if (!problemDetails.Extensions.ContainsKey(TraceIdExtensionKey))
        {
            problemDetails.Extensions[TraceIdExtensionKey] = context.HttpContext.TraceIdentifier;
        }

        if (problemDetails is not HttpValidationProblemDetails validationProblemDetails)
        {
            return;
        }

        problemDetails.Title ??= ValidationTitle;
        problemDetails.Detail ??= problemDetails.Title;

        if (!problemDetails.Extensions.ContainsKey(CodeExtensionKey))
        {
            problemDetails.Extensions[CodeExtensionKey] = ValidationCode;
        }

        NormalizeValidationErrors(validationProblemDetails, GetBodyParameterNames(context.HttpContext));
    }

    private static void NormalizeValidationErrors(HttpValidationProblemDetails validationProblemDetails, ISet<string> bodyParameterNames)
    {
        if (validationProblemDetails.Errors.Count == 0)
        {
            return;
        }

        var hasFieldLevelErrors = validationProblemDetails.Errors.Keys
            .Select(key => NormalizeKey(key, bodyParameterNames))
            .Any(static key => !IsBodyKey(key));

        var normalizedErrors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var entry in validationProblemDetails.Errors)
        {
            var normalizedKey = NormalizeKey(entry.Key, bodyParameterNames);

            foreach (var rawMessage in entry.Value)
            {
                var message = NormalizeMessage(normalizedKey, rawMessage, hasFieldLevelErrors);
                if (string.IsNullOrWhiteSpace(message))
                {
                    continue;
                }

                if (!normalizedErrors.TryGetValue(normalizedKey, out var messages))
                {
                    messages = [];
                    normalizedErrors[normalizedKey] = messages;
                }

                if (!messages.Contains(message, StringComparer.Ordinal))
                {
                    messages.Add(message);
                }
            }
        }

        if (normalizedErrors.Count == 0)
        {
            normalizedErrors["body"] = ["The request body is invalid."];
        }

        validationProblemDetails.Errors.Clear();
        foreach (var entry in normalizedErrors)
        {
            validationProblemDetails.Errors.Add(entry.Key, entry.Value.ToArray());
        }
    }

    private static ISet<string> GetBodyParameterNames(HttpContext httpContext)
    {
        if (httpContext.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>() is not { } actionDescriptor)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var bodyParameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in actionDescriptor.Parameters)
        {
            if (parameter.BindingInfo?.BindingSource != BindingSource.Body)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(parameter.Name))
            {
                bodyParameterNames.Add(parameter.Name);
            }

            if (!string.IsNullOrWhiteSpace(parameter.BindingInfo.BinderModelName))
            {
                bodyParameterNames.Add(parameter.BindingInfo.BinderModelName!);
            }
        }

        return bodyParameterNames;
    }

    private static string NormalizeKey(string key, ISet<string> bodyParameterNames)
    {
        if (string.IsNullOrWhiteSpace(key) || key == "$")
        {
            return "body";
        }

        var normalizedKey = key;
        if (normalizedKey.StartsWith("$.", StringComparison.Ordinal))
        {
            normalizedKey = normalizedKey[2..];
        }
        else if (normalizedKey.StartsWith("$", StringComparison.Ordinal))
        {
            normalizedKey = normalizedKey[1..];
        }

        foreach (var bodyParameterName in bodyParameterNames)
        {
            if (normalizedKey.Equals(bodyParameterName, StringComparison.OrdinalIgnoreCase))
            {
                return "body";
            }

            var dottedPrefix = $"{bodyParameterName}.";
            if (normalizedKey.StartsWith(dottedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                normalizedKey = normalizedKey[dottedPrefix.Length..];
                break;
            }

            var indexedPrefix = $"{bodyParameterName}[";
            if (normalizedKey.StartsWith(indexedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                normalizedKey = normalizedKey[bodyParameterName.Length..];
                break;
            }
        }

        return string.IsNullOrWhiteSpace(normalizedKey) ? "body" : normalizedKey;
    }

    private static string? NormalizeMessage(string normalizedKey, string rawMessage, bool hasFieldLevelErrors)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
        {
            return null;
        }

        if (IsBodyKey(normalizedKey) &&
            (rawMessage.Equals("A non-empty request body is required.", StringComparison.OrdinalIgnoreCase) ||
             rawMessage.EndsWith("field is required.", StringComparison.OrdinalIgnoreCase)))
        {
            return hasFieldLevelErrors ? null : "The request body is required.";
        }

        if (rawMessage.StartsWith("The JSON value could not be converted to ", StringComparison.Ordinal))
        {
            return IsBodyKey(normalizedKey)
                ? "The request body is invalid."
                : MapJsonConversionMessage(rawMessage);
        }

        return rawMessage;
    }

    private static string MapJsonConversionMessage(string message)
    {
        const string prefix = "The JSON value could not be converted to ";
        var pathMarkerIndex = message.IndexOf(". Path:", StringComparison.Ordinal);
        var typeName = pathMarkerIndex >= 0
            ? message[prefix.Length..pathMarkerIndex].Trim()
            : message[prefix.Length..].Trim();

        if (typeName.Contains("System.Guid", StringComparison.Ordinal))
        {
            return "The value must be a valid UUID.";
        }

        if (typeName.Contains("System.Boolean", StringComparison.Ordinal))
        {
            return "The value must be true or false.";
        }

        if (typeName.Contains("System.Int", StringComparison.Ordinal))
        {
            return "The value must be a valid integer.";
        }

        if (typeName.Contains("System.Decimal", StringComparison.Ordinal) ||
            typeName.Contains("System.Double", StringComparison.Ordinal) ||
            typeName.Contains("System.Single", StringComparison.Ordinal))
        {
            return "The value must be a valid number.";
        }

        if (typeName.Contains("System.DateTime", StringComparison.Ordinal) ||
            typeName.Contains("System.DateOnly", StringComparison.Ordinal) ||
            typeName.Contains("System.TimeOnly", StringComparison.Ordinal))
        {
            return "The value must be a valid date or date-time.";
        }

        return "The value is invalid for this field.";
    }

    private static bool IsBodyKey(string key) =>
        key.Equals("body", StringComparison.Ordinal);
}
