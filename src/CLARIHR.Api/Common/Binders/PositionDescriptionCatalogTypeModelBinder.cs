using CLARIHR.Api.Controllers;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using CLARIHR.Domain.PositionDescriptionCatalogs;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CLARIHR.Api.Common.Binders;

internal sealed class PositionDescriptionCatalogTypeModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueProviderResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        var value = valueProviderResult.FirstValue;

        if (PositionDescriptionCatalogRouteMap.TryResolve(value, out var resolvedType))
        {
            bindingContext.Result = ModelBindingResult.Success(resolvedType);
        }
        else
        {
            var logger = bindingContext.HttpContext.RequestServices?
                .GetService<ILogger<PositionDescriptionCatalogTypeModelBinder>>()
                ?? NullLogger<PositionDescriptionCatalogTypeModelBinder>.Instance;
            var request = bindingContext.HttpContext.Request;
            logger.LogWarning(
                "Rejected {Method} {Path}: unknown catalog type slug '{Slug}' (client may be using a deprecated or invalid slug).",
                request.Method,
                request.Path.Value,
                value);

            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                PositionDescriptionCatalogErrors.InvalidCatalogType.Message);
        }

        return Task.CompletedTask;
    }
}
