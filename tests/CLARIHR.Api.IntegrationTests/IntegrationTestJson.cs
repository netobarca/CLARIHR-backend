using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using CLARIHR.Api.Configuration;

namespace CLARIHR.Api.IntegrationTests;

internal static class IntegrationTestJson
{
    public static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            TypeInfoResolver = JsonTypeInfoResolver.Combine(
                new PublicContractJsonTypeInfoResolver(),
                new DefaultJsonTypeInfoResolver())
        };

        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
