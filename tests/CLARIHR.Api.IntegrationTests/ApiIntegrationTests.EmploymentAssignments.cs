using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CLARIHR.Application.Features.CompetencyFramework.Common;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Application.Features.OrgUnits.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using CLARIHR.Application.Features.PositionSlots.Common;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Integration coverage for the multi-position ("múltiples plazas") rules on the
/// assigned-positions endpoint: first-plaza-defaults-primary + auto-degrade (RF-002),
/// capacity-by-vigencia (RF-005) and same-slot dedup (RF-007). A completed employee is seeded
/// directly (no finalize flow needed); the position slot is built through the existing API helpers.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static TestUserContext CreateMultiPlazaContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            PersonnelFilePermissionCodes.Admin,
            PositionSlotPermissionCodes.Admin,
            OrgUnitPermissionCodes.Admin,
            JobProfilePermissionCodes.Admin,
            PositionDescriptionCatalogPermissionCodes.Admin,
            CompetencyFrameworkPermissionCodes.Admin);

    private async Task<Guid> SeedCompletedEmployeeAsync(Guid tenantId, string firstName, string lastName)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var personnelFile = PersonnelFile.Create(
            PersonnelFileRecordType.Employee,
            firstName,
            lastName,
            new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            maritalStatus: null,
            profession: null,
            nationality: "SV",
            personalEmail: null,
            institutionalEmail: null,
            personalPhone: null,
            institutionalPhone: null,
            birthCountry: null,
            birthDepartment: null,
            birthMunicipality: null,
            photoFilePublicId: null,
            orgUnitPublicId: null);
        personnelFile.SetTenantId(tenantId);
        personnelFile.CompleteWithoutLinkedUser();

        dbContext.Set<PersonnelFile>().Add(personnelFile);
        await dbContext.SaveChangesAsync();
        return personnelFile.PublicId;
    }

    private static object EmploymentAssignmentBody(
        Guid positionSlotId,
        bool isPrimary = true,
        bool isActive = true,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string assignmentTypeCode = "INDEFINIDO") =>
        new
        {
            assignmentTypeCode,
            positionSlotPublicId = positionSlotId,
            orgUnitPublicId = (Guid?)null,
            workCenterPublicId = (Guid?)null,
            costCenterPublicId = (Guid?)null,
            startDate = startDate ?? DateTime.UtcNow.Date,
            endDate,
            isPrimary,
            isActive,
            notes = (string?)null
        };

    [Fact]
    public async Task EmploymentAssignment_AddToVacantSlot_DefaultsToPrimary()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-MP-A", "Direccion MP A", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-MP-A", "Perfil MP A", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-MP-A", "Plaza MP A", profile.Id, maxEmployees: 2);
        var employeeId = await SeedCompletedEmployeeAsync(scenario.TenantId, "Ana", "Principal");

        var response = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/assigned-positions",
            EmploymentAssignmentBody(slot.Id, isPrimary: false));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        // First active plaza defaults to primary even though the request sent isPrimary=false.
        Assert.True(document.RootElement.GetProperty("isPrimary").GetBoolean());
    }

    [Fact]
    public async Task EmploymentAssignment_ListAndCreate_ExposePositionSlotCodeAndTitle()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-MP-LBL", "Direccion MP LBL", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-MP-LBL", "Perfil MP LBL", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-MP-LBL", "Plaza Etiqueta", profile.Id, maxEmployees: 2);
        var employeeId = await SeedCompletedEmployeeAsync(scenario.TenantId, "Seis", "Empleado");

        // The create response already carries the human-readable slot label (resolved server-side from the slot).
        var created = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/assigned-positions",
            EmploymentAssignmentBody(slot.Id));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using (var createdDoc = JsonDocument.Parse(await created.Content.ReadAsStringAsync()))
        {
            Assert.Equal("PS-MP-LBL", createdDoc.RootElement.GetProperty("positionSlotCode").GetString());
            Assert.Equal("Plaza Etiqueta", createdDoc.RootElement.GetProperty("positionSlotTitle").GetString());
        }

        // The list endpoint (the substitutions plaza combobox source) exposes the same label so the
        // frontend never has to fall back to the raw UUID — this is the Hallazgo 2 fix.
        var list = await client.GetAsync($"/api/v1/personnel-files/{employeeId}/assigned-positions");
        list.EnsureSuccessStatusCode();
        using var listDoc = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var item = listDoc.RootElement.EnumerateArray().Single(
            entry => entry.GetProperty("positionSlotPublicId").GetGuid() == slot.Id);
        Assert.Equal("PS-MP-LBL", item.GetProperty("positionSlotCode").GetString());
        Assert.Equal("Plaza Etiqueta", item.GetProperty("positionSlotTitle").GetString());
    }

    [Fact]
    public async Task EmploymentAssignment_WhenSlotCapacityFull_IsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-MP-B", "Direccion MP B", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-MP-B", "Perfil MP B", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-MP-B", "Plaza MP B", profile.Id, maxEmployees: 1);
        var employeeOne = await SeedCompletedEmployeeAsync(scenario.TenantId, "Uno", "Empleado");
        var employeeTwo = await SeedCompletedEmployeeAsync(scenario.TenantId, "Dos", "Empleado");

        var first = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{employeeOne}/assigned-positions",
            EmploymentAssignmentBody(slot.Id));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{employeeTwo}/assigned-positions",
            EmploymentAssignmentBody(slot.Id));

        await AssertProblemDetailsAsync(second, HttpStatusCode.UnprocessableEntity, "EMPLOYMENT_ASSIGNMENT_CAPACITY_EXCEEDED");
    }

    [Fact]
    public async Task EmploymentAssignment_SameSlotOverlappingPeriod_IsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-MP-C", "Direccion MP C", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-MP-C", "Perfil MP C", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-MP-C", "Plaza MP C", profile.Id, maxEmployees: 2);
        var employeeId = await SeedCompletedEmployeeAsync(scenario.TenantId, "Tres", "Empleado");

        var first = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/assigned-positions",
            EmploymentAssignmentBody(slot.Id));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var duplicate = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/assigned-positions",
            EmploymentAssignmentBody(slot.Id));

        await AssertProblemDetailsAsync(duplicate, HttpStatusCode.Conflict, "EMPLOYMENT_ASSIGNMENT_OVERLAPPING_DATES");
    }

    [Fact]
    public async Task EmploymentAssignment_NewPrimary_DemotesPreviousPrimary()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-MP-D", "Direccion MP D", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-MP-D", "Perfil MP D", orgUnit.Id);
        var slotA = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-MP-D1", "Plaza MP D1", profile.Id, maxEmployees: 2);
        var slotB = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-MP-D2", "Plaza MP D2", profile.Id, maxEmployees: 2);
        var employeeId = await SeedCompletedEmployeeAsync(scenario.TenantId, "Cuatro", "Empleado");

        var primaryA = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/assigned-positions",
            EmploymentAssignmentBody(slotA.Id, isPrimary: true));
        Assert.Equal(HttpStatusCode.Created, primaryA.StatusCode);

        var primaryB = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/assigned-positions",
            EmploymentAssignmentBody(slotB.Id, isPrimary: true));
        Assert.Equal(HttpStatusCode.Created, primaryB.StatusCode);

        var list = await client.GetAsync($"/api/v1/personnel-files/{employeeId}/assigned-positions");
        list.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var items = document.RootElement.EnumerateArray().ToArray();

        var slotAItem = items.Single(item => item.GetProperty("positionSlotPublicId").GetGuid() == slotA.Id);
        var slotBItem = items.Single(item => item.GetProperty("positionSlotPublicId").GetGuid() == slotB.Id);

        Assert.False(slotAItem.GetProperty("isPrimary").GetBoolean());
        Assert.True(slotBItem.GetProperty("isPrimary").GetBoolean());
    }

    [Fact]
    public async Task EmploymentAssignment_PatchPromoteSecondaryToPrimary_DemotesPreviousPrimary()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-MP-E", "Direccion MP E", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-MP-E", "Perfil MP E", orgUnit.Id);
        var slotA = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-MP-E1", "Plaza MP E1", profile.Id, maxEmployees: 2);
        var slotB = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-MP-E2", "Plaza MP E2", profile.Id, maxEmployees: 2);
        var employeeId = await SeedCompletedEmployeeAsync(scenario.TenantId, "Cinco", "Empleado");

        _ = await PostAssignmentAsync(client, employeeId, EmploymentAssignmentBody(slotA.Id, isPrimary: true));
        var secondary = await PostAssignmentAsync(client, employeeId, EmploymentAssignmentBody(slotB.Id, isPrimary: false));

        var patch = await PatchAssignmentAsync(
            client,
            employeeId,
            secondary.Id,
            secondary.Token,
            new[] { new { op = "replace", path = "/isPrimary", value = true } });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var list = await client.GetAsync($"/api/v1/personnel-files/{employeeId}/assigned-positions");
        list.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var items = document.RootElement.EnumerateArray().ToArray();
        var slotAItem = items.Single(item => item.GetProperty("positionSlotPublicId").GetGuid() == slotA.Id);
        var slotBItem = items.Single(item => item.GetProperty("positionSlotPublicId").GetGuid() == slotB.Id);

        Assert.False(slotAItem.GetProperty("isPrimary").GetBoolean());
        Assert.True(slotBItem.GetProperty("isPrimary").GetBoolean());
    }

    [Fact]
    public async Task EmploymentAssignment_PutDemoteOnlyPrimary_IsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-MP-F", "Direccion MP F", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-MP-F", "Perfil MP F", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-MP-F", "Plaza MP F", profile.Id, maxEmployees: 2);
        var employeeId = await SeedCompletedEmployeeAsync(scenario.TenantId, "Seis", "Empleado");

        var primary = await PostAssignmentAsync(client, employeeId, EmploymentAssignmentBody(slot.Id, isPrimary: true));

        var put = await PutAssignmentAsync(
            client,
            employeeId,
            primary.Id,
            primary.Token,
            EmploymentAssignmentBody(slot.Id, isPrimary: false));

        await AssertProblemDetailsAsync(put, HttpStatusCode.UnprocessableEntity, "EMPLOYMENT_ASSIGNMENT_PRIMARY_REQUIRED");
    }

    [Fact]
    public async Task EmploymentAssignment_PatchDeactivatePrimaryWithoutReplacement_IsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-MP-G", "Direccion MP G", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-MP-G", "Perfil MP G", orgUnit.Id);
        var slotA = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-MP-G1", "Plaza MP G1", profile.Id, maxEmployees: 2);
        var slotB = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-MP-G2", "Plaza MP G2", profile.Id, maxEmployees: 2);
        var employeeId = await SeedCompletedEmployeeAsync(scenario.TenantId, "Siete", "Empleado");

        var primary = await PostAssignmentAsync(client, employeeId, EmploymentAssignmentBody(slotA.Id, isPrimary: true));
        _ = await PostAssignmentAsync(client, employeeId, EmploymentAssignmentBody(slotB.Id, isPrimary: false));

        var patch = await PatchAssignmentAsync(
            client,
            employeeId,
            primary.Id,
            primary.Token,
            new[] { new { op = "replace", path = "/isActive", value = false } });

        await AssertProblemDetailsAsync(patch, HttpStatusCode.UnprocessableEntity, "EMPLOYMENT_ASSIGNMENT_PRIMARY_REQUIRED");
    }

    [Fact]
    public async Task EmploymentAssignment_InvalidAssignmentTypeCode_IsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-MP-H", "Direccion MP H", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-MP-H", "Perfil MP H", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-MP-H", "Plaza MP H", profile.Id, maxEmployees: 2);
        var employeeId = await SeedCompletedEmployeeAsync(scenario.TenantId, "Ocho", "Empleado");

        var response = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/assigned-positions",
            EmploymentAssignmentBody(slot.Id, assignmentTypeCode: "NOT_A_REAL_TYPE"));

        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "EMPLOYMENT_ASSIGNMENT_TYPE_CODE_INVALID");
    }

    [Fact]
    public async Task EmploymentAssignment_DeletePrimaryWithActiveSecondary_IsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-MP-I", "Direccion MP I", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-MP-I", "Perfil MP I", orgUnit.Id);
        var slotA = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-MP-I1", "Plaza MP I1", profile.Id, maxEmployees: 2);
        var slotB = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-MP-I2", "Plaza MP I2", profile.Id, maxEmployees: 2);
        var employeeId = await SeedCompletedEmployeeAsync(scenario.TenantId, "Nueve", "Empleado");

        var primary = await PostAssignmentAsync(client, employeeId, EmploymentAssignmentBody(slotA.Id, isPrimary: true));
        _ = await PostAssignmentAsync(client, employeeId, EmploymentAssignmentBody(slotB.Id, isPrimary: false));

        var delete = await DeleteAssignmentAsync(client, employeeId, primary.Id, primary.Token);

        await AssertProblemDetailsAsync(delete, HttpStatusCode.UnprocessableEntity, "EMPLOYMENT_ASSIGNMENT_PRIMARY_REQUIRED");
    }

    [Fact]
    public async Task EmploymentAssignment_DeleteSecondary_Succeeds()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-MP-J", "Direccion MP J", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-MP-J", "Perfil MP J", orgUnit.Id);
        var slotA = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-MP-J1", "Plaza MP J1", profile.Id, maxEmployees: 2);
        var slotB = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-MP-J2", "Plaza MP J2", profile.Id, maxEmployees: 2);
        var employeeId = await SeedCompletedEmployeeAsync(scenario.TenantId, "Diez", "Empleado");

        _ = await PostAssignmentAsync(client, employeeId, EmploymentAssignmentBody(slotA.Id, isPrimary: true));
        var secondary = await PostAssignmentAsync(client, employeeId, EmploymentAssignmentBody(slotB.Id, isPrimary: false));

        var delete = await DeleteAssignmentAsync(client, employeeId, secondary.Id, secondary.Token);

        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
    }

    // Same shape as EmploymentAssignmentBody but with the D-26 rest-day field. Kept separate so the
    // existing tests keep posting bodies WITHOUT the key (backwards-compatibility coverage for free).
    private static object EmploymentAssignmentBodyWithRestDay(
        Guid positionSlotId,
        int? restDayOfWeek,
        bool isPrimary = true,
        bool isActive = true) =>
        new
        {
            assignmentTypeCode = "INDEFINIDO",
            positionSlotPublicId = positionSlotId,
            orgUnitPublicId = (Guid?)null,
            workCenterPublicId = (Guid?)null,
            costCenterPublicId = (Guid?)null,
            startDate = DateTime.UtcNow.Date,
            endDate = (DateTime?)null,
            isPrimary,
            isActive,
            notes = (string?)null,
            restDayOfWeek
        };

    [Fact]
    public async Task EmploymentAssignment_RestDayOfWeek_PutReflectsAndGetReturnsIt()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-MP-RD1", "Direccion MP RD1", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-MP-RD1", "Perfil MP RD1", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-MP-RD1", "Plaza MP RD1", profile.Id, maxEmployees: 2);
        var employeeId = await SeedCompletedEmployeeAsync(scenario.TenantId, "Once", "Empleado");

        // Create WITHOUT the field: the assignment stays null (additive, backwards compatible).
        var created = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/assigned-positions",
            EmploymentAssignmentBody(slot.Id));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Guid assignmentId;
        Guid token;
        using (var createdDoc = JsonDocument.Parse(await created.Content.ReadAsStringAsync()))
        {
            assignmentId = createdDoc.RootElement.GetProperty("employmentAssignmentPublicId").GetGuid();
            token = createdDoc.RootElement.GetProperty("concurrencyToken").GetGuid();
            Assert.True(
                !createdDoc.RootElement.TryGetProperty("restDayOfWeek", out var restDay)
                || restDay.ValueKind == JsonValueKind.Null);
        }

        // PUT with restDayOfWeek = 3 (Wednesday) — the response reflects the new value.
        var put = await PutAssignmentAsync(
            client,
            employeeId,
            assignmentId,
            token,
            EmploymentAssignmentBodyWithRestDay(slot.Id, restDayOfWeek: 3));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        using (var putDoc = JsonDocument.Parse(await put.Content.ReadAsStringAsync()))
        {
            Assert.Equal(3, putDoc.RootElement.GetProperty("restDayOfWeek").GetInt32());
        }

        // GET returns the persisted value.
        var get = await client.GetAsync($"/api/v1/personnel-files/{employeeId}/assigned-positions/{assignmentId}");
        get.EnsureSuccessStatusCode();
        using var getDoc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.Equal(3, getDoc.RootElement.GetProperty("restDayOfWeek").GetInt32());
    }

    [Fact]
    public async Task EmploymentAssignment_RestDayOfWeekOutOfRange_IsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-MP-RD2", "Direccion MP RD2", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-MP-RD2", "Perfil MP RD2", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-MP-RD2", "Plaza MP RD2", profile.Id, maxEmployees: 2);
        var employeeId = await SeedCompletedEmployeeAsync(scenario.TenantId, "Doce", "Empleado");

        var assignment = await PostAssignmentAsync(client, employeeId, EmploymentAssignmentBody(slot.Id));

        // 7 is outside the DayOfWeek range (0 = Sunday … 6 = Saturday) → request validation 400.
        var put = await PutAssignmentAsync(
            client,
            employeeId,
            assignment.Id,
            assignment.Token,
            EmploymentAssignmentBodyWithRestDay(slot.Id, restDayOfWeek: 7));

        await AssertProblemDetailsAsync(put, HttpStatusCode.BadRequest, "common.validation");
    }

    private async Task<(Guid Id, Guid Token)> PostAssignmentAsync(HttpClient client, Guid employeeId, object body)
    {
        var response = await client.PostJsonAsync($"/api/v1/personnel-files/{employeeId}/assigned-positions", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (
            document.RootElement.GetProperty("employmentAssignmentPublicId").GetGuid(),
            document.RootElement.GetProperty("concurrencyToken").GetGuid());
    }

    private static async Task<HttpResponseMessage> PutAssignmentAsync(HttpClient client, Guid employeeId, Guid assignmentId, Guid token, object body)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/v1/personnel-files/{employeeId}/assigned-positions/{assignmentId}")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{token}\"");
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PatchAssignmentAsync(HttpClient client, Guid employeeId, Guid assignmentId, Guid token, object operations)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/assigned-positions/{assignmentId}")
        {
            Content = new StringContent(JsonSerializer.Serialize(operations), Encoding.UTF8, "application/json-patch+json")
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{token}\"");
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> DeleteAssignmentAsync(HttpClient client, Guid employeeId, Guid assignmentId, Guid token)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/v1/personnel-files/{employeeId}/assigned-positions/{assignmentId}");
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{token}\"");
        return await client.SendAsync(request);
    }
}
