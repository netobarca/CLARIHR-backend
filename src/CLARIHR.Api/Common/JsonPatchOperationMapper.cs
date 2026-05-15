using System.Text.Json;
using Microsoft.AspNetCore.JsonPatch;
using Newtonsoft.Json.Linq;

namespace CLARIHR.Api.Common;

internal static class JsonPatchOperationMapper
{
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

    public static JsonElement? MapValue(object? value)
    {
        if (value is null)
        {
            return JsonSerializer.SerializeToElement<object?>(null);
        }

        if (value is JToken token)
        {
            using var document = JsonDocument.Parse(token.ToString(Newtonsoft.Json.Formatting.None));
            return document.RootElement.Clone();
        }

        return JsonSerializer.SerializeToElement(value, value.GetType());
    }
}
