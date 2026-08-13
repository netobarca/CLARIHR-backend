using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CLARIHR.Api.Configuration;

/// <summary>
/// H-26 (parte B) — <see cref="DateOnly"/> es el tipo correcto para un campo que semánticamente es un DÍA
/// (<c>startDate</c>, <c>hireDate</c>, <c>birthDate</c>): un día no tiene hora ni zona, así que el problema de
/// <c>Kind=Unspecified</c> no puede existir por construcción.
/// <para>
/// El converter por defecto de .NET solo acepta <c>"2026-08-01"</c> y rechaza <c>"2026-08-01T00:00:00Z"</c>, que es
/// justo la forma que el playbook venía documentando como obligatoria y que usan los clientes actuales. Este acepta
/// las dos y se queda con la fecha: cambiar el tipo no puede romper a quien ya funcionaba.
/// </para>
/// </summary>
public sealed class LenientDateOnlyJsonConverter : JsonConverter<DateOnly>
{
    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Parse(reader.GetString());

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    internal static DateOnly Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new JsonException("A date is required in `yyyy-MM-dd` format.");
        }

        if (DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        // The instant form: keep the calendar day it names. An offset is meaningless for a day field, so the day
        // is read as written rather than shifted into UTC — shifting it would move a birth date across midnight.
        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var instant))
        {
            return DateOnly.FromDateTime(instant.Date);
        }

        throw new JsonException($"'{text}' is not a valid date. Use `yyyy-MM-dd`.");
    }
}

/// <inheritdoc cref="LenientDateOnlyJsonConverter"/>
public sealed class LenientNullableDateOnlyJsonConverter : JsonConverter<DateOnly?>
{
    public override DateOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? null : LenientDateOnlyJsonConverter.Parse(reader.GetString());

    public override void Write(Utf8JsonWriter writer, DateOnly? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
}
