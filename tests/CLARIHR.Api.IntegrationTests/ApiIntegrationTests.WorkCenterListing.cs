using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Application.Features.Locations.Common;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Las dos listas de centros de trabajo no tenían ninguna prueba de camino feliz: se creaban centros en
/// otros casos, pero nadie comprobaba que <b>listarlos</b> devolviera algo. El hueco salió a la luz al
/// añadir la prueba de la clave <c>q</c> — G6 acusa un endpoint cuya única cobertura es de error, y el
/// primero en tener cobertura de error fue el que reveló que no tenía ninguna otra.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    [Fact]
    public async Task WorkCenters_List_ShouldReturnTheCentersOfTheCompany()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(
            scenario.ActorUserId, scenario.TenantId, LocationPermissionCodes.Admin));

        var creado = await CrearCentroDeTrabajoParaListadoAsync(client, scenario.TenantId, "LST");

        var response = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/work-centers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var raiz = document.RootElement;

        Assert.True(raiz.GetProperty("totalCount").GetInt32() >= 1);

        var codigos = raiz.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("code").GetString())
            .ToArray();

        Assert.Contains(creado, codigos);
    }

    [Fact]
    public async Task WorkCenterTypes_List_ShouldReturnTheTypesOfTheCompany()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(
            scenario.ActorUserId, scenario.TenantId, LocationPermissionCodes.Admin));

        var typeResponse = await client.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/work-center-types",
            new
            {
                code = "TYPE-LST",
                name = "Tipo para listado",
                requiresAddress = false,
                requiresGeo = false,
                allowsBiometric = false,
            });
        typeResponse.EnsureSuccessStatusCode();

        var response = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/work-center-types");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var raiz = document.RootElement;

        Assert.True(raiz.GetProperty("totalCount").GetInt32() >= 1);

        var codigos = raiz.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("code").GetString())
            .ToArray();

        Assert.Contains("TYPE-LST", codigos);
    }

    private async Task<string> CrearCentroDeTrabajoParaListadoAsync(HttpClient client, Guid companyId, string tag)
    {
        var defaultGroup = await GetDefaultLocationGroupAsync(client, companyId);

        var typeResponse = await client.PostJsonAsync($"/api/v1/companies/{companyId}/work-center-types", new
        {
            code = $"TYPE-{tag}",
            name = $"Tipo {tag}",
            requiresAddress = false,
            requiresGeo = false,
            allowsBiometric = false,
        });
        typeResponse.EnsureSuccessStatusCode();
        var workCenterType = await typeResponse.Content.ReadFromJsonAsync<WorkCenterTypeItem>(JsonOptions);

        var codigo = $"CEN-{tag}";
        var centerResponse = await client.PostJsonAsync($"/api/v1/companies/{companyId}/work-centers", new
        {
            code = codigo,
            name = $"Centro {tag}",
            workCenterTypePublicId = workCenterType!.Id,
            locationGroupPublicId = defaultGroup.Id,
            address = "San Salvador",
            geoLat = (decimal?)null,
            geoLong = (decimal?)null,
            phone = "2222-2222",
            email = "listado@acme-one.test",
            notes = (string?)null,
        });
        centerResponse.EnsureSuccessStatusCode();

        return codigo;
    }
}
