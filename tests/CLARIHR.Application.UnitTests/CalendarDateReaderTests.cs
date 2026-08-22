using CLARIHR.Domain.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Fija el COMPORTAMIENTO de la lectura de fechas, no solo la ausencia del error. El guardrail hermano
/// (<see cref="CalendarDateTypeGuardrailsTests"/>) verifica que nadie use <c>TryGetDateTime</c> en crudo; esto
/// verifica que lo que se usa en su lugar hace lo correcto.
/// <para>
/// Hallazgo <c>00000 / B-01</c>. Las dos aserciones que importan son las dos que fallaban antes:
/// <c>Kind</c> tiene que salir <c>Utc</c> (con <c>Unspecified</c> Npgsql devuelve <c>500</c>) y el día NO puede
/// moverse cuando el texto trae offset (con <c>TryGetDateTime</c> se movía).
/// </para>
/// </summary>
public sealed class CalendarDateReaderTests
{
    /// <summary>
    /// Las tres formas que circulan nombran el MISMO día y tienen que leerse igual. La tercera es la que
    /// fallaba: `18:07` en `-06:00` son las `00:07Z` del día siguiente, y quien lee el día se llevaba el 16.
    /// </summary>
    [Theory]
    [InlineData("2026-08-15")]                    // la forma natural — antes daba Kind=Unspecified → 500
    [InlineData("2026-08-15T00:00:00Z")]          // la forma que el playbook documentaba como obligatoria
    [InlineData("2026-08-15T18:07:00-06:00")]     // ⚠️ la de F-03 — antes se almacenaba como el 16
    [InlineData("2026-08-15T23:59:59+13:00")]     // el mismo caso desde el otro extremo del planeta
    public void CalendarDate_ReadDayAsUtcMidnight_ShouldKeepTheDayAsWritten(string text)
    {
        Assert.True(CalendarDateReader.TryReadDayAsUtcMidnight(text, out var value));

        // Igualdad exacta, no StartsWith: cualquier hora distinta de medianoche significa que se trató como
        // instante, y cualquier otro día significa que se desplazó.
        Assert.Equal(new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc), value);

        // La aserción que evita el 500: `timestamptz` solo acepta Kind=Utc.
        Assert.Equal(DateTimeKind.Utc, value.Kind);
    }

    [Theory]
    [InlineData("2026-08-15")]
    [InlineData("2026-08-15T18:07:00-06:00")]
    public void CalendarDate_ReadDay_ShouldReturnTheSameDayAsTheUtcMidnightForm(string text)
    {
        Assert.True(CalendarDateReader.TryReadDay(text, out var day));
        Assert.Equal(new DateOnly(2026, 8, 15), day);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no es una fecha")]
    [InlineData("2026-13-45")]
    public void CalendarDate_ReadDay_ShouldRejectWhatIsNotADate(string? text)
    {
        Assert.False(CalendarDateReader.TryReadDay(text, out _));
        Assert.False(CalendarDateReader.TryReadDayAsUtcMidnight(text, out _));
    }

    /// <summary>
    /// Un INSTANTE sí se convierte. Es la asimetría que hay que mantener: relabelar un instante con offset lo
    /// movería seis horas, y convertir un día lo movería un día. Cada uno con su lectura.
    /// </summary>
    [Fact]
    public void CalendarDate_ToUtcInstant_ShouldConvertLocalAndLabelUnspecified()
    {
        var utc = new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(utc, CalendarDateReader.ToUtcInstant(utc));

        var unspecified = new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Unspecified);
        var labelled = CalendarDateReader.ToUtcInstant(unspecified);
        Assert.Equal(DateTimeKind.Utc, labelled.Kind);
        Assert.Equal(12, labelled.Hour);   // etiquetado, no convertido

        var local = new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Local);
        var converted = CalendarDateReader.ToUtcInstant(local);
        Assert.Equal(DateTimeKind.Utc, converted.Kind);
        Assert.Equal(local.ToUniversalTime(), converted);   // convertido, no etiquetado
    }

    /// <summary>
    /// El lector de INSTANTES tiene que coincidir con lo que hace el converter del cuerpo JSON, o `PATCH` y
    /// `PUT` guardarían cosas distintas para el mismo campo (`JobProfile.EffectiveFromUtc` es el caso).
    /// </summary>
    [Theory]
    [InlineData("2026-08-15T12:00:00Z", 12)]        // ya en UTC — intacto
    [InlineData("2026-08-15T12:00:00", 12)]         // sin zona — etiquetado, no convertido
    [InlineData("2026-08-15T06:00:00-06:00", 12)]   // con offset — convertido: 06:00 en -06:00 son las 12:00Z
    public void CalendarDate_ReadInstant_ShouldMatchTheJsonBoundary(string text, int expectedUtcHour)
    {
        Assert.True(CalendarDateReader.TryReadInstant(text, out var value));
        Assert.Equal(DateTimeKind.Utc, value.Kind);
        Assert.Equal(expectedUtcHour, value.Hour);
        Assert.Equal(15, value.Day);
    }

    /// <summary>
    /// La red del agregado: etiqueta, nunca convierte. Con un <c>Kind=Local</c> a las 18:07 la conversión
    /// daría el día siguiente — que es el defecto que este método existe para no cometer.
    /// </summary>
    [Fact]
    public void CalendarDate_NormalizeDay_ShouldLabelTheDayWithoutMovingIt()
    {
        var lateLocal = new DateTime(2026, 8, 15, 18, 7, 0, DateTimeKind.Local);
        var normalized = CalendarDateReader.NormalizeDay(lateLocal);

        Assert.Equal(new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc), normalized);
        Assert.Equal(DateTimeKind.Utc, normalized.Kind);

        Assert.Null(CalendarDateReader.NormalizeDay((DateTime?)null));
    }
}
