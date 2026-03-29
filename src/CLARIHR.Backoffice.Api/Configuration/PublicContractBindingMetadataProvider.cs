using CLARIHR.Application.Common.Contracts;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace CLARIHR.Backoffice.Api.Configuration;

public sealed class PublicContractBindingMetadataProvider : IBindingMetadataProvider
{
    public void CreateBindingMetadata(BindingMetadataProviderContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.BindingMetadata.BinderModelName) ||
            context.BindingMetadata.BindingSource == BindingSource.Path ||
            string.IsNullOrWhiteSpace(context.Key.Name))
        {
            return;
        }

        var externalName = PublicContractNaming.GetExternalIdentifierName(context.Key.Name, context.Key.ModelType);
        if (!string.IsNullOrWhiteSpace(externalName) && !externalName.Equals(context.Key.Name, StringComparison.Ordinal))
        {
            context.BindingMetadata.BinderModelName = externalName;
        }
    }
}
