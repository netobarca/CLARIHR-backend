using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Integration coverage for the settlement ("liquidación") module: the real-settlement round-trip over an
/// executed retirement (create per plaza → adjust a line with an audited override → issue with the
/// LIQUIDACION journal → boleta/document export → annul → recreate), the scenario round-trip (create over
/// an active plaza → edit the estimated date with server-side recalculation → soft delete), the anchor and
/// uniqueness guards (SETTLEMENT_* errors), the reversal hook (draft auto-annulled; an ISSUED settlement
/// blocks the reversal — D-17) and the company bandeja + export. Reuses the retirement lifecycle helpers of
/// this partial class to produce the executed baja.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    /// <summary>
    /// Seeds a completed ACTIVE employee ready to be settled: profile (with the applicable minimum wage on
    /// the "ficha" — RF-011), open contract, active primary assignment and the plaza's negotiated
    /// SALARIO_BASE compensation concept. Returns the file and the plaza (assignment) ids.
    /// </summary>
    private async Task<(Guid FileId, Guid AssignmentId)> SeedSettlementCandidateAsync(
        Guid tenantId,
        string firstName,
        string lastName,
        string employeeCode,
        string institutionalEmail,
        decimal monthlySalary = 600m,
        decimal? minimumMonthlyWage = 365m,
        Guid? linkedUserPublicId = null)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var file = PersonnelFile.Create(
            PersonnelFileRecordType.Employee,
            firstName,
            lastName,
            new DateTime(1990, 2, 20, 0, 0, 0, DateTimeKind.Utc),
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

        var profile = PersonnelFileEmployeeProfile.Create(employeeCode, "ACTIVO", RetirementHireDate, minimumMonthlyWage);
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

        var salary = PersonnelFileCompensationConcept.Create(
            assignment.PublicId,
            CompensationNature.Ingreso,
            "SALARIO_BASE",
            deductionClass: null,
            CompensationCalculationType.Fixed,
            monthlySalary,
            calculationBaseCode: null,
            employerRate: null,
            contributionCap: null,
            currencyCode: "USD",
            payPeriodCode: "MENSUAL",
            counterpartyName: null,
            externalReference: null,
            startDate: RetirementHireDate,
            endDate: null,
            isActive: true,
            isSystemSuggested: false,
            notes: null);
        salary.BindToPersonnelFile(file.Id);
        salary.SetTenantId(tenantId);
        dbContext.Set<PersonnelFileCompensationConcept>().Add(salary);
        await dbContext.SaveChangesAsync();

        return (file.PublicId, assignment.PublicId);
    }

    /// <summary>Drives the retirement lifecycle (register → authorize → execute) so the settlement has its anchor.</summary>
    private async Task ExecuteRetirementAsync(HttpClient client, Guid employeeId, Guid requesterId)
    {
        var (requestId, token) = await CreateRetirementRequestAsync(client, employeeId, RetirementRequestBody(requesterId));
        var authorized = await PatchRetirementAsync(client, employeeId, requestId, "resolution", token,
            new { targetStatusCode = "AUTORIZADA", notes = (string?)null });
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
        var executed = await PatchRetirementAsync(client, employeeId, requestId, "execution", await ReadTokenAsync(authorized),
            new { blockRehire = false, rehireBlockReason = (string?)null });
        Assert.Equal(HttpStatusCode.OK, executed.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendSettlementAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        Guid token,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.Headers.TryAddWithoutValidation("If-Match", $"\"{token}\"");
        return await client.SendAsync(request);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    [Fact]
    public async Task Settlement_FullLifecycle_CreatesIssuesExportsAndAnnuls()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        // The requester file is LINKED to the acting user (D-06: the registering manager is the default requester).
        var (employeeId, plazaId) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Lucía", "Liquidada", "EMP-LQ-A", "lucia.lq.a@empresa.test");
        var (retirementRequesterId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Rafael", "Solicitante", "EMP-LQ-A3", "rafael.lq.a@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "DeRrhh", "EMP-LQ-A2", "gestora.lq.a@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        await ExecuteRetirementAsync(client, employeeId, retirementRequesterId);

        // [1] Create the real settlement of the plaza the retirement closed (facts inherited read-only).
        var createResponse = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = plazaId, requestDate = DateTime.UtcNow.Date, notes = "Liquidación E2E" });
        var createPayload = await createResponse.Content.ReadAsStringAsync();
        Assert.True(createResponse.StatusCode == HttpStatusCode.OK, $"Create failed: {(int)createResponse.StatusCode} {createPayload}");

        Guid settlementId, token, salarioLineId;
        decimal netPay;
        using (var doc = JsonDocument.Parse(createPayload))
        {
            var root = doc.RootElement;
            settlementId = root.GetProperty("publicId").GetGuid();
            token = root.GetProperty("concurrencyToken").GetGuid();
            Assert.Equal("BORRADOR", root.GetProperty("statusCode").GetString());
            Assert.Equal("VOLUNTARIA", root.GetProperty("retirementCategoryCode").GetString());
            Assert.Equal(600m, root.GetProperty("monthlyBaseSalary").GetDecimal());
            Assert.True(root.GetProperty("totalIncomes").GetDecimal() > 0);
            Assert.True(root.GetProperty("provisionTotal").GetDecimal() >= root.GetProperty("totalIncomes").GetDecimal());
            netPay = root.GetProperty("netPay").GetDecimal();
            Assert.True(netPay > 0);

            var lines = root.GetProperty("lines").EnumerateArray().ToArray();
            Assert.Contains(lines, line => line.GetProperty("conceptCode").GetString() == "RENUNCIA_VOLUNTARIA");
            Assert.Contains(lines, line => line.GetProperty("conceptCode").GetString() == "ISSS_PATRONAL");
            salarioLineId = lines.Single(line => line.GetProperty("conceptCode").GetString() == "SALARIO")
                .GetProperty("publicId").GetGuid();
        }

        // [2] Audited override on the salario line (D-14): note mandatory, computed value stays visible.
        var overrideRejected = await SendSettlementAsync(client, HttpMethod.Put,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/lines/{salarioLineId}",
            token, new { overrideAmount = 350m });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, overrideRejected.StatusCode);

        var overridden = await SendSettlementAsync(client, HttpMethod.Put,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/lines/{salarioLineId}",
            token, new { overrideAmount = 350m, overrideReason = "Ajuste acordado con el contador" });
        Assert.Equal(HttpStatusCode.OK, overridden.StatusCode);
        using (var doc = await ReadJsonAsync(overridden))
        {
            token = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
            var salario = doc.RootElement.GetProperty("lines").EnumerateArray()
                .Single(line => line.GetProperty("publicId").GetGuid() == salarioLineId);
            Assert.Equal(350m, salario.GetProperty("finalAmount").GetDecimal());
            Assert.True(salario.GetProperty("calculatedAmount").GetDecimal() > 0);
        }

        // [3] Issue → EMITIDA + LIQUIDACION journaled (D-15); the record freezes.
        var issued = await SendSettlementAsync(client, HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/issuance",
            token, new { confirmNegativeNet = false });
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        using (var doc = await ReadJsonAsync(issued))
        {
            Assert.Equal("EMITIDA", doc.RootElement.GetProperty("statusCode").GetString());
            token = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
        }

        Assert.Contains("LIQUIDACION", await (await client.GetAsync($"/api/v1/personnel-files/{employeeId}/personnel-actions")).Content.ReadAsStringAsync());

        var editRejected = await SendSettlementAsync(client, HttpMethod.Put,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/lines/{salarioLineId}",
            token, new { isIncluded = false });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, editRejected.StatusCode);

        // [4] Documents: sectioned xlsx + boleta PDF (D-19 — PDF in Fase 1).
        var xlsx = await client.GetAsync($"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/document?format=xlsx");
        Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", xlsx.Content.Headers.ContentType?.MediaType);

        var pdf = await client.GetAsync($"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/document?format=pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.True((await pdf.Content.ReadAsByteArrayAsync()).Length > 500);

        // [5] Duplicate per (retirement × plaza) is rejected while the settlement is live (D-16).
        var duplicate = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = plazaId, requestDate = DateTime.UtcNow.Date });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, duplicate.StatusCode);
        Assert.Contains("SETTLEMENT_ALREADY_EXISTS_FOR_POSITION", await duplicate.Content.ReadAsStringAsync());

        // [6] Annulling an EMITIDA requires a reason; annulled frees the slot for a corrected settlement.
        var annulNoReason = await SendSettlementAsync(client, HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/annulment", token, new { });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, annulNoReason.StatusCode);

        var annulled = await SendSettlementAsync(client, HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/annulment",
            token, new { reason = "Error en los días pendientes" });
        Assert.Equal(HttpStatusCode.OK, annulled.StatusCode);
        using (var doc = await ReadJsonAsync(annulled))
        {
            Assert.Equal("ANULADA", doc.RootElement.GetProperty("statusCode").GetString());
        }

        var recreated = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = plazaId, requestDate = DateTime.UtcNow.Date });
        Assert.Equal(HttpStatusCode.OK, recreated.StatusCode);
    }

    [Fact]
    public async Task Settlement_WithoutExecutedRetirement_IsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        var (employeeId, plazaId) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Andrés", "Activo", "EMP-LQ-B", "andres.lq.b@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "DeRrhh", "EMP-LQ-B2", "gestora.lq.b@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = plazaId, requestDate = DateTime.UtcNow.Date });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("SETTLEMENT_RETIREMENT_NOT_EXECUTED", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Scenario_FullLifecycle_SimulatesRecalculatesAndDeletes()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        var (employeeId, plazaId) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Sonia", "Simulada", "EMP-LQ-C", "sonia.lq.c@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "DeRrhh", "EMP-LQ-C2", "gestora.lq.c@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        // [1] Create the simulation over the ACTIVE plaza with an estimated future date.
        var estimated = DateTime.UtcNow.Date.AddDays(90);
        var created = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements/scenarios",
            new
            {
                assignedPositionPublicId = plazaId,
                estimatedRetirementDate = estimated,
                retirementCategoryCode = "VOLUNTARIA",
                retirementReasonCode = "MOTIVOS_PERSONALES",
                requestDate = DateTime.UtcNow.Date,
            });
        var createdPayload = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.OK, $"Scenario create failed: {(int)created.StatusCode} {createdPayload}");

        Guid scenarioId, token;
        decimal firstBenefit;
        using (var doc = JsonDocument.Parse(createdPayload))
        {
            var root = doc.RootElement;
            scenarioId = root.GetProperty("publicId").GetGuid();
            token = root.GetProperty("concurrencyToken").GetGuid();
            Assert.Equal("Escenario", root.GetProperty("kind").GetString());
            Assert.Equal(JsonValueKind.Null, root.GetProperty("statusCode").ValueKind);
            firstBenefit = root.GetProperty("lines").EnumerateArray()
                .Single(line => line.GetProperty("conceptCode").GetString() == "RENUNCIA_VOLUNTARIA")
                .GetProperty("finalAmount").GetDecimal();
            Assert.True(firstBenefit > 0);
        }

        // The simulation produced NO side effects: the employee stays ACTIVO.
        using (var doc = await ReadJsonAsync(await client.GetAsync($"/api/v1/personnel-files/{employeeId}/employment-information")))
        {
            Assert.Equal("ACTIVO", doc.RootElement.GetProperty("employmentStatusCode").GetString());
        }

        // [2] Pushing the estimated date one year out recalculates a larger benefit server-side.
        var moved = await SendSettlementAsync(client, HttpMethod.Put,
            $"/api/v1/personnel-files/{employeeId}/settlements/{scenarioId}",
            token, new
            {
                requestDate = DateTime.UtcNow.Date,
                estimatedRetirementDate = estimated.AddYears(1),
                parameters = new { minimumMonthlyWage = 365m },
            });
        var movedPayload = await moved.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == moved.StatusCode, $"Update failed: {(int)moved.StatusCode} {movedPayload}");
        using (var doc = JsonDocument.Parse(movedPayload))
        {
            token = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
            var recalculated = doc.RootElement.GetProperty("lines").EnumerateArray()
                .Single(line => line.GetProperty("conceptCode").GetString() == "RENUNCIA_VOLUNTARIA")
                .GetProperty("finalAmount").GetDecimal();
            Assert.True(recalculated > firstBenefit);
        }

        // [3] Scenario export carries the SIMULACIÓN mark; soft delete removes it from the listings.
        var export = await client.GetAsync($"/api/v1/personnel-files/{employeeId}/settlements/{scenarioId}/document?format=csv");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Contains("SIMULACI", await export.Content.ReadAsStringAsync());

        var deleted = await SendSettlementAsync(client, HttpMethod.Delete,
            $"/api/v1/personnel-files/{employeeId}/settlements/{scenarioId}", token);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using (var doc = await ReadJsonAsync(await client.GetAsync($"/api/v1/personnel-files/{employeeId}/settlements")))
        {
            Assert.DoesNotContain(
                doc.RootElement.EnumerateArray(),
                item => item.GetProperty("publicId").GetGuid() == scenarioId);
        }
    }

    [Fact]
    public async Task Reversal_AnnulsDraftSettlement_AndIsBlockedByIssuedOne()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        var (employeeId, plazaId) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Rodrigo", "Revertido", "EMP-LQ-D", "rodrigo.lq.d@empresa.test");
        var (retirementRequesterId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Raquel", "Solicitante", "EMP-LQ-D3", "raquel.lq.d@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "DeRrhh", "EMP-LQ-D2", "gestora.lq.d@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        var (requestId, retirementToken) = await CreateRetirementRequestAsync(client, employeeId, RetirementRequestBody(retirementRequesterId));
        var authorized = await PatchRetirementAsync(client, employeeId, requestId, "resolution", retirementToken,
            new { targetStatusCode = "AUTORIZADA", notes = (string?)null });
        var executed = await PatchRetirementAsync(client, employeeId, requestId, "execution", await ReadTokenAsync(authorized),
            new { blockRehire = false, rehireBlockReason = (string?)null });
        Assert.Equal(HttpStatusCode.OK, executed.StatusCode);
        retirementToken = await ReadTokenAsync(executed);

        // Draft settlement + ISSUE it → the reversal must be blocked (D-17).
        var created = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = plazaId, requestDate = DateTime.UtcNow.Date });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Guid settlementId, settlementToken;
        using (var doc = await ReadJsonAsync(created))
        {
            settlementId = doc.RootElement.GetProperty("publicId").GetGuid();
            settlementToken = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
        }

        var issued = await SendSettlementAsync(client, HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/issuance",
            settlementToken, new { confirmNegativeNet = false });
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        settlementToken = await ReadTokenAsync(issued);

        var blockedReversal = await PatchRetirementAsync(client, employeeId, requestId, "reversal", retirementToken,
            new { reason = "Intento con liquidación emitida" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, blockedReversal.StatusCode);
        Assert.Contains("RETIREMENT_REVERSAL_BLOCKED_BY_SETTLEMENT", await blockedReversal.Content.ReadAsStringAsync());

        // Annul the issued one and leave a DRAFT in its place → the reversal auto-annuls the draft.
        var annulled = await SendSettlementAsync(client, HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/annulment",
            settlementToken, new { reason = "Liberar la reversión" });
        Assert.Equal(HttpStatusCode.OK, annulled.StatusCode);

        var draft = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = plazaId, requestDate = DateTime.UtcNow.Date });
        Assert.Equal(HttpStatusCode.OK, draft.StatusCode);
        Guid draftId;
        using (var doc = await ReadJsonAsync(draft))
        {
            draftId = doc.RootElement.GetProperty("publicId").GetGuid();
        }

        var reverted = await PatchRetirementAsync(client, employeeId, requestId, "reversal", retirementToken,
            new { reason = "Baja registrada por error" });
        Assert.Equal(HttpStatusCode.OK, reverted.StatusCode);

        using (var doc = await ReadJsonAsync(await client.GetAsync($"/api/v1/personnel-files/{employeeId}/settlements/{draftId}")))
        {
            Assert.Equal("ANULADA", doc.RootElement.GetProperty("statusCode").GetString());
            Assert.Contains("Reversión de retiro", doc.RootElement.GetProperty("annulmentReason").GetString());
        }
    }

    [Fact]
    public async Task SettlementsBandeja_QueryAndExport_Work()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        var (employeeId, plazaId) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Beatriz", "Bandeja", "EMP-LQ-E", "beatriz.lq.e@empresa.test");
        var (retirementRequesterId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Rita", "Solicitante", "EMP-LQ-E3", "rita.lq.e@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "DeRrhh", "EMP-LQ-E2", "gestora.lq.e@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        await ExecuteRetirementAsync(client, employeeId, retirementRequesterId);
        var created = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = plazaId, requestDate = DateTime.UtcNow.Date });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var bandeja = await client.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/settlements/query",
            new { statusCode = "BORRADOR" });
        bandeja.EnsureSuccessStatusCode();
        using (var doc = await ReadJsonAsync(bandeja))
        {
            Assert.Equal(1, doc.RootElement.GetProperty("totalCount").GetInt32());
            Assert.Equal(1, doc.RootElement.GetProperty("statusCounts").GetProperty("BORRADOR").GetInt32());
            var row = doc.RootElement.GetProperty("items").EnumerateArray().Single();
            Assert.Equal(employeeId, row.GetProperty("personnelFilePublicId").GetGuid());
            Assert.True(row.GetProperty("netPay").GetDecimal() > 0);
        }

        var export = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/settlements/export?format=xlsx");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", export.Content.Headers.ContentType?.MediaType);

        var badFormat = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/settlements/export?format=doc");
        Assert.Equal(HttpStatusCode.BadRequest, badFormat.StatusCode);
    }
}
