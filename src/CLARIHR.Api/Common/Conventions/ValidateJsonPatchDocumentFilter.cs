using CLARIHR.Application.Common.Errors;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CLARIHR.Api.Common.Conventions;

/// <summary>
/// Rejects a missing/unbound JSON Patch body with the project's standard
/// <c>ProblemDetails</c> shape (typed <c>code</c> + <c>traceId</c> + localized,
/// via <see cref="ProblemDetailsFactory"/>) instead of the framework factory,
/// so every PATCH endpoint returns a consistent 400 (technical-debt JP §8.2).
/// </summary>
internal sealed class ValidateJsonPatchDocumentFilter : IActionFilter
{
    private const string InvalidPatchDocumentDetail = "The request body is invalid.";

    public void OnActionExecuting(ActionExecutingContext context)
    {
        foreach (var parameter in context.ActionDescriptor.Parameters)
        {
            if (!IsJsonPatchBodyParameter(parameter))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(parameter.Name))
            {
                continue;
            }

            if (context.ActionArguments.TryGetValue(parameter.Name, out var argument) && argument is not null)
            {
                continue;
            }

            var error = ErrorCatalog.Validation(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["body"] = [InvalidPatchDocumentDetail],
            });

            context.Result = ProblemDetailsFactory.Create(context.HttpContext, error);
            return;
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }

    private static bool IsJsonPatchBodyParameter(ParameterDescriptor parameter) =>
        parameter.BindingInfo?.BindingSource == BindingSource.Body &&
        parameter.ParameterType.IsGenericType &&
        parameter.ParameterType.GetGenericTypeDefinition() == typeof(JsonPatchDocument<>);
}
