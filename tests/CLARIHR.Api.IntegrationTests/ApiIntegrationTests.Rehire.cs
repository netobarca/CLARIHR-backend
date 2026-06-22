using System.Net;
using System.Net.Http.Json;
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
/// Integration coverage for the employee-rehire flow: the atomic round-trip that reactivates a
/// retired employee, preserves the prior period as derived history (RF-003/RF-004), opens a new
/// period with a multi-plaza assignment (RF-006) and records the RECONTRATACION action (RF-009),
/// plus the eligibility/authorization guards (RN-02/RN-14/RN-06 → REHIRE_* errors). A retired
/// employee is seeded directly (profile + prior contract); the slot is built via the API helpers.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static readonly DateTime PriorHireDate = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PriorRetirementDate = new(2024, 1, 31, 0, 0, 0, DateTimeKind.Utc);

    // The new period starts "today" so the new assignment falls within the freshly-created slot's
    // effective window (the slot's EffectiveFromUtc is ~now), matching the multi-plaza test convention.
    private static readonly DateTime RehireDate = DateTime.UtcNow.Date;

    private static TestUserContext CreateRehireAuthorizerContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            PersonnelFilePermissionCodes.Admin,
            PersonnelFilePermissionCodes.AuthorizeRehire,
            PositionSlotPermissionCodes.Admin,
            OrgUnitPermissionCodes.Admin,
            JobProfilePermissionCodes.Admin,
            PositionDescriptionCatalogPermissionCodes.Admin,
            CompetencyFrameworkPermissionCodes.Admin);

    /// <summary>
    /// Seeds a completed employee whose employment is inactive (retired) with an employee profile and
    /// a still-open prior contract, so the rehire flow has a real prior period to close and preserve.
    /// Returns the file public id and its current concurrency token (for the If-Match header).
    /// </summary>
    private async Task<(Guid PublicId, Guid Token)> SeedRehireCandidateAsync(
        Guid tenantId,
        string firstName,
        string lastName,
        string employeeCode,
        string institutionalEmail,
        bool isEmploymentActive = false,
        bool isRehireBlocked = false)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var file = PersonnelFile.Create(
            PersonnelFileRecordType.Employee,
            firstName,
            lastName,
            new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            maritalStatus: null,
            profession: null,
            nationality: "SV",
            personalEmail: null,
            institutionalEmail: institutionalEmail,
            personalPhone: null,
            institutionalPhone: null,
            birthCountry: null,
            birthDepartment: null,
            birthMunicipality: null,
            photoFilePublicId: null,
            orgUnitPublicId: null);
        file.SetTenantId(tenantId);
        file.CompleteWithoutLinkedUser();
        if (isRehireBlocked)
        {
            file.BlockRehire("Marcado como no recontratable para la prueba.");
        }

        dbContext.Set<PersonnelFile>().Add(file);
        await dbContext.SaveChangesAsync();

        var profile = PersonnelFileEmployeeProfile.Create(
            employeeCode,
            isEmploymentActive ? "ACTIVO" : "RETIRADO",
            PriorHireDate,
            retirementCategoryCode: null,
            retirementReasonCode: isEmploymentActive ? null : "RENUNCIA",
            retirementNotes: null,
            retirementDate: isEmploymentActive ? null : PriorRetirementDate);
        profile.BindToPersonnelFile(file.Id);
        profile.SetTenantId(tenantId);
        dbContext.Set<PersonnelFileEmployeeProfile>().Add(profile);

        var contract = PersonnelFileContractHistory.Create(
            "INDEFINIDO",
            PriorHireDate,
            contractEndDate: null,
            positionSlotPublicId: null,
            isActive: true,
            notes: "Periodo anterior");
        contract.BindToPersonnelFile(file.Id);
        contract.SetTenantId(tenantId);
        dbContext.Set<PersonnelFileContractHistory>().Add(contract);

        await dbContext.SaveChangesAsync();
        return (file.PublicId, file.ConcurrencyToken);
    }

    private static object RehireBody(
        Guid positionSlotId,
        bool priorPeriodClosureConfirmed = true,
        string? authorizationReason = null,
        bool createUserAccount = false,
        string? newInstitutionalEmail = null) =>
        new
        {
            newHireDate = RehireDate,
            contractTypeCode = "INDEFINIDO",
            contractStartDate = RehireDate,
            contractEndDate = (DateTime?)null,
            positionSlotPublicId = positionSlotId,
            assignmentTypeCode = "INDEFINIDO",
            createUserAccount,
            newInstitutionalEmail,
            priorPeriodClosureConfirmed,
            authorizationReason
        };

    private static async Task<HttpResponseMessage> RehireAsync(HttpClient client, Guid employeeId, Guid token, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/personnel-files/{employeeId}/rehire")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{token}\"");
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Rehire_RetiredEmployee_OpensNewPeriodAndPreservesHistory()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-RH-A", "Direccion RH A", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-RH-A", "Perfil RH A", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-RH-A", "Plaza RH A", profile.Id, maxEmployees: 1);
        var (employeeId, token) = await SeedRehireCandidateAsync(
            scenario.TenantId, "Reina", "Recontratada", "EMP-RH-A", "reina.rh.a@empresa.test");

        var response = await RehireAsync(client, employeeId, token, RehireBody(slot.Id));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The same file is active again (no duplicate, D-01). The PublicContract resolver renders the
        // file's public id as `publicId`.
        using (var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            var file = doc.RootElement.GetProperty("personnelFile");
            Assert.Equal(employeeId, file.GetProperty("publicId").GetGuid());
            Assert.True(file.GetProperty("isActive").GetBoolean());
        }

        // Prior period preserved + new one opened: two contracts, exactly one active (RF-004).
        var contractsResponse = await client.GetAsync($"/api/v1/personnel-files/{employeeId}/contract-history");
        contractsResponse.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await contractsResponse.Content.ReadAsStringAsync()))
        {
            var contracts = doc.RootElement.EnumerateArray().ToArray();
            Assert.Equal(2, contracts.Length);
            Assert.Equal(1, contracts.Count(contract => contract.GetProperty("isActive").GetBoolean()));
        }

        // Timeline derives both periods (RF-011); exactly the active one is current.
        var timelineResponse = await client.GetAsync($"/api/v1/personnel-files/{employeeId}/employment-periods");
        timelineResponse.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await timelineResponse.Content.ReadAsStringAsync()))
        {
            Assert.Equal(2, doc.RootElement.GetProperty("periodCount").GetInt32());
            Assert.Equal(1, doc.RootElement.GetProperty("periods").EnumerateArray().Count(period => period.GetProperty("isCurrent").GetBoolean()));
        }

        // New active primary assignment on the chosen slot (RF-006/D-16).
        var assignmentsResponse = await client.GetAsync($"/api/v1/personnel-files/{employeeId}/assigned-positions");
        assignmentsResponse.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await assignmentsResponse.Content.ReadAsStringAsync()))
        {
            var active = doc.RootElement.EnumerateArray().Where(item => item.GetProperty("isActive").GetBoolean()).ToArray();
            Assert.Single(active);
            Assert.Equal(slot.Id, active[0].GetProperty("positionSlotPublicId").GetGuid());
            Assert.True(active[0].GetProperty("isPrimary").GetBoolean());
        }

        // Employee profile reflects the new active period (RF-003): status back to ACTIVO, baja cleared.
        var profileResponse = await client.GetAsync($"/api/v1/personnel-files/{employeeId}/employment-information");
        profileResponse.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await profileResponse.Content.ReadAsStringAsync()))
        {
            Assert.Equal("ACTIVO", doc.RootElement.GetProperty("employmentStatusCode").GetString());
            Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("retirementDate").ValueKind);
        }

        // Append-only RECONTRATACION personnel action recorded (RF-009).
        var actionsResponse = await client.GetAsync($"/api/v1/personnel-files/{employeeId}/personnel-actions");
        actionsResponse.EnsureSuccessStatusCode();
        Assert.Contains("RECONTRATACION", await actionsResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Rehire_PriorPeriodNotConfirmed_IsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-RH-B", "Direccion RH B", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-RH-B", "Perfil RH B", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-RH-B", "Plaza RH B", profile.Id, maxEmployees: 1);
        var (employeeId, token) = await SeedRehireCandidateAsync(
            scenario.TenantId, "Pedro", "Pendiente", "EMP-RH-B", "pedro.rh.b@empresa.test");

        var response = await RehireAsync(client, employeeId, token, RehireBody(slot.Id, priorPeriodClosureConfirmed: false));

        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "REHIRE_PRIOR_PERIOD_OPEN");
    }

    [Fact]
    public async Task Rehire_ActiveEmployee_IsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-RH-C", "Direccion RH C", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-RH-C", "Perfil RH C", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-RH-C", "Plaza RH C", profile.Id, maxEmployees: 1);
        var (employeeId, token) = await SeedRehireCandidateAsync(
            scenario.TenantId, "Aida", "Activa", "EMP-RH-C", "aida.rh.c@empresa.test", isEmploymentActive: true);

        var response = await RehireAsync(client, employeeId, token, RehireBody(slot.Id));

        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "REHIRE_NOT_RETIRED");
    }

    [Fact]
    public async Task Rehire_BlockedFileWithoutAuthorization_IsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-RH-D", "Direccion RH D", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-RH-D", "Perfil RH D", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-RH-D", "Plaza RH D", profile.Id, maxEmployees: 1);
        var (employeeId, token) = await SeedRehireCandidateAsync(
            scenario.TenantId, "Bruno", "Bloqueado", "EMP-RH-D", "bruno.rh.d@empresa.test", isRehireBlocked: true);

        // Even with a justification, a manager lacking AuthorizeRehire cannot override the block.
        var response = await RehireAsync(client, employeeId, token, RehireBody(slot.Id, authorizationReason: "Necesitamos su experiencia."));

        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "REHIRE_REQUIRES_AUTHORIZATION");
    }

    [Fact]
    public async Task Rehire_BlockedFileWithAuthorization_Succeeds()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRehireAuthorizerContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-RH-E", "Direccion RH E", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-RH-E", "Perfil RH E", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-RH-E", "Plaza RH E", profile.Id, maxEmployees: 1);
        var (employeeId, token) = await SeedRehireCandidateAsync(
            scenario.TenantId, "Bianca", "Bloqueada", "EMP-RH-E", "bianca.rh.e@empresa.test", isRehireBlocked: true);

        var response = await RehireAsync(client, employeeId, token, RehireBody(slot.Id, authorizationReason: "Aprobado por jefatura."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Rehire_StaleConcurrencyToken_IsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-RH-F", "Direccion RH F", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-RH-F", "Perfil RH F", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-RH-F", "Plaza RH F", profile.Id, maxEmployees: 1);
        var (employeeId, _) = await SeedRehireCandidateAsync(
            scenario.TenantId, "Carla", "Concurrencia", "EMP-RH-F", "carla.rh.f@empresa.test");

        var response = await RehireAsync(client, employeeId, Guid.NewGuid(), RehireBody(slot.Id));

        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
    }
}
