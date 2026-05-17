using CLARIHR.Api.Controllers;
using CLARIHR.Domain.PositionDescriptionCatalogs;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CLARIHR.Api.Configuration;

/// <summary>
/// Surfaces the real accepted values of the <c>{catalogType}</c> route parameter in
/// OpenAPI. The parameter is bound to the strongly-typed
/// <see cref="PositionDescriptionCatalogType"/> enum (via
/// <c>PositionDescriptionCatalogTypeModelBinder</c>, debt §2.3), so Swashbuckle would
/// otherwise emit the enum member names — not the slugs a client must actually send
/// (<c>position-function-types</c>, <c>salary-classes</c>, …). This filter rewrites the
/// parameter schema to a string enum of the canonical slugs from
/// <see cref="PositionDescriptionCatalogRouteMap.Slugs"/> (single source of truth),
/// closing technical-debt §3.6 / §6.2 and enabling typed SDK generation. Metadata-only,
/// non-breaking.
/// </summary>
public sealed class CatalogTypeSlugOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        foreach (var parameter in operation.Parameters)
        {
            var description = context.ApiDescription.ParameterDescriptions
                .FirstOrDefault(candidate => candidate.Name.Equals(parameter.Name, StringComparison.Ordinal));

            var modelType = description?.Type ?? description?.ModelMetadata?.ModelType;
            if (modelType != typeof(PositionDescriptionCatalogType))
            {
                continue;
            }

            parameter.Schema = new OpenApiSchema
            {
                Type = "string",
                Enum = PositionDescriptionCatalogRouteMap.Slugs
                    .Select(static slug => (IOpenApiAny)new OpenApiString(slug))
                    .ToList(),
                Example = new OpenApiString(PositionDescriptionCatalogRouteMap.Slugs[0]),
            };

            parameter.Description = string.IsNullOrWhiteSpace(parameter.Description)
                ? $"Catalog type slug. One of: {string.Join(", ", PositionDescriptionCatalogRouteMap.Slugs)}."
                : parameter.Description;
        }
    }
}
