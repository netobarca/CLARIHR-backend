using System.Globalization;

namespace CLARIHR.Domain.Common;

/// <summary>
/// La única forma de leer una fecha que llega escrita como texto. Vive en el dominio porque es la capa que
/// <c>CLARIHR.Api</c> (los converters de la frontera) y <c>CLARIHR.Application</c> (los lectores de JSON Patch)
/// pueden ver las dos; tenerla dos veces fue exactamente el defecto.
/// <para>
/// <b>La distinción que gobierna todo esto (§1.2 de las definiciones técnicas):</b> un <b>día</b> no tiene hora
/// ni zona, un <b>instante</b> tiene las dos. Leer un día como si fuera un instante es lo que corre la fecha:
/// <c>2026-08-15T18:07:00-06:00</c> convertido a UTC es el <b>16</b> de agosto, y quien pidió «el día del
/// nombramiento» acaba de perder un día.
/// </para>
/// <para>
/// Origen: H-26 normalizó la frontera pero dejó fuera el cuerpo de JSON Patch, que no llega como
/// <c>DateTime</c> sino como <c>JsonElement</c> y por lo tanto no pasa por ningún converter — hallazgo
/// <c>00000 / B-01</c>.
/// </para>
/// </summary>
public static class CalendarDateReader
{
    /// <summary>
    /// Lee un texto que nombra un <b>DÍA</b> y devuelve el día <b>tal como está escrito</b>.
    /// <para>
    /// Acepta las dos formas que circulan: <c>"2026-08-15"</c> (la natural) y <c>"2026-08-15T00:00:00Z"</c>
    /// (la que el playbook venía documentando como obligatoria). <b>Un offset no desplaza el día</b>: para un
    /// campo de día el offset no significa nada, y aplicarlo movería una fecha de nacimiento al otro lado de
    /// la medianoche.
    /// </para>
    /// </summary>
    public static bool TryReadDay(string? text, out DateOnly day)
    {
        day = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDay))
        {
            day = parsedDay;
            return true;
        }

        // La forma de instante: se conserva el día del calendario que nombra. `DateTimeOffset.Date` es la
        // fecha en el propio offset del texto, no en UTC — que es justo lo que hace falta aquí.
        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var instant))
        {
            day = DateOnly.FromDateTime(instant.Date);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Igual que <see cref="TryReadDay"/>, pero devuelve el día en la forma de instante que todavía usan las
    /// columnas <c>timestamptz</c> que no se han migrado: <b>medianoche UTC de ese día</b>.
    /// <para>
    /// Es el puente para los campos que aún son <c>DateTime</c> (hallazgo <c>00000 / B-02</c>): garantiza
    /// <c>Kind=Utc</c> —sin lo cual Npgsql rechaza el parámetro y devuelve <c>500</c>— <b>y</b> garantiza que
    /// el día no se mueva. Cuando el campo pase a <c>DateOnly</c>, el llamador cambia a
    /// <see cref="TryReadDay"/> y el comportamiento observable no cambia.
    /// </para>
    /// </summary>
    public static bool TryReadDayAsUtcMidnight(string? text, out DateTime value)
    {
        if (TryReadDay(text, out var day))
        {
            value = DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Lee un texto que nombra un <b>INSTANTE</b> y lo normaliza a UTC, con las mismas tres reglas que aplica
    /// el converter del cuerpo JSON (<see cref="ToUtcInstant"/>).
    /// <para>
    /// Es el lector para los campos que el agregado trata como instante — <c>JobProfile.EffectiveFromUtc</c>,
    /// por ejemplo, cuyo <c>PUT</c> tampoco trunca—. Usar aquí la lectura de día haría que <c>PATCH</c> y
    /// <c>PUT</c> guardaran cosas distintas para el mismo campo. Que esos campos <i>deberían</i> ser días es
    /// cierto y está levantado como <c>00000 / B-02</c>: se corrige cambiando el tipo, no divergiendo los dos
    /// caminos de escritura.
    /// </para>
    /// </summary>
    public static bool TryReadInstant(string? text, out DateTime value)
    {
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            value = ToUtcInstant(parsed);
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Normaliza un <b>DÍA</b> que ya viene como <see cref="DateTime"/>: se queda con la fecha y la etiqueta
    /// UTC, sin convertir. Es la red del agregado, equivalente a
    /// <c>PersonnelFileNormalization.NormalizeDate</c>, para las entidades que aún guardan días en
    /// <c>timestamptz</c>.
    /// <para>
    /// <b>Etiquetar, no convertir</b>: convertir movería la fecha, que es precisamente el defecto.
    /// </para>
    /// </summary>
    public static DateTime NormalizeDay(DateTime value) =>
        DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);

    /// <inheritdoc cref="NormalizeDay(DateTime)"/>
    public static DateTime? NormalizeDay(DateTime? value) =>
        value.HasValue ? NormalizeDay(value.Value) : null;

    /// <summary>
    /// Normaliza un <b>INSTANTE</b> a UTC. Los tres casos NO son simétricos, y el de en medio es la trampa:
    /// <list type="bullet">
    /// <item><c>Utc</c> → intacto.</item>
    /// <item><c>Local</c> (offset explícito) → <b>convertido</b>. Reetiquetarlo movería el instante.</item>
    /// <item><c>Unspecified</c> → etiquetado UTC, que es lo que el sistema entero almacena.</item>
    /// </list>
    /// ⚠️ No usar para días: convertir un día desplaza la fecha. Para eso está
    /// <see cref="TryReadDayAsUtcMidnight"/>.
    /// </summary>
    public static DateTime ToUtcInstant(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
