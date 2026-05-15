using CLARIHR.Api.Controllers;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using CLARIHR.Domain.PositionDescriptionCatalogs;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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
            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                PositionDescriptionCatalogErrors.InvalidCatalogType.Message);
        }

        return Task.CompletedTask;
    }
}
