using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// H-15 — a position slot created by mistake was permanent: no `DELETE`, no retire route, and the finding's
/// author had to remove two test slots with raw SQL because the API offered no way.
///
/// The decisive fact behind the design is that **nothing has a foreign key to `position_slots` except its own
/// self-references**. Four tables point at a slot by `public_id` with no FK —
/// `personnel_file_employment_assignments`, `personnel_file_contract_histories`,
/// `personnel_file_authorization_substitutions` and `exit_interview_submissions` — so a raw delete does not
/// fail, it **silently orphans employment history**. The guard is therefore the feature; the verb is the easy
/// half.
///
/// The outbound FKs are the mirror image: all `RESTRICT`, so deleting a slot that another slot depends on used
/// to surface as a raw Postgres `500` instead of a contract error.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    [Fact]
    public async Task PositionSlots_Delete_WhenUnused_ShouldSucceed()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-H15-DEL", "Direccion H15", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-H15-DEL", "Perfil H15", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-H15-DEL", "Plaza de prueba", profile.Id, maxEmployees: 1);

        // 200 with the final snapshot, not 204: that is what the sibling DELETEs of this repo return
        // (`job-catalogs`, the job profile sub-resources). This test first assumed 204 — inventing a third
        // shape for the same verb is exactly what H-11 was about.
        var response = await DeletePositionSlotAsync(client, slot.Id, slot.ConcurrencyToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var deleted = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("PS-H15-DEL", deleted.RootElement.GetProperty("code").GetString());

        var afterGet = await client.GetAsync($"/api/v1/position-slots/{slot.Id}");
        Assert.Equal(HttpStatusCode.NotFound, afterGet.StatusCode);
    }

    // The case the guard exists for: the slot carries employment history that has no FK protecting it.
    [Fact]
    public async Task PositionSlots_Delete_WithActiveAssignment_ShouldReturn409()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-H15-ASG", "Direccion H15 ASG", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-H15-ASG", "Perfil H15 ASG", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-H15-ASG", "Plaza ocupada", profile.Id, maxEmployees: 2);
        var employeeId = await SeedCompletedEmployeeAsync(scenario.TenantId, "Ocupante", "H15");

        var assignment = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/assigned-positions",
            EmploymentAssignmentBody(slot.Id));
        assignment.EnsureSuccessStatusCode();

        var current = await GetPositionSlotAsync(client, slot.Id);
        var response = await DeletePositionSlotAsync(client, slot.Id, current.ConcurrencyToken);

        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "POSITION_SLOT_IN_USE");
    }

    // Without the guard this is a raw Postgres RESTRICT violation surfacing as 500, not a 409.
    [Fact]
    public async Task PositionSlots_Delete_WhenParentOfAnotherSlot_ShouldReturn409()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-H15-PAR", "Direccion H15 PAR", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-H15-PAR", "Perfil H15 PAR", orgUnit.Id);
        var parent = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-H15-PADRE", "Plaza padre", profile.Id, maxEmployees: 1);

        var childResponse = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/position-slots", new
        {
            code = "PS-H15-HIJA",
            title = "Plaza hija",
            jobProfilePublicId = profile.Id,
            workCenterPublicId = (Guid?)null,
            directDependencyPositionSlotPublicId = parent.Id,
            functionalDependencyPositionSlotPublicId = (Guid?)null,
            status = "Vacant",
            maxEmployees = 1,
            occupiedEmployees = 0,
            effectiveFromUtc = DateTime.UtcNow.Date,
            effectiveToUtc = (DateTime?)null,
            notes = (string?)null
        });
        childResponse.EnsureSuccessStatusCode();

        var response = await DeletePositionSlotAsync(client, parent.Id, parent.ConcurrencyToken);

        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "POSITION_SLOT_IN_USE");
    }

    [Fact]
    public async Task PositionSlots_Delete_WithoutIfMatch_ShouldReturn400()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-H15-IFM", "Direccion H15 IFM", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-H15-IFM", "Perfil H15 IFM", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-H15-IFM", "Plaza sin token", profile.Id, maxEmployees: 1);

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/position-slots/{slot.Id}");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// H-15 — suspending a slot that still has people in it was allowed, and left the aggregate incoherent:
    /// `IsActive` went false while its assignments stayed active and `occupiedEmployees` kept its old value.
    /// `EnsureStatusConsistency` only normalised Vacant/Occupied, so nothing objected.
    /// </summary>
    [Fact]
    public async Task PositionSlots_Suspend_WithOccupants_ShouldReturn422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-H15-SUS", "Direccion H15 SUS", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-H15-SUS", "Perfil H15 SUS", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-H15-SUS", "Plaza con gente", profile.Id, maxEmployees: 2);
        var employeeId = await SeedCompletedEmployeeAsync(scenario.TenantId, "Adentro", "H15");

        var assignment = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/assigned-positions",
            EmploymentAssignmentBody(slot.Id));
        assignment.EnsureSuccessStatusCode();

        var current = await GetPositionSlotAsync(client, slot.Id);
        var response = await PatchPositionSlotStatusAsync(client, slot.Id, current.ConcurrencyToken, "Suspended");

        await AssertProblemDetailsAsync(
            response, HttpStatusCode.UnprocessableEntity, "POSITION_SLOT_SUSPEND_WITH_OCCUPANTS");
    }

    [Fact]
    public async Task PositionSlots_Search_WithIsActiveFalse_ShouldReturnOnlySuspended()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-H15-FIL", "Direccion H15 FIL", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-H15-FIL", "Perfil H15 FIL", orgUnit.Id);
        _ = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-H15-VIVA", "Plaza viva", profile.Id, maxEmployees: 1);
        var retired = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-H15-RETIRADA", "Plaza retirada", profile.Id, maxEmployees: 1);

        (await PatchPositionSlotStatusAsync(client, retired.Id, retired.ConcurrencyToken, "Suspended"))
            .EnsureSuccessStatusCode();

        using var document = await GetJsonAsync(
            client, $"/api/v1/companies/{scenario.TenantId}/position-slots?isActive=false&page=1&pageSize=50");

        var codes = document.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("code").GetString())
            .ToArray();

        Assert.Equal(new[] { "PS-H15-RETIRADA" }, codes);
    }

    // The filter is additive: omitting it must keep returning everything, or the frontend contract moved.
    [Fact]
    public async Task PositionSlots_Search_WithoutFilter_ShouldStillReturnAll()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-H15-ALL", "Direccion H15 ALL", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-H15-ALL", "Perfil H15 ALL", orgUnit.Id);
        _ = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-H15-A", "Plaza A", profile.Id, maxEmployees: 1);
        var retired = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-H15-B", "Plaza B", profile.Id, maxEmployees: 1);

        (await PatchPositionSlotStatusAsync(client, retired.Id, retired.ConcurrencyToken, "Suspended"))
            .EnsureSuccessStatusCode();

        using var document = await GetJsonAsync(
            client, $"/api/v1/companies/{scenario.TenantId}/position-slots?page=1&pageSize=50");

        Assert.Equal(2, document.RootElement.GetProperty("items").GetArrayLength());
    }

    private static async Task<HttpResponseMessage> DeletePositionSlotAsync(
        HttpClient client,
        Guid slotPublicId,
        Guid concurrencyToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/position-slots/{slotPublicId}");
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{concurrencyToken}\"");

        return await client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> PatchPositionSlotStatusAsync(
        HttpClient client,
        Guid slotPublicId,
        Guid concurrencyToken,
        string status)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/position-slots/{slotPublicId}/status")
        {
            Content = JsonContent.Create(new { status }),
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{concurrencyToken}\"");

        return client.SendAsync(request);
    }

    private async Task<PositionSlotItem> GetPositionSlotAsync(HttpClient client, Guid slotPublicId)
    {
        var response = await client.GetAsync($"/api/v1/position-slots/{slotPublicId}");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PositionSlotItem>(JsonOptions))!;
    }
}
