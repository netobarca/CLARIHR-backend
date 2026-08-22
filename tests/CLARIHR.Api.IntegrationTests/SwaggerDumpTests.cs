using System.Net;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Volcado del contrato OpenAPI generado, para regenerar <c>docs/technical/api/openapi.yaml</c>.
/// No es un guardrail: no afirma nada sobre el contenido. Existe porque el procedimiento de
/// regeneración no estaba documentado en ninguna parte y se resolvía a mano.
///
/// Se ejecuta bajo demanda:
///   dotnet test --filter "FullyQualifiedName~Swagger_DumpContract" -e CLARIHR_DUMP_SWAGGER=1
/// Deja el JSON en TestResults/swagger.json; la conversión a YAML va aparte.
/// </summary>
public sealed class SwaggerDumpTests(IntegrationTestWebApplicationFactory factory)
    : IClassFixture<IntegrationTestWebApplicationFactory>
{
    [Fact]
    public async Task Swagger_DumpContract_WritesGeneratedOpenApiJson()
    {
        if (Environment.GetEnvironmentVariable("CLARIHR_DUMP_SWAGGER") != "1")
        {
            return; // inerte salvo que se pida explícitamente
        }

        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var destino = Path.Combine(Directory.GetCurrentDirectory(), "swagger-dump.json");
        await File.WriteAllTextAsync(destino, await response.Content.ReadAsStringAsync());
        Assert.True(File.Exists(destino));
    }
}
