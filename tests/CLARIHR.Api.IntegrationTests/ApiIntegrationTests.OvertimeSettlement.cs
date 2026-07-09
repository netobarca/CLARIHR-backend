using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// End-to-end coverage of the overtime ↔ settlement integration (REQ-007 PR-6, RF-014/§0.15). Unlike the
/// recurring / one-time income suggestions (a MANUAL line at a KNOWN amount, seed <c>IsSystemCalculated=false</c>),
/// the overtime pay-off is an ENGINE-CALCULATED line: the settlement's plaza carries an automatic
/// <c>HORAS_EXTRAS_PENDIENTES_PAGO</c> line (seed <c>-9915</c>, <c>IsSystemCalculated=true</c>) valued at
/// Σ(hours × factor) × hourly rate — for a plaza with two AUTORIZADA records (2.50 h × 2.00 + 1.50 h × 2.50 = 8.75
/// factored hours), a $300 monthly salary (⇒ $10/day) and 8 h/day, the line is 8.75 × (10 ÷ 8 = 1.25) = <b>$10.94</b>
/// (golden A.4-16). Issuing with the line INCLUDED closes the plaza's overtime — the elapsed records become APLICADA
/// and the FUTURE organized shifts are annulled — and annulling the settlement reopens exactly those; EXCLUDING the
/// line closes nothing; appending the concept as a MANUAL line is rejected (system concept → 422
/// <c>SETTLEMENT_CONCEPT_INVALID</c>); and the whole flow NEVER writes to <c>PersonnelFilePayrollTransaction</c> nor
/// <c>PersonnelFileCompensationConcept</c> (RN-20). Reuses the settlement / retirement helpers of the sibling
/// partials (<see cref="SeedSettlementCandidateAsync"/>, <see cref="ExecuteRetirementAsync"/>,
/// <see cref="CountLedgerRowsAsync"/>) and the overtime helpers of <c>ApiIntegrationTests.Overtime.cs</c>
/// (<see cref="SeedOvertimeMastersAsync"/>, <see cref="OvertimeBody"/>, <see cref="CreateOvertimeAsync"/>,
/// <see cref="PatchOvertimeAsync"/>).
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static JsonElement? OvertimePayLineOrNull(JsonElement settlementRoot) =>
        settlementRoot.GetProperty("lines").EnumerateArray()
            .Where(line => line.GetProperty("conceptCode").GetString() == "HORAS_EXTRAS_PENDIENTES_PAGO")
            .Select(line => (JsonElement?)line)
            .FirstOrDefault();

    /// <summary>The persisted status of an overtime record (IgnoreQueryFilters so an annulled — is_active=false — one is visible).</summary>
    private async Task<string> GetOvertimeRecordStatusAsync(Guid recordId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await dbContext.Set<PersonnelFileOvertimeRecord>()
            .IgnoreQueryFilters()
            .Where(item => item.PublicId == recordId)
            .Select(item => item.StatusCode)
            .FirstAsync();
    }

    [Fact]
    public async Task OvertimeSettlement_CalculatedLine_AppliesElapsedAnnulsFutureAndReopensOnAnnul()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var authorizerUserId = Guid.NewGuid();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));
        using var authorizer = factory.CreateClientFor(OvertimeAuthorizerContext(scenario, authorizerUserId));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        // $300 monthly salary ⇒ $10/day ⇒ hourly rate 10 ÷ 8 = $1.25.
        var (employeeId, plazaId) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Horacio", "Extra", "EMP-OTS-A", "horacio.ots.a@empresa.test", monthlySalary: 300m);
        var (requesterId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Rodrigo", "Solicitante", "EMP-OTS-A3", "rodrigo.ots.a@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "DeRrhh", "EMP-OTS-A2", "gestora.ots.a@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        // Two elapsed AUTORIZADA records on the plaza (2.50 h × 2.00 + 1.50 h × 2.50 = 8.75 factored hours) plus a
        // FUTURE organized shift (excluded from the pay-off line but annulled on issue). Authorized BEFORE the
        // retirement (a retired profile is locked for new records).
        var (recordA, _) = await CreateAndAuthorizeOvertimeAsync(client, authorizer, employeeId,
            OvertimeBody(typeId, justId, requesterId, durationHours: 2, durationMinutes: 30, workDateOffsetDays: -1));
        var (recordB, _) = await CreateAndAuthorizeOvertimeAsync(client, authorizer, employeeId,
            OvertimeBody(typeId, justId, requesterId, durationHours: 1, durationMinutes: 30,
                factorApplied: 2.50m, factorOverrideNote: "Recargo nocturno", workDateOffsetDays: -2));
        var (recordFuture, _) = await CreateAndAuthorizeOvertimeAsync(client, authorizer, employeeId,
            OvertimeBody(typeId, justId, requesterId, durationHours: 1, durationMinutes: 0, workDateOffsetDays: 10));

        await ExecuteRetirementAsync(client, employeeId, requesterId);

        // [1 — CALCULATED] The settlement carries the automatic HORAS_EXTRAS_PENDIENTES_PAGO line: 8.75 factored
        // hours × $1.25/h = $10.94 (golden A.4-16), engine-calculated, INCLUDED.
        var created = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = plazaId, requestDate = DateTime.UtcNow.Date });
        var createdPayload = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.OK, $"Create failed: {(int)created.StatusCode} {createdPayload}");

        Guid settlementId, token, payLineId;
        using (var doc = JsonDocument.Parse(createdPayload))
        {
            var root = doc.RootElement;
            settlementId = root.GetProperty("publicId").GetGuid();
            token = root.GetProperty("concurrencyToken").GetGuid();

            var line = LineByConcept(root, "HORAS_EXTRAS_PENDIENTES_PAGO");
            Assert.Equal(8.75m, line.GetProperty("unitsOrDays").GetDecimal());
            Assert.Equal(1.25m, line.GetProperty("calculationBase").GetDecimal());
            Assert.Equal(10.94m, line.GetProperty("calculatedAmount").GetDecimal());
            Assert.True(line.GetProperty("isIncluded").GetBoolean());
            Assert.True(line.GetProperty("isSystemCalculated").GetBoolean());
            payLineId = line.GetProperty("publicId").GetGuid();
        }

        // [2 — MANUAL BLOCKED] The concept is system-calculated → appending it as a manual line is rejected.
        var manual = await SendSettlementAsync(
            client, HttpMethod.Post, $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/lines",
            token, new { conceptCode = "HORAS_EXTRAS_PENDIENTES_PAGO", description = "Intento manual", amount = 99m });
        await AssertProblemDetailsAsync(manual, HttpStatusCode.UnprocessableEntity, "SETTLEMENT_CONCEPT_INVALID");

        // [3 — EDITABLE] The liquidator edits the factored hours to 8.00 → 8.00 × 1.25 = $10.00 (audited unit override).
        var edited = await SendSettlementAsync(client, HttpMethod.Put,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/lines/{payLineId}",
            token, new { unitsOrDays = 8.00m });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        using (var doc = await ReadJsonAsync(edited))
        {
            token = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
            Assert.Equal(10.00m, LineByConcept(doc.RootElement, "HORAS_EXTRAS_PENDIENTES_PAGO").GetProperty("calculatedAmount").GetDecimal());
        }

        // [4 — ISSUE] Issuing with the line INCLUDED closes the plaza's overtime: the two elapsed records become
        // APLICADA and the FUTURE organized shift is annulled.
        var issued = await SendSettlementAsync(client, HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/issuance",
            token, new { confirmNegativeNet = false });
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        token = await ReadTokenAsync(issued);
        Assert.Equal("APLICADA", await GetOvertimeRecordStatusAsync(recordA));
        Assert.Equal("APLICADA", await GetOvertimeRecordStatusAsync(recordB));
        Assert.Equal("ANULADA", await GetOvertimeRecordStatusAsync(recordFuture));

        // [5 — ANNUL] Annulling the settlement reopens exactly the records it closed → AUTORIZADA again (both the
        // applied ones and the future annulled one).
        var annulled = await SendSettlementAsync(client, HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/annulment",
            token, new { reason = "Corrección de la liquidación" });
        Assert.Equal(HttpStatusCode.OK, annulled.StatusCode);
        Assert.Equal("AUTORIZADA", await GetOvertimeRecordStatusAsync(recordA));
        Assert.Equal("AUTORIZADA", await GetOvertimeRecordStatusAsync(recordB));
        Assert.Equal("AUTORIZADA", await GetOvertimeRecordStatusAsync(recordFuture));
    }

    [Fact]
    public async Task OvertimeSettlement_ExcludedLine_ClosesNoRecord()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var authorizerUserId = Guid.NewGuid();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));
        using var authorizer = factory.CreateClientFor(OvertimeAuthorizerContext(scenario, authorizerUserId));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var (employeeId, plazaId) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Elena", "Excluye", "EMP-OTS-B", "elena.ots.b@empresa.test", monthlySalary: 300m);
        var (requesterId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Rita", "Solicitante", "EMP-OTS-B3", "rita.ots.b@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "DeRrhh", "EMP-OTS-B2", "gestora.ots.b@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        var (recordA, _) = await CreateAndAuthorizeOvertimeAsync(client, authorizer, employeeId,
            OvertimeBody(typeId, justId, requesterId, durationHours: 2, durationMinutes: 30, workDateOffsetDays: -1));
        var (recordFuture, _) = await CreateAndAuthorizeOvertimeAsync(client, authorizer, employeeId,
            OvertimeBody(typeId, justId, requesterId, durationHours: 1, durationMinutes: 0, workDateOffsetDays: 10));

        await ExecuteRetirementAsync(client, employeeId, requesterId);

        var created = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = plazaId, requestDate = DateTime.UtcNow.Date });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        Guid settlementId, token, payLineId;
        using (var doc = await ReadJsonAsync(created))
        {
            settlementId = doc.RootElement.GetProperty("publicId").GetGuid();
            token = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
            payLineId = LineByConcept(doc.RootElement, "HORAS_EXTRAS_PENDIENTES_PAGO").GetProperty("publicId").GetGuid();
        }

        // Exclude the pay-off line.
        var excluded = await SendSettlementAsync(client, HttpMethod.Put,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/lines/{payLineId}",
            token, new { isIncluded = false });
        Assert.Equal(HttpStatusCode.OK, excluded.StatusCode);
        token = await ReadTokenAsync(excluded);

        // Issuing with the line EXCLUDED closes NOTHING: every record stays AUTORIZADA.
        var issued = await SendSettlementAsync(client, HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/issuance",
            token, new { confirmNegativeNet = false });
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        Assert.Equal("AUTORIZADA", await GetOvertimeRecordStatusAsync(recordA));
        Assert.Equal("AUTORIZADA", await GetOvertimeRecordStatusAsync(recordFuture));
    }

    [Fact]
    public async Task OvertimeSettlement_NoRecords_HasNoLine()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        var (employeeId, plazaId) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Sinhoras", "Extra", "EMP-OTS-C", "sinhoras.ots.c@empresa.test", monthlySalary: 300m);
        var (requesterId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Rosa", "Solicitante", "EMP-OTS-C3", "rosa.ots.c@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "DeRrhh", "EMP-OTS-C2", "gestora.ots.c@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        // No overtime records for this employee.
        await ExecuteRetirementAsync(client, employeeId, requesterId);

        var created = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = plazaId, requestDate = DateTime.UtcNow.Date });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var doc = await ReadJsonAsync(created);
        Assert.Null(OvertimePayLineOrNull(doc.RootElement));
    }

    [Fact]
    public async Task OvertimeSettlement_NeverWritesToPayrollTransactionsNorCompensationConcepts()
    {
        var scenario = await factory.ResetDatabaseAsync();
        var authorizerUserId = Guid.NewGuid();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));
        using var authorizer = factory.CreateClientFor(OvertimeAuthorizerContext(scenario, authorizerUserId));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var (employeeId, plazaId) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Sinledger", "Extra", "EMP-OTS-D", "sinledger.ots.d@empresa.test", monthlySalary: 300m);
        var (requesterId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Raquel", "Solicitante", "EMP-OTS-D3", "raquel.ots.d@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "DeRrhh", "EMP-OTS-D2", "gestora.ots.d@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        // Baseline after seeding: 0 payroll transactions, 1 compensation concept (the seeded SALARIO_BASE).
        var baseline = await CountLedgerRowsAsync(employeeId);

        _ = await CreateAndAuthorizeOvertimeAsync(client, authorizer, employeeId,
            OvertimeBody(typeId, justId, requesterId, durationHours: 2, durationMinutes: 30, workDateOffsetDays: -1));

        await ExecuteRetirementAsync(client, employeeId, requesterId);

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

        // The overtime ↔ settlement flow never touches the payroll ledger nor the plaza compensation concepts (RN-20):
        // the row counts are unchanged (the LIQUIDACION journal action the settlement writes is NOT one of these).
        var after = await CountLedgerRowsAsync(employeeId);
        Assert.Equal(baseline.PayrollTransactions, after.PayrollTransactions);
        Assert.Equal(baseline.CompensationConcepts, after.CompensationConcepts);
    }
}
