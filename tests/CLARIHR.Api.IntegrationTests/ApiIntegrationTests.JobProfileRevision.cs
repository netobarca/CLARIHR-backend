using System.Net.Http.Json;
using CLARIHR.Domain.JobProfiles;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// H-09 — <c>version</c> counted WRITES, not revisions. After loading the nine child collections the 33
/// profiles of the run sat between 30 and 39, correlated with how many child rows each had: every
/// <c>Add*</c>/<c>Remove*</c>, the core <c>PUT</c> and every competency-matrix write bumped it. A frontend
/// labelling that field "versión del descriptor" showed a number that meant nothing a user would recognise.
///
/// It now counts **approved revisions**: only <see cref="CLARIHR.Domain.JobProfiles.JobProfile.Publish"/>
/// moves it. A draft that was never approved reports <c>0</c>; <c>2</c> means published twice.
///
/// Reopening does not count — reopening is working *towards* the next revision, not producing one — and
/// neither does archiving, which changes no content.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    [Fact]
    public async Task JobProfiles_Version_ShouldBeZero_OnCreate()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-H09-NEW", "Perfil Nuevo");

        Assert.Equal(0, (await GetJobProfileAsync(client, profile.Id)).Version);
    }

    [Fact]
    public async Task JobProfiles_Version_ShouldNotMove_WhenCoreIsUpdated()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-H09-UPD", "Direccion H09", "Direccion");
        var category = await EnsureDefaultPositionCategoryAsync(client, scenario.TenantId);
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-H09-UPD", "Perfil Editable");
        var before = await GetJobProfileAsync(client, profile.Id);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/job-profiles/{profile.Id}")
        {
            Content = JsonContent.Create(
                BuildJobProfileUpdatePayload("JP-H09-UPD", "Perfil Editado", orgUnit.Id, category.Id))
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{before.ConcurrencyToken}\"");
        (await client.SendAsync(request)).EnsureSuccessStatusCode();

        Assert.Equal(before.Version, (await GetJobProfileAsync(client, profile.Id)).Version);
    }

    // The finding's actual shape: `P-DG` reached 39 because of how many dependent positions it had.
    [Fact]
    public async Task JobProfiles_Version_ShouldNotMove_WhenChildCollectionChanges()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-H09-CHILD", "Perfil Con Hijos");
        var before = await GetJobProfileAsync(client, profile.Id);

        for (var index = 1; index <= 3; index++)
        {
            var response = await client.PostJsonAsync($"/api/v1/job-profiles/{profile.Id}/functions", new
            {
                functionType = "General",
                description = $"Funcion {index}",
                sortOrder = index
            });
            response.EnsureSuccessStatusCode();
        }

        Assert.Equal(before.Version, (await GetJobProfileAsync(client, profile.Id)).Version);
    }

    // Asserts the EXACT value on purpose. `after > before` would have been green before the fix — the number
    // moved then too, just for the wrong reasons — and would have proven nothing.
    [Fact]
    public async Task JobProfiles_Version_ShouldBeOne_AfterFirstPublish()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-H09-PUB", "Direccion Publica", "Direccion");
        var published = await CreatePublishedJobProfileAsync(client, scenario.TenantId, orgUnit.Id, "JP-H09-PUB");

        Assert.Equal(1, published.Version);
    }

    [Fact]
    public async Task JobProfiles_Version_ShouldNotMove_OnReopen()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-H09-REO", "Direccion Reabrir", "Direccion");
        var published = await CreatePublishedJobProfileAsync(client, scenario.TenantId, orgUnit.Id, "JP-H09-REO");

        var reopened = await ReopenJobProfileAsync(client, published.Id, "Correccion de responsabilidades.");

        Assert.Equal(published.Version, reopened.Version);
    }

    [Fact]
    public async Task JobProfiles_Version_ShouldNotMove_OnArchive()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-H09-ARC", "Direccion Archivar", "Direccion");
        var published = await CreatePublishedJobProfileAsync(client, scenario.TenantId, orgUnit.Id, "JP-H09-ARC");

        using var request = new HttpRequestMessage(
            HttpMethod.Patch, $"/api/v1/job-profiles/{published.Id}/archival");
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{published.ConcurrencyToken}\"");
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var archived = await response.Content.ReadFromJsonAsync<JobProfileItem>(JsonOptions);
        Assert.NotNull(archived);
        Assert.Equal(published.Version, archived!.Version);
    }

    // The whole point of the number: a descriptor approved twice reads 2, whatever happened in between.
    [Fact]
    public async Task JobProfiles_Version_ShouldBeTwo_AfterReopenAndRepublish()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-H09-CYC", "Direccion Ciclo", "Direccion");
        var published = await CreatePublishedJobProfileAsync(client, scenario.TenantId, orgUnit.Id, "JP-H09-CYC");
        Assert.Equal(1, published.Version);

        var reopened = await ReopenJobProfileAsync(client, published.Id, "Ajuste de funciones.");

        var functionResponse = await client.PostJsonAsync($"/api/v1/job-profiles/{published.Id}/functions", new
        {
            functionType = "Specific",
            description = "Funcion agregada en la revision 2",
            sortOrder = 9
        });
        functionResponse.EnsureSuccessStatusCode();
        Assert.Equal(1, (await GetJobProfileAsync(client, published.Id)).Version);

        using var republishRequest = new HttpRequestMessage(
            HttpMethod.Patch, $"/api/v1/job-profiles/{published.Id}/publication");
        republishRequest.Headers.TryAddWithoutValidation(
            "If-Match", $"\"{(await GetJobProfileAsync(client, published.Id)).ConcurrencyToken}\"");
        var republishResponse = await client.SendAsync(republishRequest);
        republishResponse.EnsureSuccessStatusCode();

        var republished = await republishResponse.Content.ReadFromJsonAsync<JobProfileItem>(JsonOptions);
        Assert.NotNull(republished);
        Assert.Equal(2, republished!.Version);
        Assert.NotEqual(reopened.ConcurrencyToken, republished.ConcurrencyToken);
    }

    // The competency matrix is an operational overlay on an approved descriptor, not a revision of it. It
    // reached the aggregate only to bump this counter; now it must leave it alone.
    [Fact]
    public async Task JobProfiles_Version_ShouldNotMove_WhenMatrixChanges()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateCompetencyFrameworkAdminContext(scenario));

        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-H09-MTX", "Perfil Matriz");
        var competency = await CreateJobCatalogItemAsync(client, scenario.TenantId, JobCatalogCategory.Competency, "COMP-H09", "Liderazgo");
        var competencyType = await CreateJobCatalogItemAsync(client, scenario.TenantId, JobCatalogCategory.CompetencyType, "CTYPE-H09", "Gerencial");
        var behaviorLevel = await CreateJobCatalogItemAsync(client, scenario.TenantId, JobCatalogCategory.BehaviorLevel, "BLEVEL-H09", "Estrategico");
        var level = await CreatePyramidLevelAsync(client, scenario.TenantId, "OPL-H09-1");
        var conduct = await CreateCompetencyConductAsync(
            client, scenario.TenantId, competency.Id, competencyType.Id, behaviorLevel.Id, "Conducta observable.", 1);

        var before = await GetJobProfileAsync(client, profile.Id);

        _ = await AddMatrixItemAsync(
            client, profile.Id, level.Id, new[] { conduct.Id }, "Evidencia esperada.", 1);

        Assert.Equal(before.Version, (await GetJobProfileAsync(client, profile.Id)).Version);
    }

    /// <summary>
    /// H-09 TRAP GUARD. In the domain the core <c>PUT</c>'s <c>Version++</c> and its
    /// <c>RefreshConcurrencyToken()</c> lived inside the SAME <c>if (bumpVersion)</c> block. Dropping the
    /// version bump by deleting that block would silently stop the <c>PUT</c> from rotating the token, and
    /// optimistic concurrency on the profile core would break with every test still green.
    /// </summary>
    [Fact]
    public async Task JobProfiles_Update_ShouldRotateConcurrencyToken_WithoutMovingVersion()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-H09-TOK", "Direccion Token", "Direccion");
        var category = await EnsureDefaultPositionCategoryAsync(client, scenario.TenantId);
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-H09-TOK", "Perfil Token");
        var before = await GetJobProfileAsync(client, profile.Id);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/job-profiles/{profile.Id}")
        {
            Content = JsonContent.Create(
                BuildJobProfileUpdatePayload("JP-H09-TOK", "Perfil Token Editado", orgUnit.Id, category.Id))
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{before.ConcurrencyToken}\"");
        (await client.SendAsync(request)).EnsureSuccessStatusCode();

        var after = await GetJobProfileAsync(client, profile.Id);
        Assert.NotEqual(before.ConcurrencyToken, after.ConcurrencyToken);
        Assert.Equal(before.Version, after.Version);

        // And the stale token must now be refused, which is the behaviour the rotation exists for.
        using var staleRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/job-profiles/{profile.Id}")
        {
            Content = JsonContent.Create(
                BuildJobProfileUpdatePayload("JP-H09-TOK", "Segundo Intento", orgUnit.Id, category.Id))
        };
        staleRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{before.ConcurrencyToken}\"");

        await AssertProblemDetailsAsync(
            await client.SendAsync(staleRequest), System.Net.HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
    }
}
