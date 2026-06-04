using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace CLARIHR.Api.Common.Conventions;

/// <summary>
/// Expands <see cref="ProducesStandardErrorsAttribute"/> into one
/// <see cref="ProducesResponseTypeAttribute"/> per requested status code so that
/// ApiExplorer and Swashbuckle pick them up identically to hand-written declarations.
/// Action-level attributes take precedence over controller-level ones, and any
/// status code already declared inline on the action is preserved as-is.
/// </summary>
public sealed class ProducesStandardErrorsConvention : IActionModelConvention
{
    private static readonly (StandardErrorSet Flag, int StatusCode)[] FlagToStatus =
    [
        (StandardErrorSet.BadRequest, StatusCodes.Status400BadRequest),
        (StandardErrorSet.Unauthorized, StatusCodes.Status401Unauthorized),
        (StandardErrorSet.Forbidden, StatusCodes.Status403Forbidden),
        (StandardErrorSet.NotFound, StatusCodes.Status404NotFound),
        (StandardErrorSet.Conflict, StatusCodes.Status409Conflict),
        (StandardErrorSet.UnprocessableEntity, StatusCodes.Status422UnprocessableEntity),
    ];

    public void Apply(ActionModel action)
    {
        var attribute = action.Attributes.OfType<ProducesStandardErrorsAttribute>().FirstOrDefault()
            ?? action.Controller.Attributes.OfType<ProducesStandardErrorsAttribute>().FirstOrDefault();

        if (attribute is null || attribute.Errors == StandardErrorSet.None)
        {
            return;
        }

        var declaredStatusCodes = action.Filters
            .OfType<ProducesResponseTypeAttribute>()
            .Select(filter => filter.StatusCode)
            .ToHashSet();

        foreach (var (flag, statusCode) in FlagToStatus)
        {
            if ((attribute.Errors & flag) != flag)
            {
                continue;
            }

            if (declaredStatusCodes.Add(statusCode))
            {
                action.Filters.Add(new ProducesResponseTypeAttribute(typeof(ProblemDetails), statusCode));
            }
        }
    }
}
