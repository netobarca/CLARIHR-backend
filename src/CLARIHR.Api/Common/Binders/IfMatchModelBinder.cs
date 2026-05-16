using CLARIHR.Api.Common;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CLARIHR.Api.Common.Binders;

public sealed class IfMatchModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var headerValue = bindingContext.HttpContext.Request.Headers[IfMatchHeader.HeaderName].ToString();
        if (IfMatchHeader.TryParseConcurrencyToken(headerValue, out var concurrencyToken))
        {
            bindingContext.Result = ModelBindingResult.Success(concurrencyToken);
            return Task.CompletedTask;
        }

        var logger = bindingContext.HttpContext.RequestServices?
            .GetService<ILogger<IfMatchModelBinder>>()
            ?? NullLogger<IfMatchModelBinder>.Instance;
        var request = bindingContext.HttpContext.Request;
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            logger.LogWarning(
                "Rejected {Method} {Path}: required '{Header}' header is missing.",
                request.Method,
                request.Path.Value,
                IfMatchHeader.HeaderName);
        }
        else
        {
            logger.LogWarning(
                "Rejected {Method} {Path}: '{Header}' header value is malformed (expected a concurrency token GUID).",
                request.Method,
                request.Path.Value,
                IfMatchHeader.HeaderName);
        }

        bindingContext.ModelState.TryAddModelError(IfMatchHeader.HeaderName, IfMatchHeader.MissingDetail);
        bindingContext.Result = ModelBindingResult.Failed();
        return Task.CompletedTask;
    }
}
