using System.Net;
using System.Text;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Verifica que la huella versionada del contrato coincide con la que genera el código.
///
/// Por qué existe: `openapi.yaml` se desfasó del código durante siete días sin que nada lo detectara
/// — faltaban 9 rutas, entre ellas las tres transiciones de estado de los perfiles de puesto, que son
/// las que bloquean los Pasos 7 y 8 del asistente. El guardrail que ya había pide el swagger al propio
/// host y comprueba etiquetas: compara el código consigo mismo, así que un endpoint ausente del
/// documento publicado le resulta invisible.
///
/// Por qué una huella y no el documento: a 10.000 operaciones el documento serían ~30 MB y ~987k
/// líneas, ilegible en cualquier revisión. La huella son ~95 KB hoy y ~1 MB a esa escala, y el diff
/// dice en una línea qué endpoint cambió de forma.
///
/// Con CLARIHR_WRITE_FINGERPRINT=1 reescribe el archivo en vez de comparar: es el paso 2 del
/// procedimiento de regeneración, y comparte implementación con la comparación para que no puedan
/// divergir.
/// </summary>
public sealed class ContractFingerprintGuardrailsTests(IntegrationTestWebApplicationFactory factory)
    : IClassFixture<IntegrationTestWebApplicationFactory>
{
    [Fact]
    public async Task ContractFingerprint_MatchesTheGeneratedContract()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var actual = ContractFingerprint.Build(await response.Content.ReadAsStringAsync());
        var path = ContractFingerprint.VersionedPath();

        if (Environment.GetEnvironmentVariable("CLARIHR_WRITE_FINGERPRINT") == "1")
        {
            await File.WriteAllTextAsync(
                path,
                ContractFingerprint.Header(actual.Count) + string.Join('\n', actual) + '\n');
            return;
        }

        Assert.True(File.Exists(path), $"No existe la huella versionada en {path}");

        var expected = (await File.ReadAllLinesAsync(path))
            .Where(static line => line.Length > 0 && !line.StartsWith('#'))
            .ToHashSet(StringComparer.Ordinal);

        var missing = actual.Where(line => !expected.Contains(line)).ToArray();
        var stale = expected.Where(line => !actual.Contains(line)).Order(StringComparer.Ordinal).ToArray();

        if (missing.Length == 0 && stale.Length == 0)
        {
            return;
        }

        var report = new StringBuilder()
            .AppendLine("La huella versionada del contrato no coincide con el código.")
            .AppendLine($"Archivo: {path}")
            .AppendLine("Regenerar: ver docs/technical/operations/regenerar-contrato.md")
            .AppendLine();

        Append(report, "EN EL CÓDIGO PERO NO EN LA HUELLA — falta documentar", '+', missing);
        Append(report, "EN LA HUELLA PERO NO EN EL CÓDIGO — documenta algo que ya no existe", '-', stale);

        Assert.Fail(report.ToString());
    }

    private static void Append(StringBuilder report, string title, char marker, string[] lines)
    {
        if (lines.Length == 0)
        {
            return;
        }

        report.AppendLine($"{title} ({lines.Length}):");
        foreach (var line in lines.Take(25))
        {
            report.AppendLine($"  {marker} {line}");
        }

        if (lines.Length > 25)
        {
            report.AppendLine($"  … y {lines.Length - 25} más");
        }

        report.AppendLine();
    }
}
