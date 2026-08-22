using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Application.Features.OrgStructureCatalogs.Common;
using CLARIHR.Application.Features.Preferences.Common;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// 00003 / B-04 — <b>borrado duro condicional en los tres recursos que faltaban</b>: tipos de centro de
/// trabajo, centros de trabajo y áreas funcionales.
/// <para>
/// Lo que estas pruebas defienden no es el camino feliz —que es mecánico— sino <b>las dos referencias que
/// no tienen clave foránea</b>: la preferencia del tablero apunta al área funcional por su <c>code</c>, y
/// la asignación de expediente apunta al centro de trabajo por su <c>publicId</c>. Ninguna de las dos
/// aparece en el grafo de restricciones de la base, así que un borrado guiado solo por ese grafo las
/// habría dejado colgando <b>sin que la base de datos protestara</b>.
/// </para>
/// </summary>
public sealed partial class ApiIntegrationTests
{
    [Fact]
    public async Task WorkCenterTypes_Delete_WhenNothingReferencesIt_ShouldRemoveIt()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLocationAdminContext(scenario));

        var tipo = await CrearTipoDeCentroAsync(client, scenario.TenantId, "TIPO-BORRABLE");

        var uso = await LeerUsoAsync(client, $"/api/v1/work-center-types/{tipo.Id}/usage");
        Assert.Equal(0, uso.GetProperty("workCenterActiveReferences").GetInt32());
        Assert.False(uso.GetProperty("hasActiveReferences").GetBoolean());

        var borrado = await client.DeleteWithIfMatchAsync($"/api/v1/work-center-types/{tipo.Id}", tipo.ConcurrencyToken);
        Assert.Equal(HttpStatusCode.OK, borrado.StatusCode);

        var despues = await client.GetAsync($"/api/v1/work-center-types/{tipo.Id}");
        Assert.Equal(HttpStatusCode.NotFound, despues.StatusCode);
    }

    /// <summary>
    /// El contrapeso del camino feliz: con un centro colgando, el borrado tiene que negarse. Sin esta
    /// prueba, un guard que no comprobara nada pasaría la anterior igual de bien.
    /// </summary>
    [Fact]
    public async Task WorkCenterTypes_Delete_WhenAWorkCenterReferencesIt_ShouldReturn409()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLocationAdminContext(scenario));

        var tipo = await CrearTipoDeCentroAsync(client, scenario.TenantId, "TIPO-EN-USO");
        _ = await CrearCentroAsync(client, scenario.TenantId, "CEN-EN-USO", tipo.Id);

        var uso = await LeerUsoAsync(client, $"/api/v1/work-center-types/{tipo.Id}/usage");
        Assert.Equal(1, uso.GetProperty("workCenterActiveReferences").GetInt32());

        var borrado = await client.DeleteWithIfMatchAsync($"/api/v1/work-center-types/{tipo.Id}", tipo.ConcurrencyToken);
        await AssertProblemDetailsAsync(borrado, HttpStatusCode.Conflict, "WORK_CENTER_TYPE_IN_USE_FOR_DELETE");
    }

    [Fact]
    public async Task WorkCenters_Delete_WhenNothingReferencesIt_ShouldRemoveIt()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLocationAdminContext(scenario));

        var tipo = await CrearTipoDeCentroAsync(client, scenario.TenantId, "TIPO-CEN-DEL");
        var centro = await CrearCentroAsync(client, scenario.TenantId, "CEN-BORRABLE", tipo.Id);

        var borrado = await client.DeleteWithIfMatchAsync($"/api/v1/work-centers/{centro.Id}", centro.ConcurrencyToken);
        Assert.Equal(HttpStatusCode.OK, borrado.StatusCode);

        var despues = await client.GetAsync($"/api/v1/work-centers/{centro.Id}");
        Assert.Equal(HttpStatusCode.NotFound, despues.StatusCode);
    }

    /// <summary>
    /// ⚠️ <b>La referencia que la base de datos NO protege.</b> La asignación de expediente guarda el
    /// <c>publicId</c> del centro sin clave foránea: sin este guard el borrado habría pasado limpiamente
    /// y habría dejado expedientes apuntando a un centro que ya no existe.
    /// </summary>
    [Fact]
    public async Task WorkCenters_Delete_WhenAPersonnelAssignmentReferencesIt_ShouldReturn409()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLocationAdminContext(scenario));

        var tipo = await CrearTipoDeCentroAsync(client, scenario.TenantId, "TIPO-CEN-ASIG");
        var centro = await CrearCentroAsync(client, scenario.TenantId, "CEN-CON-ASIG", tipo.Id);

        // La asignación cuelga de un expediente real: sin él la FK a `personnel_files` la rechaza.
        using var rrhh = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));
        var expediente = await CreateBarePersonnelFileAsync(rrhh, scenario.TenantId, "Ana", "Prueba");

        await CrearAsignacionQueApuntaAlCentroAsync(scenario.TenantId, expediente.Id, centro.Id);

        var uso = await LeerUsoAsync(client, $"/api/v1/work-centers/{centro.Id}/usage");
        Assert.Equal(0, uso.GetProperty("positionSlotActiveReferences").GetInt32());
        Assert.Equal(1, uso.GetProperty("employmentAssignmentReferences").GetInt32());
        Assert.True(uso.GetProperty("hasActiveReferences").GetBoolean());

        var borrado = await client.DeleteWithIfMatchAsync($"/api/v1/work-centers/{centro.Id}", centro.ConcurrencyToken);
        await AssertProblemDetailsAsync(borrado, HttpStatusCode.Conflict, "WORK_CENTER_IN_USE_FOR_DELETE");
    }

    [Fact]
    public async Task FunctionalAreas_Delete_WhenNothingReferencesIt_ShouldRemoveIt()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId, scenario.TenantId, OrgStructureCatalogPermissionCodes.Admin));

        var area = await CrearAreaFuncionalAsync(client, scenario.TenantId, "AREA-BORRABLE");

        var uso = await LeerUsoAsync(client, $"/api/v1/organization-structure-catalogs/functional-areas/{area.Id}/usage");
        Assert.Equal(0, uso.GetProperty("orgUnitActiveReferences").GetInt32());
        Assert.Equal(0, uso.GetProperty("dashboardPreferenceReferences").GetInt32());

        var borrado = await client.DeleteWithIfMatchAsync(
            $"/api/v1/organization-structure-catalogs/functional-areas/{area.Id}", area.ConcurrencyToken);
        Assert.Equal(HttpStatusCode.OK, borrado.StatusCode);
    }

    /// <summary>
    /// ⚠️ <b>La otra referencia sin clave foránea.</b> La preferencia del tablero elige el área por su
    /// <c>code</c>. Borrarla dejaría el indicador de RRHH apuntando a un código inexistente, y la base de
    /// datos no lo habría impedido.
    /// </summary>
    [Fact]
    public async Task FunctionalAreas_Delete_WhenTheDashboardPreferenceReferencesIt_ShouldReturn409()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId,
                scenario.TenantId,
                OrgStructureCatalogPermissionCodes.Admin,
                CompanyPreferencePermissionCodes.Admin));

        var area = await CrearAreaFuncionalAsync(client, scenario.TenantId, "AREA-TABLERO");
        await ApuntarPreferenciaDeTableroAsync(scenario.TenantId, "AREA-TABLERO");

        var uso = await LeerUsoAsync(client, $"/api/v1/organization-structure-catalogs/functional-areas/{area.Id}/usage");
        Assert.Equal(0, uso.GetProperty("orgUnitActiveReferences").GetInt32());
        Assert.Equal(1, uso.GetProperty("dashboardPreferenceReferences").GetInt32());
        Assert.True(uso.GetProperty("hasActiveReferences").GetBoolean());

        var borrado = await client.DeleteWithIfMatchAsync(
            $"/api/v1/organization-structure-catalogs/functional-areas/{area.Id}", area.ConcurrencyToken);
        await AssertProblemDetailsAsync(borrado, HttpStatusCode.Conflict, "ORG_STRUCTURE_CATALOG_IN_USE_FOR_DELETE");
    }

    private async Task<WorkCenterTypeItem> CrearTipoDeCentroAsync(HttpClient client, Guid companyId, string code)
    {
        var response = await client.PostJsonAsync($"/api/v1/companies/{companyId}/work-center-types", new
        {
            code,
            name = $"Tipo {code}",
            requiresAddress = false,
            requiresGeo = false,
            allowsBiometric = false
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WorkCenterTypeItem>(JsonOptions))!;
    }

    private async Task<WorkCenterItem> CrearCentroAsync(HttpClient client, Guid companyId, string code, Guid tipoId)
    {
        var grupo = await GetDefaultLocationGroupAsync(client, companyId);
        var response = await client.PostJsonAsync($"/api/v1/companies/{companyId}/work-centers", new
        {
            code,
            name = $"Centro {code}",
            workCenterTypePublicId = tipoId,
            locationGroupPublicId = grupo.Id,
            address = (string?)null,
            geoLat = (decimal?)null,
            geoLong = (decimal?)null,
            phone = (string?)null,
            email = (string?)null,
            notes = (string?)null
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WorkCenterItem>(JsonOptions))!;
    }

    private async Task<FunctionalAreaItem> CrearAreaFuncionalAsync(HttpClient client, Guid companyId, string code)
    {
        var response = await client.PostJsonAsync(
            $"/api/v1/companies/{companyId}/organization-structure-catalogs/functional-areas",
            new { code, name = $"Area {code}", description = (string?)null, sortOrder = 1 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<FunctionalAreaItem>(JsonOptions))!;
    }

    /// <summary>Asignación mínima que apunta al centro. Se crea por el contexto porque no hay clave foránea que atar.</summary>
    private async Task CrearAsignacionQueApuntaAlCentroAsync(Guid tenantId, Guid personnelFileId, Guid workCenterPublicId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var internalId = await dbContext.Set<PersonnelFile>()
            .IgnoreQueryFilters()
            .Where(item => item.PublicId == personnelFileId)
            .Select(item => item.Id)
            .FirstAsync();

        var assignment = PersonnelFileEmploymentAssignment.Create(
            "INDEFINIDO",
            contractTypeCode: null,
            workdayCode: null,
            payrollTypeCode: null,
            positionSlotPublicId: null,
            orgUnitPublicId: null,
            workCenterPublicId: workCenterPublicId,
            costCenterPublicId: null,
            startDate: new DateOnly(2024, 1, 1),
            endDate: null,
            isPrimary: true,
            isActive: true,
            notes: null);
        assignment.BindToPersonnelFile(internalId);
        assignment.SetTenantId(tenantId);
        dbContext.Set<PersonnelFileEmploymentAssignment>().Add(assignment);
        await dbContext.SaveChangesAsync();
    }

    private async Task ApuntarPreferenciaDeTableroAsync(Guid tenantId, string functionalAreaCode)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var preference = await dbContext.CompanyPreferences
            .IgnoreQueryFilters()
            .FirstAsync(item => item.TenantId == tenantId);

        preference.SetDashboardSettings(functionalAreaCode, null);
        await dbContext.SaveChangesAsync();
    }

    private static async Task<JsonElement> LeerUsoAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var documento = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return documento.RootElement.Clone();
    }

    private sealed record FunctionalAreaItem(Guid Id, string Code, string Name, Guid ConcurrencyToken);
}
