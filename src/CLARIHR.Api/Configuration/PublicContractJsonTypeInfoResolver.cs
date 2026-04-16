using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using CLARIHR.Application.Common.Contracts;

namespace CLARIHR.Api.Configuration;

public sealed class PublicContractJsonTypeInfoResolver : DefaultJsonTypeInfoResolver
{
    public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        var jsonTypeInfo = base.GetTypeInfo(type, options);

        if (jsonTypeInfo.Kind is not JsonTypeInfoKind.Object)
        {
            return jsonTypeInfo;
        }

        RemoveSuppressedMembers(jsonTypeInfo);
        RenameIdentifierMembers(jsonTypeInfo);
        NormalizeCodeMembers(jsonTypeInfo);
        AddSyntheticNormalizedCodeMember(jsonTypeInfo);
        ConfigurePropertyBasedDeserialization(type, jsonTypeInfo);

        return jsonTypeInfo;
    }

    private static void RemoveSuppressedMembers(JsonTypeInfo jsonTypeInfo)
    {
        for (var index = jsonTypeInfo.Properties.Count - 1; index >= 0; index--)
        {
            var property = jsonTypeInfo.Properties[index];
            if (PublicContractNaming.ShouldSuppressMember(GetClrName(property)))
            {
                jsonTypeInfo.Properties.RemoveAt(index);
            }
        }
    }

    private static void RenameIdentifierMembers(JsonTypeInfo jsonTypeInfo)
    {
        var assignedNames = new HashSet<string>(jsonTypeInfo.Properties.Select(property => property.Name), StringComparer.Ordinal);
        var aliasProperties = new List<JsonPropertyInfo>();

        foreach (var property in jsonTypeInfo.Properties)
        {
            var currentName = property.Name;
            var renamed = PublicContractNaming.GetExternalJsonName(GetClrName(property), property.PropertyType);
            if (renamed.Equals(currentName, StringComparison.Ordinal))
            {
                continue;
            }

            _ = assignedNames.Remove(currentName);

            if (!assignedNames.Add(renamed))
            {
                _ = assignedNames.Add(currentName);
                continue;
            }

            property.Name = renamed;

            if (property.Set is null || !assignedNames.Add(currentName))
            {
                continue;
            }

            var aliasProperty = jsonTypeInfo.CreateJsonPropertyInfo(property.PropertyType, currentName);
            aliasProperty.Set = property.Set;
            aliasProperty.ShouldSerialize = static (_, _) => false;
            aliasProperties.Add(aliasProperty);
        }

        foreach (var aliasProperty in aliasProperties)
        {
            jsonTypeInfo.Properties.Add(aliasProperty);
        }
    }

    private static void NormalizeCodeMembers(JsonTypeInfo jsonTypeInfo)
    {
        foreach (var property in jsonTypeInfo.Properties)
        {
            if (!PublicContractNaming.IsCodeProperty(GetClrName(property), property.PropertyType) || property.Get is null)
            {
                continue;
            }

            var originalGetter = property.Get;
            property.Get = instance =>
            {
                var value = originalGetter(instance) as string;
                return PublicContractNaming.NormalizeCodeValue(value);
            };
        }
    }

    private static void AddSyntheticNormalizedCodeMember(JsonTypeInfo jsonTypeInfo)
    {
        if (jsonTypeInfo.Properties.Any(property => GetClrName(property).Equals("NormalizedCode", StringComparison.Ordinal)))
        {
            return;
        }

        var codeProperty = jsonTypeInfo.Properties
            .FirstOrDefault(property =>
                GetClrName(property).Equals("Code", StringComparison.Ordinal) &&
                property.PropertyType == typeof(string) &&
                property.Get is not null);

        if (codeProperty is null)
        {
            return;
        }

        var syntheticProperty = jsonTypeInfo.CreateJsonPropertyInfo(typeof(string), PublicContractNaming.ToCamelCase("NormalizedCode"));
        syntheticProperty.Get = instance =>
        {
            var value = codeProperty.Get!(instance) as string;
            return PublicContractNaming.NormalizeCodeValue(value);
        };
        syntheticProperty.Set = null;
        syntheticProperty.ShouldSerialize = static (_, _) => true;
        jsonTypeInfo.Properties.Add(syntheticProperty);
    }

    private static void ConfigurePropertyBasedDeserialization(Type type, JsonTypeInfo jsonTypeInfo)
    {
        if (jsonTypeInfo.CreateObject is not null ||
            !type.IsClass ||
            type.IsAbstract ||
            type == typeof(string) ||
            !jsonTypeInfo.Properties.Any(static property => property.Set is not null))
        {
            return;
        }

        jsonTypeInfo.CreateObject = () => RuntimeHelpers.GetUninitializedObject(type);
    }

    private static string GetClrName(JsonPropertyInfo property) =>
        property.AttributeProvider is MemberInfo member
            ? member.Name
            : property.Name;
}
