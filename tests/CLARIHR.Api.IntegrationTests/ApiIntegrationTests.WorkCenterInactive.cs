using System.Net;
using System.Net.Http.Json;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.OrgUnits.Common;
using CLARIHR.Application.Features.PositionSlots.Common;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// 00950 / B-02 (§3.6) — <b>no se crean plazas contra un centro de trabajo de baja.</b>
/// <para>
/// Es la misma brecha que se cerró para la unidad organizativa, en el otro padre de la plaza:
/// <c>ResolveWorkCenterIdAsync</c> filtraba por <c>TenantId</c> y <c>PublicId</c> y <b>nunca por
/// <c>IsActive</c></b>. La asimetría era la señal: el <b>tipo</b> de centro ya estaba protegido
/// (<c>WORK_CENTER_TYPE_INACTIVE</c>) y el centro mismo no.
/// </para>
/// <para>
/// Decisión de producto (2026-08-21): igual que con la unidad, se bloquea el salto que <b>crea futuro</b>.
/// Cerrar una sede sigue siendo legítimo y las plazas que ya existen ahí se conservan.
/// </para>
/// </summary>
public sealed partial class ApiIntegrationTests
{
    [Fact]
    public async Task PositionSlots_Create_WithInactiveWorkCenter_ShouldReturn422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId,
                scenario.TenantId,
                PositionSlotPermissionCodes.Admin,
                OrgUnitPermissionCodes.Admin,
                JobProfilePermissionCodes.Admin,
                JobProfilePermissionCodes.Publish,
                LocationPermissionCodes.Admin));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-WC-INACT", "Direccion", "Direccion");
        var perfil = await CreatePublishedJobProfileAsync(client, scenario.TenantId, orgUnit.Id, "JP-WC-INACT");
        var tipo = await CrearTipoDeCentroAsync(client, scenario.TenantId, "TIPO-WC-INACT");
        var centro = await CrearCentroAsync(client, scenario.TenantId, "CEN-WC-INACT", tipo.Id);

        // El centro se da de baja DESPUES de existir: el estado que el defecto permitia explotar.
        var baja = await client.PatchJsonAsync($"/api/v1/work-centers/{centro.Id}/inactivate", new
        {
            concurrencyToken = centro.ConcurrencyToken
        });
        baja.EnsureSuccessStatusCode();

        var response = await CrearPlazaAsync(client, scenario.TenantId, "PS-WC-INACT", perfil.Id, centro.Id);
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "POSITION_SLOT_WORK_CENTER_INACTIVE");
    }

    /// <summary>
    /// Contrapeso: con el centro activo la plaza se crea. Sin el, un guard que rechazara toda plaza con
    /// centro de trabajo pasaria el rojo igual de bien.
    /// </summary>
    [Fact]
    public async Task PositionSlots_Create_WithActiveWorkCenter_ShouldSucceed()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId,
                scenario.TenantId,
                PositionSlotPermissionCodes.Admin,
                OrgUnitPermissionCodes.Admin,
                JobProfilePermissionCodes.Admin,
                JobProfilePermissionCodes.Publish,
                LocationPermissionCodes.Admin));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-WC-ACT", "Direccion", "Direccion");
        var perfil = await CreatePublishedJobProfileAsync(client, scenario.TenantId, orgUnit.Id, "JP-WC-ACT");
        var tipo = await CrearTipoDeCentroAsync(client, scenario.TenantId, "TIPO-WC-ACT");
        var centro = await CrearCentroAsync(client, scenario.TenantId, "CEN-WC-ACT", tipo.Id);

        var response = await CrearPlazaAsync(client, scenario.TenantId, "PS-WC-ACT", perfil.Id, centro.Id);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private Task<HttpResponseMessage> CrearPlazaAsync(
        HttpClient client,
        Guid companyId,
        string code,
        Guid jobProfileId,
        Guid workCenterId) =>
        client.PostJsonAsync($"/api/v1/companies/{companyId}/position-slots", new
        {
            code,
            title = $"Plaza {code}",
            jobProfilePublicId = jobProfileId,
            workCenterPublicId = workCenterId,
            directDependencyPositionSlotPublicId = (Guid?)null,
            functionalDependencyPositionSlotPublicId = (Guid?)null,
            status = "Vacant",
            maxEmployees = 1,
            occupiedEmployees = 0,
            effectiveFromUtc = DateTime.UtcNow.Date,
            effectiveToUtc = (DateTime?)null,
            notes = (string?)null
        });
}
