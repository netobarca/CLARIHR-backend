using System.ComponentModel.DataAnnotations;
using System.Reflection;
using CLARIHR.Application.Common.Contracts;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CLARIHR.Api.Configuration;

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

        // A field the server marks [Required] (DataAnnotations) must not advertise itself as nullable/optional
        // in the published contract. For a POSITIONAL RECORD the attribute lives on the constructor PARAMETER,
        // not the generated property — `[property: Required]` on a record throws at model-validation time, so the
        // idiomatic form targets the parameter. We therefore look for [Required] on both the property and the
        // matching constructor parameter. Swashbuckle leaves `nullable: true` on the schema; clear it so the
        // OpenAPI matches the runtime validation (e.g. assignmentTypeCode and positionSlotPublicId on
        // …/assigned-positions, which are rejected with 400 when null/omitted).
        var requiredParameterNames = context.Type
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Where(parameter => parameter.Name is not null &&
                parameter.GetCustomAttribute<RequiredAttribute>() is not null)
            .Select(parameter => parameter.Name!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var property in properties)
        {
            var isRequired = property.GetCustomAttribute<RequiredAttribute>() is not null ||
                requiredParameterNames.Contains(property.Name);
            if (!isRequired ||
                PublicContractNaming.ShouldSuppressMember(property.Name))
            {
                continue;
            }

            var externalName = PublicContractNaming.GetExternalJsonName(property.Name, property.PropertyType);
            if (!schema.Properties.TryGetValue(externalName, out var requiredSchema))
            {
                continue;
            }

            requiredSchema.Nullable = false;
            schema.Required ??= new HashSet<string>(StringComparer.Ordinal);
            _ = schema.Required.Add(externalName);
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
