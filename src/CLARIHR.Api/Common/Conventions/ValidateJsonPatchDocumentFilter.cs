using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using MvcProblemDetailsFactory = Microsoft.AspNetCore.Mvc.Infrastructure.ProblemDetailsFactory;

namespace CLARIHR.Api.Common.Conventions;

internal sealed class ValidateJsonPatchDocumentFilter(MvcProblemDetailsFactory problemDetailsFactory) : IActionFilter
{
    private const string InvalidPatchDocumentDetail = "Invalid patch document.";

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

            context.Result = new BadRequestObjectResult(problemDetailsFactory.CreateProblemDetails(
                context.HttpContext,
                statusCode: StatusCodes.Status400BadRequest,
                detail: InvalidPatchDocumentDetail));

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
