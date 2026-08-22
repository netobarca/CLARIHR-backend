using System.Net;
using System.Text.Json;
using CLARIHR.Application.Features.CostCenters.Common;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.OrgStructureCatalogs.Common;
using CLARIHR.Application.Features.OrgUnits.Common;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// El cliente envía <c>?q=…</c>; si la búsqueda no valida, el error tiene que volver bajo la clave
/// <c>q</c>. Volvía bajo <c>search</c> —el nombre interno del <i>query object</i>— en los cinco
/// endpoints que declaraban el parámetro como <c>q</c> en vez de renombrarlo, dejando al frontend sin
/// forma de asociar el mensaje con su caja de búsqueda.
/// <para>
/// Va un caso por endpoint, con la ruta literal, y no un <c>[Theory]</c> con el recurso interpolado:
/// G6 normaliza cada <c>{…}</c> a <c>{id}</c>, así que un recurso interpolado produce el endpoint
/// fantasma <c>GET /api/v1/companies/{id}/{id}</c> —que nadie cubre— en vez de las rutas reales.
/// </para>
/// </summary>
public sealed partial class ApiIntegrationTests
{
    [Fact]
    public async Task Search_TooShort_OnOrgUnits_ShouldReportTheErrorUnderThePublicKeyQ()
    {
        var (client, companyId) = await AbrirClienteDeBusquedaAsync();
        using var _ = client;

        var response = await client.GetAsync($"/api/v1/companies/{companyId}/organization-units?q=a");

        await AssertClaveDeBusquedaEsPublicaAsync(response, "organization-units");
    }

    [Fact]
    public async Task Search_TooShort_OnWorkCenters_ShouldReportTheErrorUnderThePublicKeyQ()
    {
        var (client, companyId) = await AbrirClienteDeBusquedaAsync();
        using var _ = client;

        var response = await client.GetAsync($"/api/v1/companies/{companyId}/work-centers?q=a");

        await AssertClaveDeBusquedaEsPublicaAsync(response, "work-centers");
    }

    [Fact]
    public async Task Search_TooShort_OnWorkCenterTypes_ShouldReportTheErrorUnderThePublicKeyQ()
    {
        var (client, companyId) = await AbrirClienteDeBusquedaAsync();
        using var _ = client;

        var response = await client.GetAsync($"/api/v1/companies/{companyId}/work-center-types?q=a");

        await AssertClaveDeBusquedaEsPublicaAsync(response, "work-center-types");
    }

    [Fact]
    public async Task Search_TooShort_OnCostCenterTypes_ShouldReportTheErrorUnderThePublicKeyQ()
    {
        var (client, companyId) = await AbrirClienteDeBusquedaAsync();
        using var _ = client;

        var response = await client.GetAsync($"/api/v1/companies/{companyId}/cost-center-types?q=a");

        await AssertClaveDeBusquedaEsPublicaAsync(response, "cost-center-types");
    }

    [Fact]
    public async Task Search_TooShort_OnLocationGroups_ShouldReportTheErrorUnderThePublicKeyQ()
    {
        var (client, companyId) = await AbrirClienteDeBusquedaAsync();
        using var _ = client;

        var response = await client.GetAsync($"/api/v1/companies/{companyId}/location-groups?q=a");

        await AssertClaveDeBusquedaEsPublicaAsync(response, "location-groups");
    }

    /// <summary>
    /// Control: este endpoint ya declaraba <c>[FromQuery(Name = "q")] string? search</c>. Verifica que
    /// uniformar a los otros cinco no rompe a los que ya estaban bien.
    /// </summary>
    [Fact]
    public async Task Search_TooShort_OnUnitTypes_ShouldKeepReportingTheErrorUnderThePublicKeyQ()
    {
        var (client, companyId) = await AbrirClienteDeBusquedaAsync();
        using var _ = client;

        var response = await client.GetAsync(
            $"/api/v1/companies/{companyId}/organization-structure-catalogs/unit-types?q=a");

        await AssertClaveDeBusquedaEsPublicaAsync(response, "unit-types");
    }

    private async Task<(HttpClient Client, Guid CompanyId)> AbrirClienteDeBusquedaAsync()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var client = factory.CreateClientFor(TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            OrgUnitPermissionCodes.Admin,
            LocationPermissionCodes.Admin,
            CostCenterPermissionCodes.Admin,
            OrgStructureCatalogPermissionCodes.Admin));

        return (client, scenario.TenantId);
    }

    private static async Task AssertClaveDeBusquedaEsPublicaAsync(HttpResponseMessage response, string recurso)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var claves = problem.RootElement.GetProperty("errors")
            .EnumerateObject()
            .Select(campo => campo.Name)
            .ToArray();

        Assert.True(
            claves.Contains("q"),
            $"`{recurso}` devolvió las claves [{string.Join(", ", claves)}]; se esperaba `q`, " +
            "que es el nombre con el que el cliente envió el campo.");
        Assert.DoesNotContain("search", claves, StringComparer.OrdinalIgnoreCase);
    }
}
