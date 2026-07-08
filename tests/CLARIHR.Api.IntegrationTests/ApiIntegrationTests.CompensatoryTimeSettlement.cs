using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.Leave;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// End-to-end coverage of the compensatory-time ↔ settlement integration (REQ-002 PR-6, RF-013/D-19): the
/// settlement engine emits an automatic <c>HORAS_EXTRAS_PENDIENTES</c> pay-off line valued from the employee's
/// pending compensatory-time balance (saldo 12 h × salario diario 20 ÷ 8 h × factor 1.00 = $30.00 — golden
/// A.4-10). The liquidator's edited hours survive a recalculation and a regenerate re-reads the fund; a settlement
/// with no fund carries no line (retrocompatible); on a multi-plaza retirement only the PRINCIPAL plaza's
/// settlement carries the line; and — since the seed is now <c>IsSystemCalculated=true</c> — appending the
/// concept as a MANUAL line is rejected (422 <c>SETTLEMENT_CONCEPT_INVALID</c>). Reuses the settlement, retirement
/// and compensatory-time helpers of the sibling partials.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static JsonElement? CompensatoryLineOrNull(JsonElement settlementRoot) =>
        settlementRoot.GetProperty("lines").EnumerateArray()
            .Where(line => line.GetProperty("conceptCode").GetString() == "HORAS_EXTRAS_PENDIENTES")
            .Select(line => (JsonElement?)line)
            .FirstOrDefault();

    /// <summary>Adds a second (non-primary) active plaza with its own base salary so the retirement closes two plazas.</summary>
    private async Task<Guid> AddSecondaryPlazaAsync(Guid tenantId, Guid fileId, decimal monthlySalary = 600m)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var internalId = await dbContext.Set<PersonnelFile>()
            .IgnoreQueryFilters()
            .Where(item => item.PublicId == fileId)
            .Select(item => item.Id)
            .FirstAsync();

        var assignment = PersonnelFileEmploymentAssignment.Create(
            "INDEFINIDO",
            contractTypeCode: null,
            workdayCode: null,
            payrollTypeCode: null,
            positionSlotPublicId: null,
            orgUnitPublicId: null,
            workCenterPublicId: null,
            costCenterPublicId: null,
            startDate: new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            endDate: null,
            isPrimary: false,
            isActive: true,
            notes: null);
        assignment.BindToPersonnelFile(internalId);
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
            startDate: new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            endDate: null,
            isActive: true,
            isSystemSuggested: false,
            notes: null);
        salary.BindToPersonnelFile(internalId);
        salary.SetTenantId(tenantId);
        dbContext.Set<PersonnelFileCompensationConcept>().Add(salary);
        await dbContext.SaveChangesAsync();

        return assignment.PublicId;
    }

    [Fact]
    public async Task Settlement_CompensatoryTime_AutomaticLine_IsEditableRegeneratesAndBlocksManual()
    {
        var scenario = await factory.ResetDatabaseAsync();
        await SetCompensatoryTimeCreditRequiresDocumentAsync(scenario.TenantId, requires: false);
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        var (employeeId, plazaId) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Sofía", "Compensada", "EMP-CTL-A", "sofia.ctl.a@empresa.test");
        var (requesterId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Raúl", "Solicitante", "EMP-CTL-A3", "raul.ctl.a@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "DeRrhh", "EMP-CTL-A2", "gestora.ctl.a@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        var (typeId, _) = await SeedCompensatoryTimeTypeAsync(
            scenario, "FLEX", "Tiempo compensatorio", CompensatoryTimeOperations.Both, 1.00m);

        // A 12h credit BEFORE retirement (a retired profile is locked) → fund balance = 12h.
        _ = await CreateCreditNoDocAsync(client, employeeId, typeId, "2026-03-04", 12m);

        await ExecuteRetirementAsync(client, employeeId, requesterId);

        // [1 — WITH FUND] The settlement carries the automatic HORAS_EXTRAS_PENDIENTES line valued at
        // 12 h × (600/30 ÷ 8 = 2.50/h) × factor 1.00 = $30.00 (golden A.4-10).
        var created = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = plazaId, requestDate = DateTime.UtcNow.Date });
        var createdPayload = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.OK, $"Create failed: {(int)created.StatusCode} {createdPayload}");

        Guid settlementId, token, comptimeLineId, salarioLineId;
        using (var doc = JsonDocument.Parse(createdPayload))
        {
            var root = doc.RootElement;
            settlementId = root.GetProperty("publicId").GetGuid();
            token = root.GetProperty("concurrencyToken").GetGuid();

            var line = LineByConcept(root, "HORAS_EXTRAS_PENDIENTES");
            Assert.Equal(12m, line.GetProperty("unitsOrDays").GetDecimal());
            Assert.Equal(2.50m, line.GetProperty("calculationBase").GetDecimal());
            Assert.Equal(30.00m, line.GetProperty("calculatedAmount").GetDecimal());
            Assert.True(line.GetProperty("isSystemCalculated").GetBoolean());
            comptimeLineId = line.GetProperty("publicId").GetGuid();
            salarioLineId = LineByConcept(root, "SALARIO").GetProperty("publicId").GetGuid();
        }

        // [2 — MANUAL BLOCKED] The concept is now system-calculated → appending it as a manual line is rejected.
        var manual = await SendSettlementAsync(
            client, HttpMethod.Post, $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/lines",
            token, new { conceptCode = "HORAS_EXTRAS_PENDIENTES", description = "Intento manual", amount = 99m });
        await AssertProblemDetailsAsync(manual, HttpStatusCode.UnprocessableEntity, "SETTLEMENT_CONCEPT_INVALID");

        // [3 — EDITABLE] The liquidator edits the hours to 10 → 10 × 2.50 = $25.00 (audited unit override).
        var edited = await SendSettlementAsync(client, HttpMethod.Put,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/lines/{comptimeLineId}",
            token, new { unitsOrDays = 10m });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        using (var doc = await ReadJsonAsync(edited))
        {
            token = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
            var line = LineByConcept(doc.RootElement, "HORAS_EXTRAS_PENDIENTES");
            Assert.Equal(10m, line.GetProperty("unitsOrDays").GetDecimal());
            Assert.Equal(25.00m, line.GetProperty("calculatedAmount").GetDecimal());
        }

        // The edited hours survive a subsequent recalculation (toggling another line reruns the engine).
        var recalculated = await SendSettlementAsync(client, HttpMethod.Put,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/lines/{salarioLineId}",
            token, new { isIncluded = true });
        Assert.Equal(HttpStatusCode.OK, recalculated.StatusCode);
        using (var doc = await ReadJsonAsync(recalculated))
        {
            token = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
            Assert.Equal(10m, LineByConcept(doc.RootElement, "HORAS_EXTRAS_PENDIENTES").GetProperty("unitsOrDays").GetDecimal());
        }

        // [4 — REGENERATE] Regenerating discards adjustments and re-reads the fund → 12 h / $30.00 again.
        var regenerated = await SendSettlementAsync(client, HttpMethod.Post,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/lines/regenerate",
            token, body: null);
        Assert.Equal(HttpStatusCode.OK, regenerated.StatusCode);
        using (var doc = await ReadJsonAsync(regenerated))
        {
            var line = LineByConcept(doc.RootElement, "HORAS_EXTRAS_PENDIENTES");
            Assert.Equal(12m, line.GetProperty("unitsOrDays").GetDecimal());
            Assert.Equal(30.00m, line.GetProperty("calculatedAmount").GetDecimal());
        }
    }

    [Fact]
    public async Task Settlement_CompensatoryTime_NoBalance_HasNoLine()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        var (employeeId, plazaId) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Tomás", "SinSaldo", "EMP-CTL-B", "tomas.ctl.b@empresa.test");
        var (requesterId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Rita", "Solicitante", "EMP-CTL-B3", "rita.ctl.b@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "DeRrhh", "EMP-CTL-B2", "gestora.ctl.b@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        // No compensatory-time fund for this employee.
        await ExecuteRetirementAsync(client, employeeId, requesterId);

        var created = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = plazaId, requestDate = DateTime.UtcNow.Date });
        var createdPayload = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.OK, $"Create failed: {(int)created.StatusCode} {createdPayload}");

        using var doc = JsonDocument.Parse(createdPayload);
        Assert.Null(CompensatoryLineOrNull(doc.RootElement));
    }

    [Fact]
    public async Task Settlement_CompensatoryTime_MultiPlaza_LineOnlyOnPrincipalPlaza()
    {
        var scenario = await factory.ResetDatabaseAsync();
        await SetCompensatoryTimeCreditRequiresDocumentAsync(scenario.TenantId, requires: false);
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        var (employeeId, principalPlazaId) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Úrsula", "DosPlazas", "EMP-CTL-C", "ursula.ctl.c@empresa.test");
        var secondaryPlazaId = await AddSecondaryPlazaAsync(scenario.TenantId, employeeId);
        var (requesterId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Rodrigo", "Solicitante", "EMP-CTL-C3", "rodrigo.ctl.c@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "DeRrhh", "EMP-CTL-C2", "gestora.ctl.c@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        var (typeId, _) = await SeedCompensatoryTimeTypeAsync(
            scenario, "FLEX", "Tiempo compensatorio", CompensatoryTimeOperations.Both, 1.00m);
        _ = await CreateCreditNoDocAsync(client, employeeId, typeId, "2026-03-04", 12m);

        await ExecuteRetirementAsync(client, employeeId, requesterId);

        // The SECONDARY plaza settlement carries NO compensatory line (the fund is resolved only for the principal).
        var secondary = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = secondaryPlazaId, requestDate = DateTime.UtcNow.Date });
        var secondaryPayload = await secondary.Content.ReadAsStringAsync();
        Assert.True(secondary.StatusCode == HttpStatusCode.OK, $"Secondary create failed: {(int)secondary.StatusCode} {secondaryPayload}");
        using (var doc = JsonDocument.Parse(secondaryPayload))
        {
            Assert.Null(CompensatoryLineOrNull(doc.RootElement));
        }

        // The PRINCIPAL plaza settlement DOES carry the line (12 h → $30.00).
        var principal = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = principalPlazaId, requestDate = DateTime.UtcNow.Date });
        var principalPayload = await principal.Content.ReadAsStringAsync();
        Assert.True(principal.StatusCode == HttpStatusCode.OK, $"Principal create failed: {(int)principal.StatusCode} {principalPayload}");
        using (var doc = JsonDocument.Parse(principalPayload))
        {
            var line = CompensatoryLineOrNull(doc.RootElement);
            Assert.NotNull(line);
            Assert.Equal(12m, line!.Value.GetProperty("unitsOrDays").GetDecimal());
            Assert.Equal(30.00m, line.Value.GetProperty("calculatedAmount").GetDecimal());
        }
    }
}
