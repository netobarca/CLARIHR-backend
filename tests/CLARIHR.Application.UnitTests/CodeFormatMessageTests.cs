using System.Reflection;
using System.Text.RegularExpressions;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// 00002 / B-03 — <b>el mensaje de formato tiene que decir la verdad sobre su propia regla.</b>
/// <para>
/// Antes los 46 sitios decían «Code format is invalid.», que no dice cuál es el formato. Al medirlo
/// apareció por qué nadie lo había arreglado con un texto compartido: <b>las reglas no son iguales</b>
/// —hay de 50 y de 80 caracteres, unas admiten punto y barra y otras no—, así que una sola frase
/// habría sido falsa en la mayoría de los sitios.
/// </para>
/// <para>
/// Cada clase de reglas declara ahora su propio <c>CodeFormatMessage</c> junto a su regex. Esta prueba
/// las descubre <b>por reflexión</b>: una clase nueva con las dos piezas queda cubierta sin tocar este
/// archivo, y una que las tenga descuadradas falla.
/// </para>
/// </summary>
public sealed class CodeFormatMessageTests
{
    private static readonly Dictionary<string, char> CaracteresPorNombre = new(StringComparer.Ordinal)
    {
        ["hyphen"] = '-',
        ["underscore"] = '_',
        ["period"] = '.',
        ["slash"] = '/',
    };

    /// <summary>Caracteres que ninguna regla de código admite; si alguno pasa, la frase miente por omisión.</summary>
    private static readonly char[] NuncaPermitidos = [' ', '@', '#', '%', 'ñ', '('];

    public static TheoryData<string> Reglas()
    {
        var datos = new TheoryData<string>();
        foreach (var tipo in DescubrirReglas())
        {
            datos.Add(tipo.FullName!);
        }

        return datos;
    }

    [Fact]
    public void Discovery_ShouldFindEveryRulesClass()
    {
        // Contrapeso: si la reflexión dejara de encontrar tipos, las pruebas de abajo pasarían vacías
        // y el guardrail sería decoración. Al escribirlo eran 12.
        Assert.True(
            DescubrirReglas().Count >= 12,
            $"Se esperaban al menos 12 clases con CodeFormatMessage; se encontraron {DescubrirReglas().Count}.");
    }

    [Theory]
    [MemberData(nameof(Reglas))]
    public void CodeFormatMessage_ShouldStateTheRealMaximumLength(string nombreTipo)
    {
        var (mensaje, esValido) = Resolver(nombreTipo);
        var maximo = MaximoDeclarado(mensaje);

        var justo = new string('A', maximo);
        var unoDeMas = new string('A', maximo + 1);

        Assert.True(esValido(justo), $"{nombreTipo}: el mensaje promete {maximo} caracteres pero la regla los rechaza.");
        Assert.False(esValido(unoDeMas), $"{nombreTipo}: el mensaje promete un máximo de {maximo} pero la regla acepta {maximo + 1}.");
    }

    [Theory]
    [MemberData(nameof(Reglas))]
    public void CodeFormatMessage_ShouldStateExactlyTheAllowedCharacters(string nombreTipo)
    {
        var (mensaje, esValido) = Resolver(nombreTipo);
        var anunciados = CaracteresPorNombre
            .Where(par => mensaje.Contains(par.Key, StringComparison.OrdinalIgnoreCase))
            .Select(par => par.Value)
            .ToArray();

        foreach (var c in anunciados)
        {
            Assert.True(esValido($"A{c}B"), $"{nombreTipo}: el mensaje anuncia '{c}' pero la regla lo rechaza.");
        }

        foreach (var c in CaracteresPorNombre.Values.Except(anunciados))
        {
            Assert.False(esValido($"A{c}B"), $"{nombreTipo}: la regla acepta '{c}' y el mensaje no lo dice.");
        }

        foreach (var c in NuncaPermitidos)
        {
            Assert.False(esValido($"A{c}B"), $"{nombreTipo}: la regla acepta '{c}', que el mensaje no menciona.");
        }
    }

    [Theory]
    [MemberData(nameof(Reglas))]
    public void CodeFormatMessage_ShouldRequireALetterOrDigitFirst(string nombreTipo)
    {
        var (_, esValido) = Resolver(nombreTipo);

        Assert.False(esValido("-AB"), $"{nombreTipo}: el mensaje dice que empieza con letra o número, pero '-AB' pasa.");
        Assert.True(esValido("A1B"), $"{nombreTipo}: 'A1B' debería ser válido.");
    }

    private static int MaximoDeclarado(string mensaje)
    {
        var m = Regex.Match(mensaje, @"up to (?<n>\d+) characters", RegexOptions.IgnoreCase);
        Assert.True(m.Success, $"El mensaje no declara un máximo legible: «{mensaje}»");
        return int.Parse(m.Groups["n"].Value);
    }

    private static (string Mensaje, Func<string, bool> EsValido) Resolver(string nombreTipo)
    {
        var tipo = DescubrirReglas().Single(t => t.FullName == nombreTipo);
        var mensaje = (string)tipo.GetField("CodeFormatMessage", BindingFlags.Public | BindingFlags.Static)!
            .GetRawConstantValue()!;
        var metodo = tipo.GetMethod("IsValidCode", BindingFlags.Public | BindingFlags.Static)!;
        return (mensaje, code => (bool)metodo.Invoke(null, [code])!);
    }

    private static List<Type> DescubrirReglas() =>
        typeof(CLARIHR.Application.DependencyInjection).Assembly
            .GetTypes()
            .Where(t => t.IsClass
                     && t.GetField("CodeFormatMessage", BindingFlags.Public | BindingFlags.Static) is not null
                     && t.GetMethod("IsValidCode", BindingFlags.Public | BindingFlags.Static) is not null)
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();
}
