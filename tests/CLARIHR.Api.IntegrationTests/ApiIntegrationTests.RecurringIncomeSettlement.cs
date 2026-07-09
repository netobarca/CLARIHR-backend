using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.CostCenters;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// End-to-end coverage of the recurring-income ↔ settlement integration (REQ-005 PR-6, §3.5). A VIGENTE cyclic
/// income with <c>settlementActionCode = PAGAR_SALDO</c> on the PRINCIPAL plaza feeds an editable/excludable
/// <c>INGRESO_CICLICO_PENDIENTE</c> suggestion (concept seed <c>-9888</c>, <c>IsSystemCalculated=false</c> ⇒ a
/// MANUAL line, not engine-calculated) valued at the plan balance (total − Σ applied installments); issuing the
/// settlement finalizes the employee's VIGENTE cyclic incomes (a settled employee's incomes end) and annulling it
/// reopens exactly the ones that settlement closed. A <c>CANCELAR</c> income carries NO line yet still ends on
/// issue; on a multi-plaza retirement only the principal plaza's settlement carries the line; and — clarification
/// №12 / RN-14 / RN-16 — the whole flow NEVER writes to <c>PersonnelFilePayrollTransaction</c> nor
/// <c>PersonnelFileCompensationConcept</c>. Reuses the settlement, retirement and recurring-income helpers of the
/// sibling partials (<see cref="SeedSettlementCandidateAsync"/>, <see cref="ExecuteRetirementAsync"/>,
/// <see cref="AddSecondaryPlazaAsync"/>, <see cref="CreateAndAuthorizeVigenteIncomeAsync"/>).
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static JsonElement? RecurringIncomeLineOrNull(JsonElement settlementRoot) =>
        settlementRoot.GetProperty("lines").EnumerateArray()
            .Where(line => line.GetProperty("conceptCode").GetString() == "INGRESO_CICLICO_PENDIENTE")
            .Select(line => (JsonElement?)line)
            .FirstOrDefault();

    /// <summary>
    /// Seeds a settlement candidate (profile with minimum wage, open contract, active primary assignment and
    /// SALARIO_BASE) whose PRIMARY plaza also carries a cost center, so a recurring income can be registered on it
    /// (P-15: the cost center is derived from the plaza and a plaza without one is rejected 422).
    /// </summary>
    private async Task<(Guid FileId, Guid AssignmentId)> SeedRecurringIncomeSettlementCandidateAsync(
        Guid tenantId,
        string firstName,
        string lastName,
        string employeeCode,
        string institutionalEmail)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var costCenterType = CostCenterType.Create($"CCT-{employeeCode}", $"Tipo {employeeCode}", null);
        costCenterType.SetTenantId(tenantId);
        dbContext.Set<CostCenterType>().Add(costCenterType);
        await dbContext.SaveChangesAsync();

        var costCenter = CostCenter.Create($"CC-{employeeCode}", $"Centro de costo {employeeCode}", costCenterType.Id, null, null, null, null);
        costCenter.SetTenantId(tenantId);
        dbContext.Set<CostCenter>().Add(costCenter);
        await dbContext.SaveChangesAsync();

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
        file.CompleteWithoutLinkedUser();

        dbContext.Set<PersonnelFile>().Add(file);
        await dbContext.SaveChangesAsync();

        var profile = PersonnelFileEmployeeProfile.Create(employeeCode, "ACTIVO", RetirementHireDate, 365m);
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
            costCenterPublicId: costCenter.PublicId,
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
            600m,
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

    private static async Task<string> GetRecurringIncomeStatusAsync(HttpClient client, Guid fileId, Guid incomeId)
    {
        var response = await client.GetAsync($"/api/v1/personnel-files/{fileId}/recurring-incomes/{incomeId}");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("statusCode").GetString()!;
    }

    private async Task<(long PayrollTransactions, long CompensationConcepts)> CountLedgerRowsAsync(Guid fileId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var internalId = await dbContext.Set<PersonnelFile>()
            .IgnoreQueryFilters()
            .Where(item => item.PublicId == fileId)
            .Select(item => item.Id)
            .FirstAsync();

        var payrollTransactions = await dbContext.Set<PersonnelFilePayrollTransaction>()
            .IgnoreQueryFilters()
            .LongCountAsync(item => item.PersonnelFileId == internalId);
        var compensationConcepts = await dbContext.Set<PersonnelFileCompensationConcept>()
            .IgnoreQueryFilters()
            .LongCountAsync(item => item.PersonnelFileId == internalId);
        return (payrollTransactions, compensationConcepts);
    }

    [Fact]
    public async Task RecurringIncomeSettlement_PagarSaldo_SuggestsLineFinalizesOnIssueAndReopensOnAnnul()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));
        using var authorizer = factory.CreateClientFor(RecurringIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var (employeeId, plazaId) = await SeedRecurringIncomeSettlementCandidateAsync(
            scenario.TenantId, "Cira", "Ciclica", "EMP-RIL-A", "cira.ril.a@empresa.test");
        var (requesterId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Rafael", "Solicitante", "EMP-RIL-A3", "rafael.ril.a@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "DeRrhh", "EMP-RIL-A2", "gestora.ril.a@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        // A finite $300 (6 × $50) PAGAR_SALDO income, authorized to VIGENTE BEFORE the retirement (a retired
        // profile is locked). Nothing is applied → the plan balance is the full $300.
        var (incomeId, _) = await CreateAndAuthorizeVigenteIncomeAsync(
            client, authorizer, employeeId,
            FiniteRecurringIncomeBody(installmentValue: 50, installmentCount: 6, settlementActionCode: "PAGAR_SALDO"));

        await ExecuteRetirementAsync(client, employeeId, requesterId);

        // [1 — SUGGESTED] The settlement of the principal plaza carries the INGRESO_CICLICO_PENDIENTE line at the
        // plan balance ($300), INCLUDED and EDITABLE (a MANUAL line — isSystemCalculated=false, seed -9888).
        var created = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = plazaId, requestDate = DateTime.UtcNow.Date });
        var createdPayload = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.OK, $"Create failed: {(int)created.StatusCode} {createdPayload}");

        Guid settlementId, token, cyclicLineId;
        using (var doc = JsonDocument.Parse(createdPayload))
        {
            var root = doc.RootElement;
            settlementId = root.GetProperty("publicId").GetGuid();
            token = root.GetProperty("concurrencyToken").GetGuid();

            var line = LineByConcept(root, "INGRESO_CICLICO_PENDIENTE");
            Assert.Equal(300m, line.GetProperty("calculatedAmount").GetDecimal());
            Assert.Equal(300m, line.GetProperty("finalAmount").GetDecimal());
            Assert.True(line.GetProperty("isIncluded").GetBoolean());
            Assert.False(line.GetProperty("isSystemCalculated").GetBoolean());
            cyclicLineId = line.GetProperty("publicId").GetGuid();
        }

        // [2 — EDITABLE] The liquidator can edit the manual amount (proves the line is editable) → $250.
        var edited = await SendSettlementAsync(client, HttpMethod.Put,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/lines/{cyclicLineId}",
            token, new { manualAmount = 250m });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        using (var doc = await ReadJsonAsync(edited))
        {
            token = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
            Assert.Equal(250m, LineByConcept(doc.RootElement, "INGRESO_CICLICO_PENDIENTE").GetProperty("finalAmount").GetDecimal());
        }

        // [3 — ISSUE] Issuing finalizes the employee's VIGENTE cyclic incomes (§0.11: a settled employee's
        // incomes end).
        var issued = await SendSettlementAsync(client, HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/issuance",
            token, new { confirmNegativeNet = false });
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        token = await ReadTokenAsync(issued);
        Assert.Equal("FINALIZADO", await GetRecurringIncomeStatusAsync(client, employeeId, incomeId));

        // [4 — ANNUL] Annulling the settlement reopens exactly the incomes it finalized → VIGENTE again.
        var annulled = await SendSettlementAsync(client, HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/annulment",
            token, new { reason = "Corrección de la liquidación" });
        Assert.Equal(HttpStatusCode.OK, annulled.StatusCode);
        Assert.Equal("VIGENTE", await GetRecurringIncomeStatusAsync(client, employeeId, incomeId));
    }

    [Fact]
    public async Task RecurringIncomeSettlement_Cancelar_HasNoLineButStillFinalizesOnIssue()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));
        using var authorizer = factory.CreateClientFor(RecurringIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var (employeeId, plazaId) = await SeedRecurringIncomeSettlementCandidateAsync(
            scenario.TenantId, "Nadia", "Cancelada", "EMP-RIL-B", "nadia.ril.b@empresa.test");
        var (requesterId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Rita", "Solicitante", "EMP-RIL-B3", "rita.ril.b@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "DeRrhh", "EMP-RIL-B2", "gestora.ril.b@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        // A VIGENTE income whose settlement action is CANCELAR (no pay-off on retirement).
        var (incomeId, _) = await CreateAndAuthorizeVigenteIncomeAsync(
            client, authorizer, employeeId,
            FiniteRecurringIncomeBody(installmentValue: 50, installmentCount: 6, settlementActionCode: "CANCELAR"));

        await ExecuteRetirementAsync(client, employeeId, requesterId);

        // [1 — NO LINE] The settlement carries NO INGRESO_CICLICO_PENDIENTE line (only PAGAR_SALDO is suggested).
        var created = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = plazaId, requestDate = DateTime.UtcNow.Date });
        var createdPayload = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.OK, $"Create failed: {(int)created.StatusCode} {createdPayload}");

        Guid settlementId, token;
        using (var doc = JsonDocument.Parse(createdPayload))
        {
            Assert.Null(RecurringIncomeLineOrNull(doc.RootElement));
            settlementId = doc.RootElement.GetProperty("publicId").GetGuid();
            token = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
        }

        // [2 — ISSUE] The CANCELAR income still ends on issue (all VIGENTE incomes close when the employee is settled).
        var issued = await SendSettlementAsync(client, HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/issuance",
            token, new { confirmNegativeNet = false });
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        Assert.Equal("FINALIZADO", await GetRecurringIncomeStatusAsync(client, employeeId, incomeId));
    }

    [Fact]
    public async Task RecurringIncomeSettlement_MultiPlaza_LineOnlyOnPrincipalPlaza()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));
        using var authorizer = factory.CreateClientFor(RecurringIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var (employeeId, principalPlazaId) = await SeedRecurringIncomeSettlementCandidateAsync(
            scenario.TenantId, "Ursula", "DosPlazas", "EMP-RIL-C", "ursula.ril.c@empresa.test");
        var secondaryPlazaId = await AddSecondaryPlazaAsync(scenario.TenantId, employeeId);
        var (requesterId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Rodrigo", "Solicitante", "EMP-RIL-C3", "rodrigo.ril.c@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "DeRrhh", "EMP-RIL-C2", "gestora.ril.c@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        // The recurring income lives on the PRINCIPAL plaza (default), PAGAR_SALDO → balance $300.
        _ = await CreateAndAuthorizeVigenteIncomeAsync(
            client, authorizer, employeeId,
            FiniteRecurringIncomeBody(installmentValue: 50, installmentCount: 6, settlementActionCode: "PAGAR_SALDO"));

        await ExecuteRetirementAsync(client, employeeId, requesterId);

        // The SECONDARY plaza settlement carries NO line (the per-employee balance is resolved only for the
        // principal plaza — a per-employee fund vs per-plaza settlement must not double-suggest).
        var secondary = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = secondaryPlazaId, requestDate = DateTime.UtcNow.Date });
        var secondaryPayload = await secondary.Content.ReadAsStringAsync();
        Assert.True(secondary.StatusCode == HttpStatusCode.OK, $"Secondary create failed: {(int)secondary.StatusCode} {secondaryPayload}");
        using (var doc = JsonDocument.Parse(secondaryPayload))
        {
            Assert.Null(RecurringIncomeLineOrNull(doc.RootElement));
        }

        // The PRINCIPAL plaza settlement DOES carry the line ($300).
        var principal = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = principalPlazaId, requestDate = DateTime.UtcNow.Date });
        var principalPayload = await principal.Content.ReadAsStringAsync();
        Assert.True(principal.StatusCode == HttpStatusCode.OK, $"Principal create failed: {(int)principal.StatusCode} {principalPayload}");
        using (var doc = JsonDocument.Parse(principalPayload))
        {
            var line = RecurringIncomeLineOrNull(doc.RootElement);
            Assert.NotNull(line);
            Assert.Equal(300m, line!.Value.GetProperty("finalAmount").GetDecimal());
            Assert.False(line.Value.GetProperty("isSystemCalculated").GetBoolean());
        }
    }

    [Fact]
    public async Task RecurringIncomeSettlement_NeverWritesToPayrollTransactionsNorCompensationConcepts()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));
        using var authorizer = factory.CreateClientFor(RecurringIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var (employeeId, plazaId) = await SeedRecurringIncomeSettlementCandidateAsync(
            scenario.TenantId, "Bruno", "SinLedger", "EMP-RIL-D", "bruno.ril.d@empresa.test");
        var (requesterId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Raquel", "Solicitante", "EMP-RIL-D3", "raquel.ril.d@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "DeRrhh", "EMP-RIL-D2", "gestora.ril.d@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        // Baseline after seeding: 0 payroll transactions, 1 compensation concept (the seeded SALARIO_BASE).
        var baseline = await CountLedgerRowsAsync(employeeId);

        // Full flow: create → authorize → apply a cuota → retire → create + issue a settlement.
        var (incomeId, token) = await CreateAndAuthorizeVigenteIncomeAsync(
            client, authorizer, employeeId,
            FiniteRecurringIncomeBody(installmentValue: 50, installmentCount: 6, settlementActionCode: "PAGAR_SALDO"));
        var applied = await ApplyNextInstallmentAsync(client, employeeId, incomeId, token);
        Assert.Equal(1, applied.Number);

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
            // After one applied installment the balance is $250 ($300 − $50).
            Assert.Equal(250m, LineByConcept(doc.RootElement, "INGRESO_CICLICO_PENDIENTE").GetProperty("finalAmount").GetDecimal());
        }

        var issued = await SendSettlementAsync(client, HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/issuance",
            settlementToken, new { confirmNegativeNet = false });
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);

        // The whole module (recurring income + installment + settlement) never touches the payroll ledger nor the
        // plaza compensation concepts (RN-14 / RN-16): the row counts are unchanged.
        var after = await CountLedgerRowsAsync(employeeId);
        Assert.Equal(baseline.PayrollTransactions, after.PayrollTransactions);
        Assert.Equal(baseline.CompensationConcepts, after.CompensationConcepts);
    }
}
