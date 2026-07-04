using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Integration coverage for the definitive-retirement module: the full lifecycle round-trip
/// (register → authorize → interview tray → orchestrated execution → reversal restoring the execution
/// snapshot → new request), the state-machine and date guards (RETIREMENT_* errors), the separation of
/// duties (subject ≠ actor, requester ≠ authorizer — D-13 ratified) and the If-Match concurrency
/// convention. Employees are seeded directly (profile + active contract + active slotless assignment);
/// the retirement catalogs (VOLUNTARIA/RENUNCIA) ship seeded for SV.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static readonly DateTime RetirementHireDate = new(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // "Today" so the execution guard (FechaRetiro ≤ hoy, D-05) allows the manual execution.
    private static readonly DateTime RetirementEffectiveDate = DateTime.UtcNow.Date;

    private static TestUserContext CreateRetirementContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            PersonnelFilePermissionCodes.Admin,
            PersonnelFilePermissionCodes.AuthorizeRetirement,
            PersonnelFilePermissionCodes.RevertRetirement);

    private static TestUserContext CreateRetirementManagerOnlyContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            PersonnelFilePermissionCodes.Admin);

    /// <summary>
    /// Seeds a completed ACTIVE employee with a profile, an open contract and an active primary (slotless)
    /// assignment, so the execution has real rows to close and the reversal real rows to reopen.
    /// </summary>
    private async Task<Guid> SeedRetirementCandidateAsync(
        Guid tenantId,
        string firstName,
        string lastName,
        string employeeCode,
        string institutionalEmail,
        Guid? linkedUserPublicId = null)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var file = PersonnelFile.Create(
            PersonnelFileRecordType.Employee,
            firstName,
            lastName,
            new DateTime(1992, 5, 10, 0, 0, 0, DateTimeKind.Utc),
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
        if (linkedUserPublicId is { } linked)
        {
            file.Complete(linked);
        }
        else
        {
            file.CompleteWithoutLinkedUser();
        }

        dbContext.Set<PersonnelFile>().Add(file);
        await dbContext.SaveChangesAsync();

        var profile = PersonnelFileEmployeeProfile.Create(employeeCode, "ACTIVO", RetirementHireDate);
        profile.BindToPersonnelFile(file.Id);
        profile.SetTenantId(tenantId);
        dbContext.Set<PersonnelFileEmployeeProfile>().Add(profile);

        var contract = PersonnelFileContractHistory.Create(
            "INDEFINIDO",
            RetirementHireDate,
            contractEndDate: null,
            positionSlotPublicId: null,
            isActive: true,
            notes: "Contrato vigente");
        contract.BindToPersonnelFile(file.Id);
        contract.SetTenantId(tenantId);
        dbContext.Set<PersonnelFileContractHistory>().Add(contract);

        var assignment = PersonnelFileEmploymentAssignment.Create(
            "INDEFINIDO",
            contractTypeCode: null,
            workdayCode: null,
            payrollTypeCode: null,
            positionSlotPublicId: null,
            orgUnitPublicId: null,
            workCenterPublicId: null,
            costCenterPublicId: null,
            startDate: RetirementHireDate,
            endDate: null,
            isPrimary: true,
            isActive: true,
            notes: null);
        assignment.BindToPersonnelFile(file.Id);
        assignment.SetTenantId(tenantId);
        dbContext.Set<PersonnelFileEmploymentAssignment>().Add(assignment);

        await dbContext.SaveChangesAsync();
        return file.PublicId;
    }

    private static object RetirementRequestBody(
        Guid requesterFilePublicId,
        DateTime? retirementDate = null,
        string? notes = "Baja de prueba") =>
        new
        {
            requesterFilePublicId,
            requestDate = DateTime.UtcNow.Date,
            retirementDate = retirementDate ?? RetirementEffectiveDate,
            retirementCategoryCode = "VOLUNTARIA",
            retirementReasonCode = "MOTIVOS_PERSONALES",
            notes
        };

    private static async Task<(Guid RequestId, Guid Token)> CreateRetirementRequestAsync(
        HttpClient client,
        Guid employeeId,
        object body)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/personnel-files/{employeeId}/retirement-requests", body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, $"Create failed: {(int)response.StatusCode} {payload}");
        using var doc = JsonDocument.Parse(payload);
        return (
            doc.RootElement.GetProperty("retirementRequestPublicId").GetGuid(),
            doc.RootElement.GetProperty("concurrencyToken").GetGuid());
    }

    private static async Task<HttpResponseMessage> PatchRetirementAsync(
        HttpClient client,
        Guid employeeId,
        Guid requestId,
        string action,
        Guid token,
        object body)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/retirement-requests/{requestId}/{action}")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{token}\"");
        return await client.SendAsync(request);
    }

    private static async Task<Guid> ReadTokenAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("concurrencyToken").GetGuid();
    }

    [Fact]
    public async Task Retirement_FullLifecycle_ExecutesAndReverts()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        var employeeId = await SeedRetirementCandidateAsync(
            scenario.TenantId, "Elena", "Egresada", "EMP-RT-A", "elena.rt.a@empresa.test");
        var requesterId = await SeedRetirementCandidateAsync(
            scenario.TenantId, "Rafael", "Solicitante", "EMP-RT-A2", "rafael.rt.a@empresa.test");

        // [1] Register → SOLICITADA.
        var (requestId, token) = await CreateRetirementRequestAsync(client, employeeId, RetirementRequestBody(requesterId));

        // [2] Authorize → AUTORIZADA (interview enabled from here, D-07).
        var authorized = await PatchRetirementAsync(client, employeeId, requestId, "resolution", token,
            new { targetStatusCode = "AUTORIZADA", notes = (string?)null });
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
        token = await ReadTokenAsync(authorized);

        // [3] Interview tray lists the employee (no form configured → SIN_FORMULARIO).
        var trayResponse = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/retirement-requests/interview-tray");
        trayResponse.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await trayResponse.Content.ReadAsStringAsync()))
        {
            var row = doc.RootElement.EnumerateArray()
                .Single(item => item.GetProperty("personnelFilePublicId").GetGuid() == employeeId);
            Assert.Equal("SIN_FORMULARIO", row.GetProperty("interviewStatus").GetString());
        }

        // [4] Execute → EJECUTADA (orchestrated baja).
        var executed = await PatchRetirementAsync(client, employeeId, requestId, "execution", token,
            new { blockRehire = false, rehireBlockReason = (string?)null });
        Assert.Equal(HttpStatusCode.OK, executed.StatusCode);
        token = await ReadTokenAsync(executed);

        // Profile stamped: RETIRADO + retirement metadata written by the module (single door).
        var profileResponse = await client.GetAsync($"/api/v1/personnel-files/{employeeId}/employment-information");
        profileResponse.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await profileResponse.Content.ReadAsStringAsync()))
        {
            Assert.Equal("RETIRADO", doc.RootElement.GetProperty("employmentStatusCode").GetString());
            Assert.Equal("MOTIVOS_PERSONALES", doc.RootElement.GetProperty("retirementReasonCode").GetString());
            Assert.NotEqual(JsonValueKind.Null, doc.RootElement.GetProperty("retirementDate").ValueKind);
        }

        // Plazas + contracts closed at the retirement date (D-06).
        using (var doc = JsonDocument.Parse(await (await client.GetAsync($"/api/v1/personnel-files/{employeeId}/assigned-positions")).Content.ReadAsStringAsync()))
        {
            Assert.Equal(0, doc.RootElement.EnumerateArray().Count(item => item.GetProperty("isActive").GetBoolean()));
        }

        using (var doc = JsonDocument.Parse(await (await client.GetAsync($"/api/v1/personnel-files/{employeeId}/contract-history")).Content.ReadAsStringAsync()))
        {
            Assert.Equal(0, doc.RootElement.EnumerateArray().Count(item => item.GetProperty("isActive").GetBoolean()));
        }

        // BAJA journaled with the seeded APLICADA status (D-15).
        Assert.Contains("BAJA", await (await client.GetAsync($"/api/v1/personnel-files/{employeeId}/personnel-actions")).Content.ReadAsStringAsync());

        // Company bandeja sees it as EJECUTADA (RF-002).
        var bandejaResponse = await client.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/retirement-requests/query",
            new { statusCode = "EJECUTADA" });
        bandejaResponse.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await bandejaResponse.Content.ReadAsStringAsync()))
        {
            Assert.Equal(1, doc.RootElement.GetProperty("totalCount").GetInt32());
        }

        // [5] Revert → REVERTIDA (restores the execution snapshot, D-11).
        var reverted = await PatchRetirementAsync(client, employeeId, requestId, "reversal", token,
            new { reason = "Baja registrada por error administrativo." });
        Assert.Equal(HttpStatusCode.OK, reverted.StatusCode);

        // Profile restored: prior status (ACTIVO), baja cleared, seniority CONTINUOUS (HireDate intact).
        using (var doc = JsonDocument.Parse(await (await client.GetAsync($"/api/v1/personnel-files/{employeeId}/employment-information")).Content.ReadAsStringAsync()))
        {
            Assert.Equal("ACTIVO", doc.RootElement.GetProperty("employmentStatusCode").GetString());
            Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("retirementDate").ValueKind);
            Assert.Equal(RetirementHireDate, doc.RootElement.GetProperty("hireDate").GetDateTime());
        }

        // The EXACT closed rows reopened (previous end dates restored — here: none had one).
        using (var doc = JsonDocument.Parse(await (await client.GetAsync($"/api/v1/personnel-files/{employeeId}/assigned-positions")).Content.ReadAsStringAsync()))
        {
            var active = doc.RootElement.EnumerateArray().Where(item => item.GetProperty("isActive").GetBoolean()).ToArray();
            Assert.Single(active);
            Assert.Equal(JsonValueKind.Null, active[0].GetProperty("endDate").ValueKind);
        }

        using (var doc = JsonDocument.Parse(await (await client.GetAsync($"/api/v1/personnel-files/{employeeId}/contract-history")).Content.ReadAsStringAsync()))
        {
            Assert.Equal(1, doc.RootElement.EnumerateArray().Count(item => item.GetProperty("isActive").GetBoolean()));
        }

        // REVERSION_BAJA journaled; the employee left the interview tray (RN-008.2).
        Assert.Contains("REVERSION_BAJA", await (await client.GetAsync($"/api/v1/personnel-files/{employeeId}/personnel-actions")).Content.ReadAsStringAsync());
        using (var doc = JsonDocument.Parse(await (await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/retirement-requests/interview-tray")).Content.ReadAsStringAsync()))
        {
            Assert.DoesNotContain(
                doc.RootElement.EnumerateArray(),
                item => item.GetProperty("personnelFilePublicId").GetGuid() == employeeId);
        }

        // [6] The employee is eligible for a NEW request (RN-010.5) — the file is active again.
        _ = await CreateRetirementRequestAsync(client, employeeId, RetirementRequestBody(requesterId));
    }

    [Fact]
    public async Task Retirement_ExecuteBeforeRetirementDate_IsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        var employeeId = await SeedRetirementCandidateAsync(
            scenario.TenantId, "Fabio", "Futuro", "EMP-RT-B", "fabio.rt.b@empresa.test");
        var requesterId = await SeedRetirementCandidateAsync(
            scenario.TenantId, "Rita", "Solicita", "EMP-RT-B2", "rita.rt.b@empresa.test");

        var (requestId, token) = await CreateRetirementRequestAsync(
            client, employeeId, RetirementRequestBody(requesterId, retirementDate: DateTime.UtcNow.Date.AddDays(10)));
        var authorized = await PatchRetirementAsync(client, employeeId, requestId, "resolution", token,
            new { targetStatusCode = "AUTORIZADA", notes = (string?)null });
        token = await ReadTokenAsync(authorized);

        var response = await PatchRetirementAsync(client, employeeId, requestId, "execution", token,
            new { blockRehire = false, rehireBlockReason = (string?)null });

        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "RETIREMENT_EXECUTION_DATE_NOT_REACHED");
    }

    [Fact]
    public async Task Retirement_SecondOpenRequest_IsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        var employeeId = await SeedRetirementCandidateAsync(
            scenario.TenantId, "Doble", "Solicitud", "EMP-RT-C", "doble.rt.c@empresa.test");
        var requesterId = await SeedRetirementCandidateAsync(
            scenario.TenantId, "Rene", "Solicita", "EMP-RT-C2", "rene.rt.c@empresa.test");

        _ = await CreateRetirementRequestAsync(client, employeeId, RetirementRequestBody(requesterId));

        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/retirement-requests", RetirementRequestBody(requesterId));

        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "RETIREMENT_REQUEST_ALREADY_OPEN");
    }

    [Fact]
    public async Task Retirement_RejectWithoutNote_IsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        var employeeId = await SeedRetirementCandidateAsync(
            scenario.TenantId, "Nora", "Nota", "EMP-RT-D", "nora.rt.d@empresa.test");
        var requesterId = await SeedRetirementCandidateAsync(
            scenario.TenantId, "Raul", "Solicita", "EMP-RT-D2", "raul.rt.d@empresa.test");

        var (requestId, token) = await CreateRetirementRequestAsync(client, employeeId, RetirementRequestBody(requesterId));

        var response = await PatchRetirementAsync(client, employeeId, requestId, "resolution", token,
            new { targetStatusCode = "RECHAZADA", notes = "   " });

        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "RETIREMENT_RESOLUTION_NOTES_REQUIRED");
    }

    [Fact]
    public async Task Retirement_RequesterCannotAuthorize()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        var employeeId = await SeedRetirementCandidateAsync(
            scenario.TenantId, "Selena", "Sujeta", "EMP-RT-E", "selena.rt.e@empresa.test");
        // The requester's personnel file is LINKED to the acting user → the acting user IS the requester.
        var requesterId = await SeedRetirementCandidateAsync(
            scenario.TenantId, "Actor", "Solicitante", "EMP-RT-E2", "actor.rt.e@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        var (requestId, token) = await CreateRetirementRequestAsync(client, employeeId, RetirementRequestBody(requesterId));

        var response = await PatchRetirementAsync(client, employeeId, requestId, "resolution", token,
            new { targetStatusCode = "AUTORIZADA", notes = (string?)null });

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "RETIREMENT_REQUESTER_CANNOT_AUTHORIZE");
    }

    [Fact]
    public async Task Retirement_SubjectCannotAuthorizeTheirOwn()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        // The SUBJECT employee's file is linked to the acting user (an authorizer retiring themselves).
        var employeeId = await SeedRetirementCandidateAsync(
            scenario.TenantId, "Auto", "Autorizador", "EMP-RT-F", "auto.rt.f@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);
        var requesterId = await SeedRetirementCandidateAsync(
            scenario.TenantId, "Rosa", "Solicita", "EMP-RT-F2", "rosa.rt.f@empresa.test");

        var (requestId, token) = await CreateRetirementRequestAsync(client, employeeId, RetirementRequestBody(requesterId));

        var response = await PatchRetirementAsync(client, employeeId, requestId, "resolution", token,
            new { targetStatusCode = "AUTORIZADA", notes = (string?)null });

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden, "RETIREMENT_SELF_ACTION_FORBIDDEN");
    }

    [Fact]
    public async Task Retirement_AuthorizeWithoutDedicatedGrant_IsForbidden()
    {
        var scenario = await factory.ResetDatabaseAsync();
        // PersonnelFiles.Admin only — the AuthorizeRetirement policy deliberately EXCLUDES Admin (D-12).
        using var client = factory.CreateClientFor(CreateRetirementManagerOnlyContext(scenario));

        var employeeId = await SeedRetirementCandidateAsync(
            scenario.TenantId, "Gina", "Gestora", "EMP-RT-G", "gina.rt.g@empresa.test");
        var requesterId = await SeedRetirementCandidateAsync(
            scenario.TenantId, "Ruben", "Solicita", "EMP-RT-G2", "ruben.rt.g@empresa.test");

        var (requestId, token) = await CreateRetirementRequestAsync(client, employeeId, RetirementRequestBody(requesterId));

        var response = await PatchRetirementAsync(client, employeeId, requestId, "resolution", token,
            new { targetStatusCode = "AUTORIZADA", notes = (string?)null });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Retirement_AnnulAuthorized_FreesTheEmployee()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        var employeeId = await SeedRetirementCandidateAsync(
            scenario.TenantId, "Ana", "Anulada", "EMP-RT-H", "ana.rt.h@empresa.test");
        var requesterId = await SeedRetirementCandidateAsync(
            scenario.TenantId, "Rocio", "Solicita", "EMP-RT-H2", "rocio.rt.h@empresa.test");

        var (requestId, token) = await CreateRetirementRequestAsync(client, employeeId, RetirementRequestBody(requesterId));
        var authorized = await PatchRetirementAsync(client, employeeId, requestId, "resolution", token,
            new { targetStatusCode = "AUTORIZADA", notes = (string?)null });
        token = await ReadTokenAsync(authorized);

        var annulled = await PatchRetirementAsync(client, employeeId, requestId, "annulment", token,
            new { notes = "Se rescindió la baja." });
        Assert.Equal(HttpStatusCode.OK, annulled.StatusCode);

        // Out of the interview tray (RN-008.2) and free for a new request.
        using (var doc = JsonDocument.Parse(await (await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/retirement-requests/interview-tray")).Content.ReadAsStringAsync()))
        {
            Assert.DoesNotContain(
                doc.RootElement.EnumerateArray(),
                item => item.GetProperty("personnelFilePublicId").GetGuid() == employeeId);
        }

        _ = await CreateRetirementRequestAsync(client, employeeId, RetirementRequestBody(requesterId));
    }

    [Fact]
    public async Task Retirement_CancelSolicitada_Succeeds()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        var employeeId = await SeedRetirementCandidateAsync(
            scenario.TenantId, "Carmen", "Cancelada", "EMP-RT-I", "carmen.rt.i@empresa.test");
        var requesterId = await SeedRetirementCandidateAsync(
            scenario.TenantId, "Rodrigo", "Solicita", "EMP-RT-I2", "rodrigo.rt.i@empresa.test");

        var (requestId, token) = await CreateRetirementRequestAsync(client, employeeId, RetirementRequestBody(requesterId));

        var canceled = await PatchRetirementAsync(client, employeeId, requestId, "cancel", token,
            new { notes = "Registrada por error." });
        Assert.Equal(HttpStatusCode.OK, canceled.StatusCode);
        using var doc = JsonDocument.Parse(await canceled.Content.ReadAsStringAsync());
        Assert.Equal("ANULADA", doc.RootElement.GetProperty("requestStatusCode").GetString());
    }

    [Fact]
    public async Task Retirement_StaleConcurrencyToken_IsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        var employeeId = await SeedRetirementCandidateAsync(
            scenario.TenantId, "Tomas", "Token", "EMP-RT-J", "tomas.rt.j@empresa.test");
        var requesterId = await SeedRetirementCandidateAsync(
            scenario.TenantId, "Regina", "Solicita", "EMP-RT-J2", "regina.rt.j@empresa.test");

        var (requestId, _) = await CreateRetirementRequestAsync(client, employeeId, RetirementRequestBody(requesterId));

        var response = await PatchRetirementAsync(client, employeeId, requestId, "resolution", Guid.NewGuid(),
            new { targetStatusCode = "AUTORIZADA", notes = (string?)null });

        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task Retirement_LegacyPutWithRetiradoStatus_IsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        var employeeId = await SeedRetirementCandidateAsync(
            scenario.TenantId, "Paula", "Puerta", "EMP-RT-K", "paula.rt.k@empresa.test");

        // Fetch the profile's current token (the seed created it).
        var profileResponse = await client.GetAsync($"/api/v1/personnel-files/{employeeId}/employment-information");
        profileResponse.EnsureSuccessStatusCode();
        Guid profileToken;
        using (var doc = JsonDocument.Parse(await profileResponse.Content.ReadAsStringAsync()))
        {
            profileToken = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Put, $"/api/v1/personnel-files/{employeeId}/employment-information")
        {
            Content = JsonContent.Create(new
            {
                employeeCode = "EMP-RT-K",
                employmentStatusCode = "RETIRADO",
                hireDate = RetirementHireDate
            })
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{profileToken}\"");
        var response = await client.SendAsync(request);

        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "EMPLOYMENT_STATUS_RETIRADO_RESERVED");
    }
}
