using CLARIHR.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Backoffice.Api.Common;

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

    public static ActionResult<TValue> ToActionResultWithETag<TValue>(
        this ControllerBase controller,
        CLARIHR.Application.Common.Errors.Result<TValue> result,
        Func<TValue, Guid> etagSelector)
    {
        ArgumentNullException.ThrowIfNull(etagSelector);

        if (result.IsSuccess)
        {
            controller.Response.Headers[ETagHeader.HeaderName] = ETagHeader.Format(etagSelector(result.Value));
            return controller.Ok(result.Value);
        }

        return new ActionResult<TValue>(ProblemDetailsFactory.Create(controller.HttpContext, result.Error));
    }

    public static ActionResult<TValue> ToCreatedAtActionResult<TValue>(
        this ControllerBase controller,
        CLARIHR.Application.Common.Errors.Result<TValue> result,
        string actionName,
        Func<TValue, object?> routeValuesFactory,
        Func<TValue, Guid> etagSelector)
    {
        ArgumentException.ThrowIfNullOrEmpty(actionName);
        ArgumentNullException.ThrowIfNull(routeValuesFactory);
        ArgumentNullException.ThrowIfNull(etagSelector);

        if (result.IsSuccess)
        {
            controller.Response.Headers[ETagHeader.HeaderName] = ETagHeader.Format(etagSelector(result.Value));
            return controller.CreatedAtAction(actionName, routeValuesFactory(result.Value), result.Value);
        }

        return new ActionResult<TValue>(ProblemDetailsFactory.Create(controller.HttpContext, result.Error));
    }
}
