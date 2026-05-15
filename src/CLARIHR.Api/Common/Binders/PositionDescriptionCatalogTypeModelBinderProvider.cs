using CLARIHR.Domain.PositionDescriptionCatalogs;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CLARIHR.Api.Common.Binders;

internal sealed class PositionDescriptionCatalogTypeModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Metadata.ModelType == typeof(PositionDescriptionCatalogType))
        {
            return new PositionDescriptionCatalogTypeModelBinder();
        }

        return null;
    }
}
