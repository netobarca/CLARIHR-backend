using System.Text.RegularExpressions;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// 00005 / B-01 — <b>los nombres sembrados se muestran al usuario y salen en los reportes legales.</b>
/// <para>
/// El hallazgo se levantó por seis departamentos de El Salvador mal escritos. Al medirlo el 2026-08-20
/// eran <b>81 filas en 15 tablas</b>: los catorce municipios de esos departamentos heredaban el error
/// porque su nombre se derivaba del código ASCII, y además había «Banco Agricola», «Espanol», «Dia»,
/// «Viaticos» y los nombres de dieciséis permisos.
/// </para>
/// <para>
/// Esta prueba lee los archivos de siembra del disco y falla si un texto de presentación vuelve a
/// escribirse sin su tilde o su eñe. No mira códigos: solo literales con espacio o de la lista conocida,
/// para no tocar jamás un identificador.
/// </para>
/// </summary>
public sealed class SeedDisplayTextTests
{
    private static readonly string[] ArchivosDeSiembra =
    [
        Path.Combine("src", "CLARIHR.Application", "Features", "Provisioning", "Common", "ProvisioningConstants.cs"),
        Path.Combine("src", "CLARIHR.Infrastructure", "Persistence", "GlobalCatalogSeedData.cs"),
        Path.Combine("src", "CLARIHR.Domain", "OrgStructureCatalogs", "CompanyTypeCatalog.cs"),
        Path.Combine("src", "CLARIHR.Domain", "PersonnelFiles", "PersonnelReferenceCatalog.cs"),
        Path.Combine("src", "CLARIHR.Domain", "Locations", "ElSalvadorTerritorialCatalog.cs"),
    ];

    /// <summary>
    /// Palabras españolas que en este corpus nunca son correctas sin tilde o sin eñe. Deliberadamente NO
    /// incluye homógrafos —<c>cambio</c>, <c>registro</c>, <c>este</c>, <c>publico</c>, <c>numero</c>—
    /// ni palabras que también son inglesas: los países se siembran en inglés a propósito
    /// (<c>France</c>, <c>Brazil</c>) y un guardrail con falsos positivos acaba desactivado.
    /// </summary>
    private static readonly string[] SinTilde =
    [
        "administracion", "agricola", "ahuachapan", "amonestacion", "anonima", "aplicacion", "aprobacion",
        "asociacion", "basico", "cabanas", "catalogo", "catalogos", "certificacion", "clinicas",
        "comision", "compensacion", "configuracion", "cotizacion", "creacion", "cuscatlan", "dano",
        "descripcion", "diagnostico", "disenador", "dolar", "economica", "economico", "espanol",
        "especifico", "grafico", "informacion", "institucion", "ingles", "jerarquia", "medica", "medicas",
        "medico", "morazan", "nomina", "nominas", "ocupacion", "odontologo", "operacion", "piramide",
        "procuraduria", "produccion", "psicologo", "recontratacion", "regimen", "revision", "simulacion",
        "tecnica", "tecnico", "unica", "usulutan", "variacion", "viaticos",
    ];

    [Fact]
    public void SeedDisplayText_ShouldNotDropDiacritics()
    {
        var raiz = ResolveRepositoryRoot();
        var ofensores = new List<string>();

        foreach (var relativa in ArchivosDeSiembra)
        {
            var ruta = Path.Combine(raiz, relativa);
            Assert.True(File.Exists(ruta), $"No existe el archivo de siembra: {relativa}");

            var contenido = File.ReadAllText(ruta);
            foreach (Match literal in Regex.Matches(contenido, "\"([^\"\\\\\n]*)\""))
            {
                var texto = literal.Groups[1].Value;
                if (!EsTextoDePresentacion(texto) || EsClaveDeBusqueda(contenido, literal.Index))
                {
                    continue;
                }

                var malas = SinTilde
                    .Where(w => Regex.IsMatch(texto, $@"\b{w}\b", RegexOptions.IgnoreCase))
                    .ToArray();

                if (malas.Length > 0)
                {
                    ofensores.Add($"  {Path.GetFileName(relativa)}: «{texto}»  →  {string.Join(", ", malas)}");
                }
            }
        }

        Assert.True(
            ofensores.Count == 0,
            $"{ofensores.Count} textos sembrados pierden tildes o eñes:\n" +
            string.Join('\n', ofensores.Take(25)));
    }

    /// <summary>
    /// Un literal es texto de presentación si lleva espacio (los códigos e identificadores nunca lo
    /// llevan) o si empieza con mayúscula seguida de minúsculas, que es la forma de los nombres de una
    /// sola palabra («Español», «Básico»). Los códigos van en MAYÚSCULAS o en PascalCase con punto.
    /// </summary>
    private static bool EsTextoDePresentacion(string texto)
    {
        if (texto.Length < 4 || texto.Contains('.') || texto.Contains('_'))
        {
            return false;
        }

        return texto.Contains(' ') || Regex.IsMatch(texto, "^[A-ZÁÉÍÓÚÑ][a-záéíóúñ]+$");
    }

    /// <summary>
    /// Un literal precedido de <c>normalizedName:</c> es una clave de búsqueda, no un texto visible: va
    /// en ASCII a propósito para que se pueda encontrar «Cuscatlán» escribiendo «cuscatlan». Sin esta
    /// excepción el guardrail marcaba trabajo correcto, que es la forma más rápida de que lo desactiven.
    /// </summary>
    private static bool EsClaveDeBusqueda(string contenido, int posicionDelLiteral)
    {
        const string Marca = "normalizedName:";
        var desde = Math.Max(0, posicionDelLiteral - Marca.Length - 2);
        return contenido[desde..posicionDelLiteral].Contains(Marca, StringComparison.Ordinal);
    }

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
