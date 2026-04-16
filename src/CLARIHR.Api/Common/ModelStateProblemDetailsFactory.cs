using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Abstractions.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.Common;

internal static class ModelStateProblemDetailsFactory
{
    private const string JsonConversionPrefix = "The JSON value could not be converted to ";

    public static IActionResult Create(ActionContext actionContext)
    {
        using var _ = ProblemDetailsLocalizationScope.UseFrom(actionContext.HttpContext);
        var localizer = actionContext.HttpContext.RequestServices.GetService<IBackendMessageLocalizer>();
        var error = ErrorCatalog.Validation(CreateValidationErrors(actionContext, localizer));
        return ProblemDetailsFactory.Create(actionContext.HttpContext, error);
    }

    private static IReadOnlyDictionary<string, string[]> CreateValidationErrors(
        ActionContext actionContext,
        IBackendMessageLocalizer? localizer)
    {
        var bodyParameterNames = GetBodyParameterNames(actionContext.ActionDescriptor.Parameters);
        var hasFieldLevelErrors = actionContext.ModelState
            .Where(static entry => entry.Value is { Errors.Count: > 0 })
            .Select(entry => NormalizeKey(entry.Key, bodyParameterNames))
            .Any(static key => !IsBodyKey(key));

        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var entry in actionContext.ModelState)
        {
            if (entry.Value is not { Errors.Count: > 0 } modelState)
            {
                continue;
            }

            var normalizedKey = NormalizeKey(entry.Key, bodyParameterNames);

            foreach (var modelError in modelState.Errors)
            {
                var message = NormalizeMessage(normalizedKey, modelError, hasFieldLevelErrors, localizer);
                if (string.IsNullOrWhiteSpace(message))
                {
                    continue;
                }

                if (!errors.TryGetValue(normalizedKey, out var messages))
                {
                    messages = [];
                    errors[normalizedKey] = messages;
                }

                if (!messages.Contains(message, StringComparer.Ordinal))
                {
                    messages.Add(message);
                }
            }
        }

        if (errors.Count > 0)
        {
            return errors.ToDictionary(static entry => entry.Key, static entry => entry.Value.ToArray(), StringComparer.Ordinal);
        }

        return new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["body"] = [Localize(localizer, "model.body.invalid", "The request body is invalid.")]
        };
    }

    private static HashSet<string> GetBodyParameterNames(IList<ParameterDescriptor> parameters)
    {
        var bodyParameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var parameter in parameters)
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

    private static string? NormalizeMessage(
        string normalizedKey,
        ModelError modelError,
        bool hasFieldLevelErrors,
        IBackendMessageLocalizer? localizer)
    {
        var message = string.IsNullOrWhiteSpace(modelError.ErrorMessage)
            ? modelError.Exception?.Message
            : modelError.ErrorMessage;

        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        if (IsBodyKey(normalizedKey) && IsBodyRequiredMessage(message))
        {
            return hasFieldLevelErrors
                ? null
                : Localize(localizer, "model.body.required", "The request body is required.");
        }

        if (message.StartsWith(JsonConversionPrefix, StringComparison.Ordinal))
        {
            return IsBodyKey(normalizedKey)
                ? Localize(localizer, "model.body.invalid", "The request body is invalid.")
                : MapJsonConversionMessage(message, localizer);
        }

        if (IsSimpleValueNotValidMessage(message))
        {
            return Localize(localizer, "model.value.invalid", "The value is invalid for this field.");
        }

        return message;
    }

    private static string MapJsonConversionMessage(string message, IBackendMessageLocalizer? localizer)
    {
        var typeName = ExtractConvertedTypeName(message);

        if (typeName.Contains("System.Guid", StringComparison.Ordinal))
        {
            return Localize(localizer, "model.value.uuid", "The value must be a valid UUID.");
        }

        if (typeName.Contains("System.Boolean", StringComparison.Ordinal))
        {
            return Localize(localizer, "model.value.bool", "The value must be true or false.");
        }

        if (typeName.Contains("System.Int", StringComparison.Ordinal))
        {
            return Localize(localizer, "model.value.int", "The value must be a valid integer.");
        }

        if (typeName.Contains("System.Decimal", StringComparison.Ordinal) ||
            typeName.Contains("System.Double", StringComparison.Ordinal) ||
            typeName.Contains("System.Single", StringComparison.Ordinal))
        {
            return Localize(localizer, "model.value.number", "The value must be a valid number.");
        }

        if (typeName.Contains("System.DateTime", StringComparison.Ordinal) ||
            typeName.Contains("System.DateOnly", StringComparison.Ordinal) ||
            typeName.Contains("System.TimeOnly", StringComparison.Ordinal))
        {
            return Localize(localizer, "model.value.datetime", "The value must be a valid date or date-time.");
        }

        return Localize(localizer, "model.value.invalid", "The value is invalid for this field.");
    }

    private static string ExtractConvertedTypeName(string message)
    {
        var pathMarkerIndex = message.IndexOf(". Path:", StringComparison.Ordinal);
        return pathMarkerIndex >= 0
            ? message[JsonConversionPrefix.Length..pathMarkerIndex].Trim()
            : message[JsonConversionPrefix.Length..].Trim();
    }

    private static bool IsBodyRequiredMessage(string message) =>
        message.Equals("A non-empty request body is required.", StringComparison.OrdinalIgnoreCase) ||
        message.EndsWith("field is required.", StringComparison.OrdinalIgnoreCase);

    private static bool IsSimpleValueNotValidMessage(string message) =>
        message.StartsWith("The value '", StringComparison.Ordinal) &&
        message.EndsWith("' is not valid.", StringComparison.Ordinal);

    private static bool IsBodyKey(string key) =>
        key.Equals("body", StringComparison.Ordinal);

    private static string Localize(IBackendMessageLocalizer? localizer, string key, string fallback) =>
        localizer?.Localize(key, fallback) ?? fallback;
}
