using System.Net;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// H-23 — `occupiedEmployees` used to be a hand-written number that no writer maintained: creating or removing an
/// assignment never touched it, and `PATCH /status` outright INVENTED it (0 when going Vacant, 1 when going
/// Occupied, whoever was actually inside). It was not decorative either — the HR dashboard built its
/// `positionOccupancy` KPI on it, so two API calls were enough to make the board report an occupied position that
/// did not exist.
/// <para>
/// Now both the count and the Vacant/Occupied status are DERIVED from the active assignments — the same source the
/// capacity rule and the H-15 suspend guard already trusted. `Suspended` stays the one manual state, because that
/// one is a decision and not a fact.
/// </para>
/// </summary>
public sealed partial class ApiIntegrationTests
{
    [Fact]
    public async Task PositionSlots_Occupancy_ShouldFollowTheRealAssignments()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-H23-A", "Direccion H23 A", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-H23-A", "Perfil H23 A", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-H23-A", "Plaza H23 A", profile.Id, maxEmployees: 2);
        var employeeId = await SeedCompletedEmployeeAsync(scenario.TenantId, "Ocupa", "Plaza");

        await AssertSlotOccupancyAsync(client, slot.Id, expectedOccupied: 0, expectedStatus: "Vacant");

        var created = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/assigned-positions",
            EmploymentAssignmentBody(slot.Id));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // Nobody touched the slot: the count and the status follow the assignment on their own.
        await AssertSlotOccupancyAsync(client, slot.Id, expectedOccupied: 1, expectedStatus: "Occupied");

        using var createdDocument = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var assignmentId = createdDocument.RootElement.GetProperty("employmentAssignmentPublicId").GetGuid();
        var assignmentToken = createdDocument.RootElement.GetProperty("concurrencyToken").GetGuid();

        using var deleteRequest = new HttpRequestMessage(
            HttpMethod.Delete, $"/api/v1/personnel-files/{employeeId}/assigned-positions/{assignmentId}");
        deleteRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{assignmentToken}\"");
        var delete = await client.SendAsync(deleteRequest);
        Assert.True(
            delete.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent,
            $"Delete assignment failed: {(int)delete.StatusCode} {await delete.Content.ReadAsStringAsync()}");

        await AssertSlotOccupancyAsync(client, slot.Id, expectedOccupied: 0, expectedStatus: "Vacant");
    }

    /// <summary>The listing must tell the same story as the detail — it is the surface the org chart reads.</summary>
    [Fact]
    public async Task PositionSlots_List_ShouldExposeTheDerivedOccupancy()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-H23-B", "Direccion H23 B", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-H23-B", "Perfil H23 B", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-H23-B", "Plaza H23 B", profile.Id, maxEmployees: 3);
        var employeeId = await SeedCompletedEmployeeAsync(scenario.TenantId, "Lista", "Ocupada");

        var created = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/assigned-positions",
            EmploymentAssignmentBody(slot.Id));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var response = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/position-slots?page=1&pageSize=50");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var item = document.RootElement.GetProperty("items").EnumerateArray()
            .First(row => row.GetProperty("code").GetString() == "PS-H23-B");

        Assert.Equal(1, item.GetProperty("occupiedEmployees").GetInt32());
        Assert.Equal("Occupied", item.GetProperty("status").GetString());

        // And the derived status is filterable, which is what the org-chart screens do.
        var filtered = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/position-slots?page=1&pageSize=50&status=Occupied");
        filtered.EnsureSuccessStatusCode();
        using var filteredDocument = JsonDocument.Parse(await filtered.Content.ReadAsStringAsync());
        var codes = filteredDocument.RootElement.GetProperty("items").EnumerateArray()
            .Select(row => row.GetProperty("code").GetString())
            .ToArray();
        Assert.Contains("PS-H23-B", codes);
    }

    /// <summary>
    /// The KPI the finding turned from "decorative field" into "a board that can lie" — and which had NO test at
    /// all before this one.
    /// </summary>
    [Fact]
    public async Task Dashboard_PositionOccupancy_ShouldCountTheRealAssignments()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-H23-C", "Direccion H23 C", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-H23-C", "Perfil H23 C", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-H23-C", "Plaza H23 C", profile.Id, maxEmployees: 4);
        var employeeId = await SeedCompletedEmployeeAsync(scenario.TenantId, "Tablero", "Real");

        var before = await ReadDashboardOccupancyAsync(client, scenario.TenantId);
        Assert.Equal(0, before.Occupied);
        Assert.Equal(4, before.MaxPositions);
        Assert.Equal(4, before.Vacant);

        var created = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/assigned-positions",
            EmploymentAssignmentBody(slot.Id));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var after = await ReadDashboardOccupancyAsync(client, scenario.TenantId);
        Assert.Equal(1, after.Occupied);
        Assert.Equal(3, after.Vacant);
    }

    /// <summary>
    /// The `1` the status change used to fabricate: marking an empty slot Occupied made the dashboard report one
    /// occupant that did not exist. Now the status request cannot invent people.
    /// </summary>
    [Fact]
    public async Task Dashboard_PositionOccupancy_AfterStatusChange_ShouldNotInventOccupants()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-H23-D", "Direccion H23 D", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-H23-D", "Perfil H23 D", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-H23-D", "Plaza H23 D", profile.Id, maxEmployees: 1);

        var statusResponse = await client.PatchJsonAsync($"/api/v1/position-slots/{slot.Id}/status", new
        {
            status = "Occupied",
            concurrencyToken = slot.ConcurrencyToken
        });
        statusResponse.EnsureSuccessStatusCode();

        var occupancy = await ReadDashboardOccupancyAsync(client, scenario.TenantId);
        Assert.Equal(0, occupancy.Occupied);
        Assert.Equal(1, occupancy.Vacant);

        // And the slot itself reports what is true, not what was asked for.
        await AssertSlotOccupancyAsync(client, slot.Id, expectedOccupied: 0, expectedStatus: "Vacant");
    }

    /// <summary>The manual occupancy write is gone: there is nothing left to keep in sync by hand.</summary>
    [Fact]
    public async Task PositionSlots_OccupancyEndpoint_ShouldNoLongerExist()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-H23-E", "Direccion H23 E", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-H23-E", "Perfil H23 E", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-H23-E", "Plaza H23 E", profile.Id, maxEmployees: 1);

        var response = await client.PatchJsonAsync($"/api/v1/position-slots/{slot.Id}/occupancy", new
        {
            occupiedEmployees = 1,
            concurrencyToken = slot.ConcurrencyToken
        });

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"Expected the occupancy endpoint to be gone, got {(int)response.StatusCode}.");
    }

    private async Task AssertSlotOccupancyAsync(
        HttpClient client,
        Guid slotPublicId,
        int expectedOccupied,
        string expectedStatus)
    {
        var response = await client.GetAsync($"/api/v1/position-slots/{slotPublicId}");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedOccupied, document.RootElement.GetProperty("occupiedEmployees").GetInt32());
        Assert.Equal(expectedStatus, document.RootElement.GetProperty("status").GetString());
    }

    private async Task<(int MaxPositions, int Occupied, int Vacant)> ReadDashboardOccupancyAsync(
        HttpClient client,
        Guid companyId)
    {
        var response = await client.GetAsync($"/api/v1/companies/{companyId}/personnel-files/dashboard/overview");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var occupancy = document.RootElement.GetProperty("positionOccupancy");
        return (
            occupancy.GetProperty("maxPositions").GetInt32(),
            occupancy.GetProperty("occupied").GetInt32(),
            occupancy.GetProperty("vacant").GetInt32());
    }
}
