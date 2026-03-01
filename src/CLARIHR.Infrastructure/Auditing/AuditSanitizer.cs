using System.Text.Json;
using System.Text.Json.Nodes;
using CLARIHR.Application.Abstractions.Auditing;

namespace CLARIHR.Infrastructure.Auditing;

internal sealed class AuditSanitizer : IAuditSanitizer
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private static readonly HashSet<string> SensitivePropertyNames =
    [
        "password",
        "passwordhash",
        "password_hash",
        "refreshtoken",
        "refreshtokens",
        "refresh_token",
        "refresh_tokens",
        "activationtoken",
        "activationtokens",
        "activation_token",
        "activation_tokens",
        "token",
        "tokenhash",
        "token_hash",
        "rawtoken",
        "raw_token",
        "secret",
        "secrets",
        "apikey",
        "api_key",
        "privatekey",
        "private_key"
    ];

    public string? SanitizeToJson(object? payload)
    {
        if (payload is null)
        {
            return null;
        }

        var node = JsonSerializer.SerializeToNode(payload, SerializerOptions);
        var sanitized = SanitizeNode(node);
        return sanitized?.ToJsonString(SerializerOptions);
    }

    private static JsonNode? SanitizeNode(JsonNode? node)
    {
        return node switch
        {
            null => null,
            JsonObject jsonObject => SanitizeObject(jsonObject),
            JsonArray jsonArray => SanitizeArray(jsonArray),
            _ => node.DeepClone()
        };
    }

    private static JsonObject SanitizeObject(JsonObject source)
    {
        var sanitized = new JsonObject();

        foreach (var property in source)
        {
            if (IsSensitive(property.Key))
            {
                continue;
            }

            sanitized[property.Key] = SanitizeNode(property.Value);
        }

        return sanitized;
    }

    private static JsonArray SanitizeArray(JsonArray source)
    {
        var sanitized = new JsonArray();

        foreach (var item in source)
        {
            sanitized.Add(SanitizeNode(item));
        }

        return sanitized;
    }

    private static bool IsSensitive(string propertyName)
    {
        var normalized = new string(propertyName
            .Where(char.IsLetterOrDigit)
            .ToArray())
            .ToLowerInvariant();

        return SensitivePropertyNames.Contains(normalized);
    }
}
