using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// 00002 / B-01 — <b>guardrail de calidad del español que se sirve.</b>
/// <para>
/// La paridad de claves ya la vigila <see cref="BackendMessageLocalizationTests"/>: comprueba que cada
/// código de error TENGA una entrada. Lo que nadie comprobaba es qué DICE esa entrada, y ahí es donde
/// estaba el defecto real: al medirlo el 2026-08-18 había <b>199 mensajes con fragmentos en inglés</b>
/// («Otro cost center ya usa code solicitado») y <b>320 sin tildes</b> («El filtro de mes requiere un
/// ano explicito»). Una clave presente con texto malo pasaba por traducida.
/// </para>
/// <para>
/// Estas pruebas leen el <c>.resx</c> del disco, igual que las de paridad, así que no hacen falta ni
/// base de datos ni host.
/// </para>
/// </summary>
public sealed class SpanishMessageQualityTests
{
    private static readonly string SpanishResourcePath = Path.Combine(
        ResolveRepositoryRoot(), "src", "CLARIHR.Infrastructure", "Localization", "BackendMessages.es.resx");

    private static readonly string EnglishResourcePath = Path.Combine(
        ResolveRepositoryRoot(), "src", "CLARIHR.Infrastructure", "Localization", "BackendMessages.resx");

    /// <summary>
    /// Palabras inequívocamente inglesas. Deliberadamente NO incluye las que también son español
    /// —<c>plan</c>, <c>rol</c>/<c>roles</c>, <c>legal</c>, <c>actual</c>, <c>total</c>, <c>personal</c>—
    /// ni el préstamo <c>endpoint</c>: un guardrail con falsos positivos se desactiva y deja de proteger.
    /// </summary>
    private static readonly HashSet<string> EnglishOnlyWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "is", "are", "was", "were", "been", "being", "this", "that", "these", "those", "of", "to",
        "in", "on", "at", "by", "with", "from", "for", "because", "has", "have", "had", "not", "must",
        "cannot", "already", "requested", "selected", "used", "using", "reached", "additional", "create",
        "reactivate", "administration", "permissions", "context", "current", "user", "users", "found",
        "invalid", "state", "request", "status", "behavior", "competency", "profile", "center", "cost",
        "functional", "addon", "slot", "template", "document", "file", "report", "export", "job",
        "payroll", "run", "entry", "account", "bank", "city", "country", "department", "municipality",
        "limit", "organization", "units", "its", "another", "inline", "catalog", "level", "company",
        "employee", "position", "salary", "payment", "schedule", "work", "assignment", "approval",
        "settlement", "leave", "overtime", "type", "name", "code", "value", "item", "group", "area",
        "team", "rate", "amount", "date", "time", "period", "unit", "belongs", "assigned", "deleted",
        "modified", "threshold", "allowed", "screen", "access", "before", "field", "configured",
        "management", "module", "provider", "external", "different",
    };

    /// <summary>
    /// Formas que en español NUNCA son correctas sin tilde o sin eñe. No incluye homógrafos
    /// —<c>esta</c>/<c>está</c>, <c>este</c>/<c>esté</c>, <c>registro</c>/<c>registró</c>,
    /// <c>cambio</c>/<c>cambió</c>, <c>si</c>/<c>sí</c>— porque dependen del contexto y una regla ciega
    /// metería errores nuevos en vez de quitarlos.
    /// </summary>
    private static readonly string[] MissingDiacritics =
    [
        "valido", "validos", "valida", "validas", "invalido", "invalidos", "invalida", "invalidas",
        "catalogo", "catalogos", "codigo", "codigos", "pais", "paises", "maximo", "maxima", "minimo",
        "minima", "linea", "lineas", "proposito", "limite", "limites", "todavia", "numero", "numeros",
        "modulo", "modulos", "mas", "dia", "dias", "aqui", "segun", "tambien", "despues", "ademas",
        "credito", "debito", "telefono", "electronico", "automatico", "unico", "unica", "ultimo",
        "ultima", "proximo", "basico", "tecnico", "economico", "juridico", "medico", "historico",
        "metrica", "analisis", "sesion", "articulo", "categoria", "jerarquia", "garantia", "guia",
        "politica", "nomina", "antiguedad", "calculo", "estan", "ningun", "aun",
        "contrasena", "contrasenas", "ano", "anos", "espanol", "tamano", "compania", "companias",
        "desempeno", "expiro", "haria", "seria", "podria", "deberia", "tendria", "dejaria",
    ];

    [Fact]
    public void SpanishMessages_ShouldNotContainEnglishFragments()
    {
        var ofensores = LoadValues(SpanishResourcePath)
            .Select(par => (par.Key, par.Value, Palabras: EnglishWordsIn(par.Value)))
            .Where(x => x.Palabras.Count > 0)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            ofensores.Length == 0,
            $"{ofensores.Length} mensajes en español conservan palabras en inglés:\n" +
            string.Join('\n', ofensores.Take(25).Select(x =>
                $"  {x.Key}: «{x.Value}»  →  {string.Join(", ", x.Palabras)}")));
    }

    [Fact]
    public void SpanishMessages_ShouldNotDropDiacritics()
    {
        var ofensores = LoadValues(SpanishResourcePath)
            .Select(par => (par.Key, par.Value, Palabras: MissingDiacriticsIn(par.Value)))
            .Where(x => x.Palabras.Count > 0)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            ofensores.Length == 0,
            $"{ofensores.Length} mensajes en español pierden tildes o eñes:\n" +
            string.Join('\n', ofensores.Take(25).Select(x =>
                $"  {x.Key}: «{x.Value}»  →  {string.Join(", ", x.Palabras)}")));
    }

    /// <summary>
    /// Una entrada copiada del inglés satisface la paridad de claves y no traduce nada. Se ignoran los
    /// textos muy cortos, donde la coincidencia puede ser legítima (nombres propios, siglas).
    /// </summary>
    [Fact]
    public void SpanishMessages_ShouldNotBeVerbatimEnglish()
    {
        var ingles = LoadValues(EnglishResourcePath);
        var ofensores = LoadValues(SpanishResourcePath)
            .Where(par => par.Value.Length > 20
                       && ingles.TryGetValue(par.Key, out var en)
                       && string.Equals(en, par.Value, StringComparison.Ordinal))
            .OrderBy(par => par.Key, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            ofensores.Length == 0,
            $"{ofensores.Length} mensajes en español son idénticos al inglés:\n" +
            string.Join('\n', ofensores.Take(25).Select(p => $"  {p.Key}: «{p.Value}»")));
    }

    private static List<string> EnglishWordsIn(string texto) =>
        Regex.Matches(texto, "[A-Za-zÁÉÍÓÚÜÑáéíóúüñ]+")
            .Select(m => m.Value)
            .Where(w => EnglishOnlyWords.Contains(w))
            .Select(w => w.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static List<string> MissingDiacriticsIn(string texto) =>
        MissingDiacritics
            .Where(w => Regex.IsMatch(texto, $@"\b{w}\b", RegexOptions.IgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static Dictionary<string, string> LoadValues(string ruta) =>
        XDocument.Load(ruta).Root!
            .Elements("data")
            .Where(d => d.Attribute("name") is not null)
            .ToDictionary(
                d => d.Attribute("name")!.Value,
                d => d.Element("value")?.Value ?? string.Empty,
                StringComparer.Ordinal);

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
