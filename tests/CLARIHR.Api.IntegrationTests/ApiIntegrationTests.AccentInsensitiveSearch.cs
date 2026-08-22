using System.Net;
using System.Text.Json;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.OrgUnits.Common;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// 00005 / B-01 (§2.10) — <b>buscar sin tilde tiene que encontrar lo acentuado.</b>
/// <para>
/// El defecto medido: los repositorios comparaban <c>NormalizedName.Contains(search.ToUpperInvariant())</c>
/// y ninguno de los dos lados quitaba diacríticos, así que «estacion» no encontraba «Estación SAL». Había
/// 14 filas de usuarios afectadas en la base de desarrollo.
/// </para>
/// </summary>
public sealed partial class ApiIntegrationTests
{
    [Fact]
    public async Task Search_WithoutAccents_ShouldFindAccentedNames()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, OrgUnitPermissionCodes.Admin));

        await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-ACENTO", "Dirección de Operación", "Direccion");

        var url = $"/api/v1/companies/{scenario.TenantId}/organization-units";
        Assert.Equal(1, await ContarAsync(client, $"{url}?q=direccion&pageSize=1"));
        Assert.Equal(1, await ContarAsync(client, $"{url}?q=Dirección&pageSize=1"));
        Assert.Equal(1, await ContarAsync(client, $"{url}?q=OPERACION&pageSize=1"));
    }

    /// <summary>
    /// La eñe se pliega igual que las tildes: buscar «canas» encuentra «Cañas». Es el caso que motivó el
    /// hallazgo hermano de los departamentos sembrados.
    /// </summary>
    [Fact]
    public async Task Search_WithoutTilde_ShouldFindEnye()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, OrgUnitPermissionCodes.Admin));

        await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-ENYE", "Cañas Region", "Direccion");

        var url = $"/api/v1/companies/{scenario.TenantId}/organization-units";
        Assert.Equal(1, await ContarAsync(client, $"{url}?q=canas&pageSize=1"));
        Assert.Equal(1, await ContarAsync(client, $"{url}?q=Cañas&pageSize=1"));
    }

    /// <summary>
    /// El contrapeso que impide que «plegar» se convierta en «encontrar cualquier cosa»: un término que no
    /// está no debe aparecer. Sin esto, un filtro que dejara de filtrar pasaría las dos pruebas de arriba.
    /// </summary>
    [Fact]
    public async Task Search_WithAnUnrelatedTerm_ShouldStillFilter()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, OrgUnitPermissionCodes.Admin));

        await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-FILTRO", "Dirección de Operación", "Direccion");

        var url = $"/api/v1/companies/{scenario.TenantId}/organization-units";
        Assert.Equal(0, await ContarAsync(client, $"{url}?q=zzzznoexiste&pageSize=1"));
    }
}
