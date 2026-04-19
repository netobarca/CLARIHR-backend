using System.Text.Json;
using System.Text.Json.Nodes;
using CLARIHR.Application.Abstractions.Auditing;

namespace CLARIHR.Infrastructure.Auditing;

internal sealed class AuditSanitizer : IAuditSanitizer
{
    private const string RedactedValue = "[REDACTED]";
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

    private static readonly HashSet<string> PiiPropertyNames =
    [
        "address",
        "addressline",
        "accountnumber",
        "authoruseremail",
        "birthdate",
        "companyphone",
        "customdata",
        "customdatajson",
        "dateofbirth",
        "documentnumber",
        "email",
        "filedata",
        "firstname",
        "fullname",
        "identificationnumber",
        "institutionalemail",
        "institutionalphone",
        "lastname",
        "managername",
        "normalizedidentificationnumber",
        "personalemail",
        "personalphone",
        "personname",
        "phone",
        "relatedemployeename",
        "salary",
        "workphone"
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
            var normalizedPropertyName = NormalizePropertyName(property.Key);
            if (SensitivePropertyNames.Contains(normalizedPropertyName))
            {
                continue;
            }

            sanitized[property.Key] = PiiPropertyNames.Contains(normalizedPropertyName)
                ? JsonValue.Create(RedactedValue)
                : SanitizeNode(property.Value);
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

    private static string NormalizePropertyName(string propertyName) =>
        new string(propertyName
            .Where(char.IsLetterOrDigit)
            .ToArray())
            .ToLowerInvariant();
}
