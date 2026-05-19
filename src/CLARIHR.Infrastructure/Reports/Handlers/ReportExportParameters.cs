using System.Globalization;
using System.Text.Json;

namespace CLARIHR.Infrastructure.Reports.Handlers;

/// <summary>
/// Shared parsing helpers for the JSON parameter payload of a <c>ReportExportJob</c>.
/// Extracted verbatim from the former <c>ReportExportJobGenerator</c> switch dispatcher
/// so every <see cref="IReportExportHandler"/> reads parameters identically.
/// </summary>
internal static class ReportExportParameters
{
    public static JsonDocument Parse(string parametersJson)
    {
        if (string.IsNullOrWhiteSpace(parametersJson))
        {
            return JsonDocument.Parse("{}");
        }

        try
        {
            var document = JsonDocument.Parse(parametersJson);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document
                : JsonDocument.Parse("{}");
        }
        catch (JsonException)
        {
            throw new ReportExportInvalidParametersException("Report export parameters must be a valid JSON object.");
        }
    }

    public static string? ReadString(JsonElement parameters, params string[] names)
    {
        if (!TryGetProperty(parameters, out var value, names))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? Normalize(value.GetString())
            : value.ToString();
    }

    public static Guid? ReadGuid(JsonElement parameters, params string[] names)
    {
        var value = ReadString(parameters, names);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    public static Guid RequireGuid(JsonElement parameters, params string[] names) =>
        ReadGuid(parameters, names) ??
        throw new ReportExportInvalidParametersException($"Report export parameter '{names[0]}' is required.");

    public static bool? ReadBool(JsonElement parameters, params string[] names)
    {
        if (!TryGetProperty(parameters, out var value, names))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    /// <summary>
    /// Reads a boolean using an <b>exact, case-sensitive (ordinal)</b> property
    /// match — unlike <see cref="ReadBool"/>, which resolves via the shared
    /// case-insensitive / first-match <see cref="TryGetProperty"/> helper.
    /// This is the security-sensitive reader for server-controlled flags
    /// (technical-debt doc 01 §N3): it must not be aliased nor case-insensitive,
    /// so a client-supplied key with a different casing can never be picked up
    /// instead of the canonical server-stamped one (and a job queued before the
    /// §N3 fix, carrying both keys, resolves to the canonical one — fail-closed).
    /// </summary>
    public static bool? ReadBoolExact(JsonElement parameters, string name)
    {
        if (!parameters.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    public static int? ReadInt(JsonElement parameters, params string[] names)
    {
        if (!TryGetProperty(parameters, out var value, names))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var parsed) => parsed,
            JsonValueKind.String when int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    public static DateTime? ReadDateTime(JsonElement parameters, params string[] names)
    {
        var value = ReadString(parameters, names);
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
    }

    public static TEnum ReadEnum<TEnum>(JsonElement parameters, TEnum defaultValue, params string[] names)
        where TEnum : struct, Enum =>
        ReadEnum<TEnum>(parameters, null, names) ?? defaultValue;

    public static TEnum? ReadEnum<TEnum>(JsonElement parameters, TEnum? defaultValue, params string[] names)
        where TEnum : struct, Enum
    {
        var value = ReadString(parameters, names);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            ? parsed
            : defaultValue;
    }

    private static bool TryGetProperty(JsonElement parameters, out JsonElement value, params string[] names)
    {
        foreach (var property in parameters.EnumerateObject())
        {
            if (names.Any(name => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
