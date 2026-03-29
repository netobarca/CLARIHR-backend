using CLARIHR.Application.Common.Contracts;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CLARIHR.Api.Configuration;

public sealed class PublicContractOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Parameters.Count == 0)
        {
            return;
        }

        foreach (var parameter in operation.Parameters)
        {
            var description = context.ApiDescription.ParameterDescriptions
                .FirstOrDefault(candidate => candidate.Name.Equals(parameter.Name, StringComparison.Ordinal));

            var modelType = description?.Type ?? description?.ModelMetadata?.ModelType;
            var renamed = PublicContractNaming.GetExternalIdentifierName(parameter.Name, modelType);
            if (!string.IsNullOrWhiteSpace(renamed) && renamed != parameter.Name)
            {
                parameter.Name = renamed;
            }
        }
    }
}
