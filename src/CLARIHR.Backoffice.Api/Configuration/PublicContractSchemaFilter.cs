using System.Reflection;
using CLARIHR.Application.Common.Contracts;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CLARIHR.Backoffice.Api.Configuration;

public sealed class PublicContractSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema.Properties.Count == 0)
        {
            return;
        }

        var properties = context.Type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        foreach (var property in properties)
        {
            var currentName = PublicContractNaming.ToCamelCase(property.Name);
            if (PublicContractNaming.ShouldSuppressMember(property.Name))
            {
                RemoveProperty(schema, currentName);
                continue;
            }

            var renamed = PublicContractNaming.GetExternalJsonName(property.Name, property.PropertyType);
            if (!renamed.Equals(currentName, StringComparison.Ordinal) &&
                schema.Properties.TryGetValue(currentName, out var propertySchema))
            {
                schema.Properties.Remove(currentName);
                schema.Properties[renamed] = propertySchema;
                RenameRequiredProperty(schema, currentName, renamed);
            }
        }

        var hasCodeProperty = properties.Any(property =>
            property.Name.Equals("Code", StringComparison.Ordinal) &&
            property.PropertyType == typeof(string));
        var hasNormalizedCodeProperty = properties.Any(property =>
            property.Name.Equals("NormalizedCode", StringComparison.Ordinal) &&
            property.PropertyType == typeof(string));

        if (hasCodeProperty && !hasNormalizedCodeProperty && !schema.Properties.ContainsKey("normalizedCode"))
        {
            schema.Properties["normalizedCode"] = new OpenApiSchema
            {
                Type = "string",
                ReadOnly = true
            };
        }
    }

    private static void RemoveProperty(OpenApiSchema schema, string propertyName)
    {
        if (schema.Properties.Remove(propertyName))
        {
            RenameRequiredProperty(schema, propertyName, replacement: null);
        }
    }

    private static void RenameRequiredProperty(OpenApiSchema schema, string currentName, string? replacement)
    {
        if (schema.Required is null || schema.Required.Count == 0 || !schema.Required.Remove(currentName))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(replacement))
        {
            schema.Required.Add(replacement);
        }
    }
}
