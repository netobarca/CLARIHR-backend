using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;

namespace CLARIHR.Application.Common.JsonPatch;

public static class JsonPatchOperationMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static IReadOnlyCollection<TOperation> Map<TRequest, TOperation>(
        JsonPatchDocument<TRequest> patchDoc,
        Func<string, string, string?, JsonElement?, TOperation> createOperation)
        where TRequest : class
    {
        ArgumentNullException.ThrowIfNull(patchDoc);
        ArgumentNullException.ThrowIfNull(createOperation);

        return patchDoc.Operations
            .Select(operation => createOperation(
                operation.op,
                operation.path,
                operation.from,
                MapValue(operation.value)))
            .ToArray();
    }

    public static JsonElement? MapValue(object? value) =>
        value switch
        {
            null => JsonSerializer.SerializeToElement<object?>(null),
            JsonElement element => element.Clone(),
            _ => JsonSerializer.SerializeToElement(value, value.GetType(), JsonOptions)
        };
}
