using System.Text.Json;
using System.Text.Json.Serialization;

namespace CLARIHR.Api.Configuration;

/// <summary>
/// H-26 — every date that enters this API is normalized to UTC here, at the boundary.
/// <para>
/// A body carrying <c>"2026-08-01"</c> — the natural way to write a date — deserialized with
/// <c>Kind=Unspecified</c>, and the first thing that touched it was a SQL comparison (the assignment's capacity
/// check, a report's date filter), where Npgsql refuses a parameter whose zone it does not know: <c>500</c>. The
/// aggregate normalized on construction, so the write was never the problem — the pre-write QUERY was, and no
/// amount of normalizing inside the domain could have helped.
/// </para>
/// <para>
/// The three cases are NOT symmetric, and the middle one is the trap:
/// <list type="bullet">
/// <item><c>Unspecified</c> → labelled UTC. That is what the domain already did (<c>NormalizeDate</c>) and what
/// the whole system stores.</item>
/// <item><c>Local</c> (an explicit offset like <c>-06:00</c>) → <b>converted</b> with
/// <see cref="DateTime.ToUniversalTime"/>. Relabelling it would move the instant by the offset.</item>
/// <item><c>Utc</c> → untouched.</item>
/// </list>
/// </para>
/// </summary>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        ToUtc(reader.GetDateTime());

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(ToUtc(value));

    internal static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}

/// <inheritdoc cref="UtcDateTimeJsonConverter"/>
public sealed class UtcNullableDateTimeJsonConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? null : UtcDateTimeJsonConverter.ToUtc(reader.GetDateTime());

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(UtcDateTimeJsonConverter.ToUtc(value.Value));
    }
}
