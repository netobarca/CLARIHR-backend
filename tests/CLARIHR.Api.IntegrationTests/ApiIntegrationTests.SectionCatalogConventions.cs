using System.Net;
using System.Text.Json;
using CLARIHR.Domain.JobProfiles;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// H-11 — the catalogs of §4 that a user perceives as equivalent did not agree on four axes: the name of the
/// ordering field, whether it is unique, how rich the body is, and whether a real delete exists. Two of those
/// were closed because their absence is a functional gap rather than cosmetics, and one because harmonising it
/// upward cannot break anything:
///
/// <list type="bullet">
/// <item><b>`job-catalogs` gained `sortOrder` and `description`.</b> Without them a competency picker could
/// only be alphabetical — you could not put `LIDERAZGO` first for a directive profile — and a competency
/// dictionary had nowhere to say what a competency means.</item>
/// <item><b>`name` harmonised to 150</b> where it was 120. Accepting more can never reject what fit before.</item>
/// </list>
///
/// What was deliberately NOT normalised: the ordering field names and the verb sets. Renaming
/// <c>levelOrder</c> to <c>sortOrder</c> breaks the frontend contract and enables nothing, and the pyramid's
/// UNIQUE ordering is an invariant worth keeping — an occupational pyramid is a strict ranking, so two levels
/// sharing a rank is meaningless. What actually hurt there was reordering, and that is what the bulk endpoint
/// solves.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    /// <summary>Exactly 150 characters — the harmonised ceiling, so it must be accepted, not truncated.</summary>
    private const string LongCatalogName =
        "Nombre de catalogo deliberadamente largo para comprobar que el limite armonizado de ciento cincuenta " +
        "caracteres se acepta igual en toda la seccion H11";

    [Fact]
    public async Task JobCatalogs_ShouldAcceptSortOrderAndDescription()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateCompetencyFrameworkAdminContext(scenario));

        var response = await client.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/job-catalogs/{JobCatalogCategory.Competency}",
            new
            {
                code = "COMP-H11",
                name = "Liderazgo",
                description = "Capacidad de dirigir equipos hacia un objetivo comun.",
                sortOrder = 5
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var created = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "Capacidad de dirigir equipos hacia un objetivo comun.",
            created.RootElement.GetProperty("description").GetString());
        Assert.Equal(5, created.RootElement.GetProperty("sortOrder").GetInt32());
    }

    /// <summary>
    /// The point of the field: the list must obey it, otherwise it is decoration.
    /// <para>
    /// Both the code AND the name are deliberately in the OPPOSITE alphabetical order to the intended display
    /// order. The first draft of this test used names "Primero"/"Segundo", which happen to sort the same way as
    /// the desired result — the previous ordering was by name, so it would have passed without the fix and
    /// proved nothing (the H-33 failure mode). `Zeta` before `Alfa` can only come out right if `sortOrder`
    /// really governs.
    /// </para>
    /// </summary>
    [Fact]
    public async Task JobCatalogs_List_ShouldOrderBySortOrderBeforeCode()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateCompetencyFrameworkAdminContext(scenario));

        await CreateJobCatalogItemWithOrderAsync(client, scenario.TenantId, "ZZ-PRIMERO", "Zeta", sortOrder: 10);
        await CreateJobCatalogItemWithOrderAsync(client, scenario.TenantId, "AA-SEGUNDO", "Alfa", sortOrder: 20);

        using var document = await GetJsonAsync(
            client,
            $"/api/v1/companies/{scenario.TenantId}/job-catalogs/{JobCatalogCategory.CompetencyType}?page=1&pageSize=50");

        var codes = document.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("code").GetString())
            .ToArray();

        Assert.Equal(new[] { "ZZ-PRIMERO", "AA-SEGUNDO" }, codes);
    }

    [Fact]
    public async Task JobCatalogs_ShouldAcceptNameOf150Characters()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateCompetencyFrameworkAdminContext(scenario));

        Assert.Equal(150, LongCatalogName.Length);

        var response = await client.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/job-catalogs/{JobCatalogCategory.Training}",
            new { code = "TRN-H11", name = LongCatalogName });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task OccupationalPyramidLevels_ShouldAcceptNameOf150Characters()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateCompetencyFrameworkAdminContext(scenario));

        var response = await client.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/occupational-pyramid-levels",
            new { code = "OPL-H11", name = LongCatalogName, levelOrder = 910, description = (string?)null });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task CreateJobCatalogItemWithOrderAsync(
        HttpClient client,
        Guid companyId,
        string code,
        string name,
        int sortOrder)
    {
        var response = await client.PostJsonAsync(
            $"/api/v1/companies/{companyId}/job-catalogs/{JobCatalogCategory.CompetencyType}",
            new { code, name, description = (string?)null, sortOrder });
        response.EnsureSuccessStatusCode();
    }
}
