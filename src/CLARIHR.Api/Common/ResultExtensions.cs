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

    public static ActionResult<TValue> ToActionResultWithWeakETag<TValue>(
        this ControllerBase controller,
        CLARIHR.Application.Common.Errors.Result<TValue> result,
        Func<TValue, string?> weakETagSelector)
    {
        ArgumentNullException.ThrowIfNull(weakETagSelector);

        if (result.IsSuccess)
        {
            var opaqueTag = weakETagSelector(result.Value);
            if (!string.IsNullOrEmpty(opaqueTag))
            {
                controller.Response.Headers[ETagHeader.HeaderName] = ETagHeader.FormatWeak(opaqueTag);
            }

            return controller.Ok(result.Value);
        }

        return new ActionResult<TValue>(ProblemDetailsFactory.Create(controller.HttpContext, result.Error));
    }

    public static ActionResult<TValue> ToCreatedAtActionResultWithWeakETag<TValue>(
        this ControllerBase controller,
        CLARIHR.Application.Common.Errors.Result<TValue> result,
        string actionName,
        Func<TValue, object?> routeValuesFactory,
        Func<TValue, string?> weakETagSelector)
    {
        ArgumentException.ThrowIfNullOrEmpty(actionName);
        ArgumentNullException.ThrowIfNull(routeValuesFactory);
        ArgumentNullException.ThrowIfNull(weakETagSelector);

        if (result.IsSuccess)
        {
            var opaqueTag = weakETagSelector(result.Value);
            if (!string.IsNullOrEmpty(opaqueTag))
            {
                controller.Response.Headers[ETagHeader.HeaderName] = ETagHeader.FormatWeak(opaqueTag);
            }

            return controller.CreatedAtAction(actionName, routeValuesFactory(result.Value), result.Value);
        }

        return new ActionResult<TValue>(ProblemDetailsFactory.Create(controller.HttpContext, result.Error));
    }

    public static ActionResult<TValue> ToCreatedResult<TValue>(
        this ControllerBase controller,
        CLARIHR.Application.Common.Errors.Result<TValue> result,
        Func<TValue, string> locationFactory)
    {
        ArgumentNullException.ThrowIfNull(locationFactory);

        if (result.IsSuccess)
        {
            return controller.Created(locationFactory(result.Value), result.Value);
        }

        return new ActionResult<TValue>(ProblemDetailsFactory.Create(controller.HttpContext, result.Error));
    }

    public static ActionResult<TValue> ToCreatedResult<TValue>(
        this ControllerBase controller,
        CLARIHR.Application.Common.Errors.Result<TValue> result,
        Func<TValue, string> locationFactory,
        Func<TValue, Guid> etagSelector)
    {
        ArgumentNullException.ThrowIfNull(locationFactory);
        ArgumentNullException.ThrowIfNull(etagSelector);

        if (result.IsSuccess)
        {
            controller.Response.Headers[ETagHeader.HeaderName] = ETagHeader.Format(etagSelector(result.Value));
            return controller.Created(locationFactory(result.Value), result.Value);
        }

        return new ActionResult<TValue>(ProblemDetailsFactory.Create(controller.HttpContext, result.Error));
    }

    public static ActionResult<TValue> ToCreatedAtActionResult<TValue>(
        this ControllerBase controller,
        CLARIHR.Application.Common.Errors.Result<TValue> result,
        string actionName,
        Func<TValue, object?> routeValuesFactory)
    {
        ArgumentException.ThrowIfNullOrEmpty(actionName);
        ArgumentNullException.ThrowIfNull(routeValuesFactory);

        if (result.IsSuccess)
        {
            return controller.CreatedAtAction(actionName, routeValuesFactory(result.Value), result.Value);
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

    /// <summary>
    /// H-34 — success with nothing to say: <c>204 No Content</c>. Used by the DELETE of a child, which used to
    /// answer <c>200</c> with the parent's concurrency token in the body AND in the ETag — a value that only
    /// changed in about half of those endpoints, so it could not be trusted anywhere.
    /// </summary>
    public static ActionResult ToNoContentResult<TValue>(
        this ControllerBase controller,
        CLARIHR.Application.Common.Errors.Result<TValue> result) =>
        result.IsSuccess
            ? controller.NoContent()
            : ProblemDetailsFactory.Create(controller.HttpContext, result.Error);

    public static ActionResult ToActionResult(this ControllerBase controller, CLARIHR.Application.Common.Errors.Result result)
    {
        if (result.IsSuccess)
        {
            return controller.NoContent();
        }

        return ProblemDetailsFactory.Create(controller.HttpContext, result.Error);
    }
}
