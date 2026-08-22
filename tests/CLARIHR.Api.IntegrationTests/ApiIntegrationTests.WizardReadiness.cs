using System.Net;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// 00006 / B-02 — <b>el contrato del que depende el asistente de configuración para saber si un paso
/// está de verdad completo.</b>
/// <para>
/// El hallazgo se levantó como defecto de backend, pero la lógica de progreso <b>no vive aquí</b>: no
/// hay ni una ruta de asistente en las 568 del contrato publicado. Lo que sí es responsabilidad de este
/// servidor es que las dos preguntas que el asistente necesita hacer se puedan responder, y que sigan
/// pudiéndose responder mañana.
/// </para>
/// <para>
/// Estas pruebas fijan justo eso. <b>No son un arreglo</b>: el comportamiento ya era correcto. Son el
/// seguro de que nadie lo quite mientras el frontend depende de él.
/// </para>
/// </summary>
public sealed partial class ApiIntegrationTests
{
    /// <summary>
    /// «¿Hay al menos un perfil <b>publicado</b>?» — la condición real del Paso 6, porque una plaza no
    /// se puede crear contra un borrador. Contar filas sin filtrar es lo que hacía que 32 borradores
    /// marcaran el paso en verde.
    /// </summary>
    [Fact]
    public async Task WizardReadiness_JobProfilesFilteredByPublished_ShouldCountOnlyUsableOnes()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-WIZ", "Direccion", "Direccion");
        await CreatePublishedJobProfileAsync(client, scenario.TenantId, orgUnit.Id, "JP-WIZ-PUB");
        await CreateJobProfileAsync(client, scenario.TenantId, "JP-WIZ-DRAFT-1", "Borrador 1", orgUnit.Id);
        await CreateJobProfileAsync(client, scenario.TenantId, "JP-WIZ-DRAFT-2", "Borrador 2", orgUnit.Id);

        var todos = await ContarAsync(client, $"/api/v1/companies/{scenario.TenantId}/job-profiles?pageSize=1");
        var publicados = await ContarAsync(client, $"/api/v1/companies/{scenario.TenantId}/job-profiles?status=Published&pageSize=1");

        // Sin el filtro los dos números serían 3, que es exactamente el defecto que el asistente tenía.
        Assert.Equal(3, todos);
        Assert.Equal(1, publicados);
    }

    /// <summary>
    /// «¿Hay al menos una plaza <b>vacante y vigente</b>?» — la condición del Paso 7. El estado de la
    /// plaza es <b>derivado</b> (no hay columna), así que este filtro podría no llegar a la base de
    /// datos sin que nada avisara: se comprueba que sí lo haga.
    /// </summary>
    [Fact]
    public async Task WizardReadiness_PositionSlotsFilteredByVacant_ShouldFollowTheDerivedStatus()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePositionSlotAdminContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-WIZ-PS", "Direccion", "Direccion");
        var perfil = await CreateJobProfileAsync(client, scenario.TenantId, "JP-WIZ-PS", "Perfil del asistente", orgUnit.Id);
        var plaza = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-WIZ", "Plaza del asistente", perfil.Id, maxEmployees: 1);

        var url = $"/api/v1/companies/{scenario.TenantId}/position-slots";
        Assert.Equal(1, await ContarAsync(client, $"{url}?status=Vacant&isActive=true&pageSize=1"));
        Assert.Equal(0, await ContarAsync(client, $"{url}?status=Suspended&pageSize=1"));

        // Al suspenderla, el estado derivado cambia y el conteo del asistente tiene que seguirlo.
        var suspension = await client.PatchJsonAsync($"/api/v1/position-slots/{plaza.Id}/status", new
        {
            status = "Suspended",
            concurrencyToken = plaza.ConcurrencyToken
        });
        suspension.EnsureSuccessStatusCode();

        Assert.Equal(0, await ContarAsync(client, $"{url}?status=Vacant&isActive=true&pageSize=1"));
        Assert.Equal(1, await ContarAsync(client, $"{url}?status=Suspended&pageSize=1"));
    }

    /// <summary>Una sola llamada barata: el asistente solo necesita <c>totalCount</c>, no las filas.</summary>
    private static async Task<int> ContarAsync(HttpClient client, string url)
    {
        var respuesta = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        return documento.RootElement.GetProperty("totalCount").GetInt32();
    }
}
