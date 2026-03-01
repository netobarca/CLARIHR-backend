using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Common;

internal static class ResultExtensions
{
    public static ActionResult<TValue> ToActionResult<TValue>(this ControllerBase controller, CLARIHR.Application.Common.Errors.Result<TValue> result)
    {
        if (result.IsSuccess)
        {
            return controller.Ok(result.Value);
        }

        return new ActionResult<TValue>(ProblemDetailsFactory.Create(controller.HttpContext, result.Error));
    }

    public static ActionResult ToActionResult(this ControllerBase controller, CLARIHR.Application.Common.Errors.Result result)
    {
        if (result.IsSuccess)
        {
            return controller.NoContent();
        }

        return ProblemDetailsFactory.Create(controller.HttpContext, result.Error);
    }
}
