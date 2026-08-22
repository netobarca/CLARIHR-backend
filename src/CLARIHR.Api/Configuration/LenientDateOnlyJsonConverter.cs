using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Domain.Common;

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

        // B-01 — la lectura vive en `CalendarDateReader`, en el dominio: acepta las dos formas y NUNCA
        // desplaza el día. Los lectores de JSON Patch usan la misma, que es lo que antes faltaba.
        if (CalendarDateReader.TryReadDay(text, out var date))
        {
            return date;
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
