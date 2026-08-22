using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Construye la huella del contrato a partir del swagger generado. **Una sola implementación**, usada
/// tanto por el volcador que escribe el archivo versionado como por el guardrail que lo verifica.
///
/// El primer intento tuvo dos —una en Python para generar y otra en C# para comparar— y no coincidían:
/// las 901 operaciones salían como «ausentes». Es la misma clase de defecto que la huella existe para
/// evitar: dos fuentes de verdad para lo mismo divergen siempre.
/// </summary>
internal static class ContractFingerprint
{
    private static readonly string[] Verbs = ["get", "post", "put", "patch", "delete"];

    public static IReadOnlyList<string> Build(string swaggerJson)
    {
        using var document = JsonDocument.Parse(swaggerJson);
        var lines = new List<string>();

        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var verb in Verbs)
            {
                if (!path.Value.TryGetProperty(verb, out var operation))
                {
                    continue;
                }

                var codes = operation.TryGetProperty("responses", out var responses)
                    ? string.Join('/', responses.EnumerateObject().Select(static r => r.Name).Order(StringComparer.Ordinal))
                    : string.Empty;

                lines.Add($"{verb.ToUpperInvariant()} {path.Name} {codes} {ShapeHash(operation)}");
            }
        }

        lines.Sort(StringComparer.Ordinal);
        return lines;
    }

    /// <summary>
    /// Hash de la FORMA: parámetros, cuerpo y respuestas. Excluye a propósito `summary`,
    /// `description` y `tags` — reescribir una descripción no cambia el contrato, y si lo contara,
    /// cada ajuste de redacción obligaría a regenerar la huella y el guardrail se volvería ruido que
    /// la gente aprende a ignorar. Medido: de 15 rutas «distintas» al comparar el openapi.yaml, 12
    /// eran reflujo de línea en descripciones y solo 3 tenían diferencia real de contrato.
    /// </summary>
    private static string ShapeHash(JsonElement operation)
    {
        var shape = new
        {
            p = operation.TryGetProperty("parameters", out var parameters) ? parameters.GetRawText() : null,
            b = operation.TryGetProperty("requestBody", out var body) ? body.GetRawText() : null,
            r = operation.TryGetProperty("responses", out var responses) ? responses.GetRawText() : null
        };

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(shape)));
        return Convert.ToHexStringLower(bytes)[..12];
    }

    /// <summary>Localiza el archivo versionado subiendo hasta la raíz del repositorio.</summary>
    public static string VersionedPath()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CLARIHR.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("No se encontró la raíz del repositorio (CLARIHR.slnx).");
        }

        return Path.Combine(directory.FullName, "docs", "technical", "api", "contract-fingerprint.txt");
    }

    public static string Header(int operationCount) =>
        string.Join('\n',
            "# Huella del contrato público de CLARIHR — generada, NO editar a mano.",
            "#",
            "# Una linea por operacion: VERBO ruta codigos-de-respuesta hash-de-la-forma.",
            "# El hash cubre parametros, cuerpo de peticion y respuestas. Si cambia, cambio el contrato.",
            "#",
            "# Existe porque `openapi.yaml` (2.7 MB, 89k lineas) no es verificable a escala: a 10.000",
            "# operaciones serian ~30 MB. Esta huella son ~95 KB hoy y ~1 MB a 10.000, y el guardrail",
            "# ContractFingerprintGuardrailsTests la compara contra el codigo en cada corrida.",
            "#",
            "# Regenerar: ver docs/technical/operations/regenerar-contrato.md",
            $"# operaciones: {operationCount}",
            "");
}
