using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// 00002 / B-03 — <b>los dos huecos por donde un mensaje de validación se escapa sin traducir.</b>
/// <para>
/// <see cref="BackendMessageLocalizationTests"/> ya exige que cada <c>.WithMessage("literal")</c> tenga
/// clave en el <c>.resx</c>. Su regex es <c>\.WithMessage\("</c>, y ahí está el hueco: <b>no casa con
/// <c>.WithMessage($"…")</c></b> —el <c>$</c> queda en medio— ni con <c>.WithMessage(Constante)</c>.
/// </para>
/// <para>
/// Medido el 2026-08-20: <b>39 mensajes interpolados</b> eran invisibles para ese guardrail y salían en
/// inglés con la suite en verde. Un mensaje interpolado además <b>no se puede verificar de forma
/// estática</b>, porque su clave se calcula en ejecución con el valor de la constante.
/// </para>
/// </summary>
public sealed class ValidationMessageCoverageTests
{
    private static readonly string RepositoryRoot = ResolveRepositoryRoot();
    private static readonly string ApplicationPath = Path.Combine(RepositoryRoot, "src", "CLARIHR.Application");
    private static readonly string EnglishResourcePath = Path.Combine(RepositoryRoot, "src", "CLARIHR.Infrastructure", "Localization", "BackendMessages.resx");
    private static readonly string SpanishResourcePath = Path.Combine(RepositoryRoot, "src", "CLARIHR.Infrastructure", "Localization", "BackendMessages.es.resx");

    /// <summary>
    /// Un mensaje interpolado deriva su clave en ejecución, así que ninguna prueba puede comprobar que
    /// esa clave exista. La alternativa es una constante literal junto a la regla, que sí se verifica.
    /// </summary>
    [Fact]
    public void ValidationMessages_ShouldNeverBeInterpolated()
    {
        var ofensores = Directory
            .EnumerateFiles(ApplicationPath, "*.cs", SearchOption.AllDirectories)
            .SelectMany(ruta => Regex
                .Matches(File.ReadAllText(ruta), @"\.WithMessage\(\$""(?<m>[^""]*)""")
                .Select(m => $"{Path.GetRelativePath(ApplicationPath, ruta)}: {m.Groups["m"].Value}"))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            ofensores.Length == 0,
            $"{ofensores.Length} mensajes de validación están interpolados y no se pueden verificar.\n" +
            "Usa una constante literal junto a la regla (ver CodeFormatMessage / SearchLengthMessage):\n" +
            string.Join('\n', ofensores.Take(20).Select(x => $"  {x}")));
    }

    /// <summary>
    /// Las constantes de mensaje tampoco las ve la regex de la prueba de paridad, porque en el sitio de
    /// uso no hay literal. Se comprueban aquí, por reflexión sobre su valor real.
    /// </summary>
    [Fact]
    public void ValidationMessageConstants_ShouldHaveResourceKeys()
    {
        var ingles = LoadKeys(EnglishResourcePath);
        var espanol = LoadKeys(SpanishResourcePath);
        var constantes = MessageConstants();

        Assert.NotEmpty(constantes);

        var faltan = constantes
            .Where(par => !ingles.Contains(Key(par.Value)) || !espanol.Contains(Key(par.Value)))
            .OrderBy(par => par.Key, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            faltan.Length == 0,
            $"{faltan.Length} constantes de mensaje no tienen entrada en los dos .resx:\n" +
            string.Join('\n', faltan.Select(p => $"  {p.Key}\n    texto: «{p.Value}»\n    clave: {Key(p.Value)}")));
    }

    /// <summary>
    /// El mensaje dice «al menos 2 caracteres». Si alguien sube <c>MinSearchLength</c> y no toca el texto,
    /// el mensaje miente — y como ahora es literal, mentiría en silencio.
    /// </summary>
    [Fact]
    public void SearchLengthMessage_ShouldStateTheRealMinimum()
    {
        var tipos = typeof(CLARIHR.Application.DependencyInjection).Assembly
            .GetTypes()
            .Where(t => t.GetField("SearchLengthMessage", BindingFlags.Public | BindingFlags.Static) is not null
                     && t.GetField("MinSearchLength", BindingFlags.Public | BindingFlags.Static) is not null)
            .ToArray();

        Assert.True(tipos.Length >= 14, $"Se esperaban al menos 14 clases; se encontraron {tipos.Length}.");

        foreach (var tipo in tipos)
        {
            var mensaje = (string)tipo.GetField("SearchLengthMessage", BindingFlags.Public | BindingFlags.Static)!.GetRawConstantValue()!;
            var minimo = (int)tipo.GetField("MinSearchLength", BindingFlags.Public | BindingFlags.Static)!.GetRawConstantValue()!;
            var declarado = Regex.Match(mensaje, @"at least (?<n>\d+) characters");

            Assert.True(declarado.Success, $"{tipo.Name}: el mensaje no declara un mínimo legible: «{mensaje}»");
            Assert.Equal(minimo, int.Parse(declarado.Groups["n"].Value));
        }
    }

    private static string Key(string mensaje)
    {
        var normalizado = Regex.Replace(mensaje.Trim().ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
        return $"validation.message.{(normalizado.Length == 0 ? "generic" : normalizado)}";
    }

    /// <summary>
    /// Solo las constantes que de verdad se pasan a <c>.WithMessage(...)</c>, leídas del código fuente y
    /// resueltas por reflexión.
    /// <para>
    /// ⚠️ La primera versión recogía <b>toda</b> constante terminada en <c>Message</c> y dio 3 falsos
    /// positivos: los avisos de vacaciones y horas extra se localizan por su <c>Code</c> vía
    /// <c>Localize(code, fallback)</c>, no por la clave derivada del texto. Un guardrail que señala
    /// trabajo correcto acaba desactivado, así que aquí se estrecha al uso real.
    /// </para>
    /// </summary>
    private static Dictionary<string, string> MessageConstants()
    {
        var usadas = Directory
            .EnumerateFiles(ApplicationPath, "*.cs", SearchOption.AllDirectories)
            .SelectMany(ruta => Regex
                .Matches(File.ReadAllText(ruta), @"\.WithMessage\(\s*(?<tipo>\w+)\.(?<campo>\w+)\s*\)")
                .Select(m => (Tipo: m.Groups["tipo"].Value, Campo: m.Groups["campo"].Value)))
            .ToHashSet();

        var resultado = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (nombreTipo, nombreCampo) in usadas)
        {
            var tipo = typeof(CLARIHR.Application.DependencyInjection).Assembly
                .GetTypes()
                .FirstOrDefault(t => t.Name == nombreTipo);

            if (tipo?.GetField(nombreCampo, BindingFlags.Public | BindingFlags.Static) is { IsLiteral: true } campo
                && campo.GetRawConstantValue() is string valor
                && valor.Length > 0)
            {
                resultado[$"{nombreTipo}.{nombreCampo}"] = valor;
            }
        }

        return resultado;
    }

    private static HashSet<string> LoadKeys(string ruta) =>
        XDocument.Load(ruta).Root!
            .Elements("data")
            .Select(d => d.Attribute("name")?.Value)
            .Where(n => n is not null)
            .Select(n => n!)
            .ToHashSet(StringComparer.Ordinal);

    private static string ResolveRepositoryRoot()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null && !File.Exists(Path.Combine(directorio.FullName, "CLARIHR.slnx")))
        {
            directorio = directorio.Parent;
        }

        return directorio?.FullName
            ?? throw new InvalidOperationException("No se encontró la raíz del repositorio (CLARIHR.slnx).");
    }
}
