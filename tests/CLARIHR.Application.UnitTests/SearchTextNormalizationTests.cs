using CLARIHR.Domain.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// 00005 / B-01 (§2.10) — <b>el plegado que hace que buscar sin tilde encuentre lo acentuado.</b>
/// <para>
/// La propiedad que de verdad importa no es cómo se ve la salida, sino que <b>los dos lados coincidan</b>:
/// lo que se guarda y lo que se busca pasan por la misma función. Si divergieran, la búsqueda dejaría de
/// encontrar cosas que hoy encuentra, que es peor que el defecto original.
/// </para>
/// </summary>
public sealed class SearchTextNormalizationTests
{
    [Theory]
    [InlineData("Estación", "ESTACION")]
    [InlineData("Cañas", "CANAS")]
    [InlineData("Ahuachapán", "AHUACHAPAN")]
    [InlineData("Banco Agrícola", "BANCO AGRICOLA")]
    [InlineData("José Ángel", "JOSE ANGEL")]
    [InlineData("Müller", "MULLER")]
    public void Fold_ShouldStripDiacriticsAndUppercase(string entrada, string esperado) =>
        Assert.Equal(esperado, SearchTextNormalization.Fold(entrada));

    /// <summary>
    /// El contrapeso: <b>solo</b> se quitan marcas diacríticas. Un guion largo, un signo o un número no se
    /// tocan — si se tocaran, el plegado estaría cambiando el texto en vez de normalizarlo.
    /// </summary>
    [Theory]
    [InlineData("SAL — Aeropuerto", "SAL — AEROPUERTO")]
    [InlineData("PLAN-2026/A", "PLAN-2026/A")]
    [InlineData("ABC", "ABC")]
    public void Fold_ShouldLeaveEverythingElseAlone(string entrada, string esperado) =>
        Assert.Equal(esperado, SearchTextNormalization.Fold(entrada));

    /// <summary>
    /// Idempotente: plegar lo ya plegado no cambia nada. Es lo que permite que la migración de datos se
    /// pueda repetir sin miedo, y que un valor que ya pasó por aquí no se degrade en la siguiente escritura.
    /// </summary>
    [Theory]
    [InlineData("Estación")]
    [InlineData("Cañas")]
    [InlineData("ya plegado")]
    public void Fold_ShouldBeIdempotent(string entrada)
    {
        var unaVez = SearchTextNormalization.Fold(entrada);
        Assert.Equal(unaVez, SearchTextNormalization.Fold(unaVez));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void FoldSearchTerm_ShouldHandleEmptyInput(string? entrada, string esperado) =>
        Assert.Equal(esperado, SearchTextNormalization.FoldSearchTerm(entrada));

    [Fact]
    public void FoldSearchTerm_ShouldTrimBeforeFolding() =>
        Assert.Equal("ESTACION", SearchTextNormalization.FoldSearchTerm("  Estación  "));
}
