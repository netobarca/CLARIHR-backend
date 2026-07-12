using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// End-to-end coverage of the recurring-deduction ↔ settlement integration (REQ-008 PR-6, §3.5). A VIGENTE credit
/// with <c>settlementActionCode = DESCONTAR_SALDO</c> on the PRINCIPAL plaza feeds an editable/excludable
/// <c>DESCUENTO_CICLICO_PENDIENTE</c> suggestion (concept seed <c>-9928</c>, <c>IsSystemCalculated=false</c> ⇒ a
/// MANUAL line, not engine-calculated) valued at the outstanding balance — and, crucially, classified as a
/// DEDUCTION, so it REDUCES the net pay instead of increasing it (the engine's ResolveClass switch defaults to
/// Ingreso: without the explicit Descuento arm the settlement would PAY the employee their loan). Issuing the
/// settlement finalizes the employee's VIGENTE credits — those with <c>CANCELAR</c> are written off with NO line
/// (condonación) — and annulling it reopens exactly the ones that settlement closed. With compound interest the
/// suggested balance is the outstanding CAPITAL (paying early does not owe the future interest).
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static JsonElement? RecurringDeductionLineOrNull(JsonElement settlementRoot) =>
        settlementRoot.GetProperty("lines").EnumerateArray()
            .Where(line => line.GetProperty("conceptCode").GetString() == "DESCUENTO_CICLICO_PENDIENTE")
            .Select(line => (JsonElement?)line)
            .FirstOrDefault();

    /// <summary>
    /// Seeds a settlement candidate (profile with minimum wage, open contract, active primary assignment and
    /// SALARIO_BASE) on whose primary plaza a recurring deduction can be registered. Unlike the recurring-income
    /// candidate this needs NO cost center (P-08: a credit carries only the plaza).
    /// </summary>
    private async Task<(Guid FileId, Guid AssignmentId)> SeedRecurringDeductionSettlementCandidateAsync(
        Guid tenantId,
        string firstName,
        string lastName,
        string employeeCode,
        string institutionalEmail)
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

    private static async Task<string> GetRecurringDeductionStatusAsync(HttpClient client, Guid fileId, Guid deductionId)
    {
        var response = await client.GetAsync($"/api/v1/personnel-files/{fileId}/recurring-deductions/{deductionId}");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("statusCode").GetString()!;
    }

    /// <summary>Registers a credit and authorizes it to VIGENTE (a third-party authorizer — double anti-self).</summary>
    private async Task<Guid> CreateAndAuthorizeVigenteDeductionAsync(
        IntegrationTestScenario scenario,
        HttpClient manager,
        Guid fileId,
        object body)
    {
        var (deductionId, _) = await CreateAndAuthorizeDeductionAsync(scenario, manager, fileId, body);
        return deductionId;
    }

    [Fact]
    public async Task RecurringDeductionSettlement_DescontarSaldo_ReducesTheNetFinalizesOnIssueAndReopensOnAnnul()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        var (employeeId, plazaId) = await SeedRecurringDeductionSettlementCandidateAsync(
            scenario.TenantId, "Dolores", "Deudora", "EMP-RDL-A", "dolores.rdl.a@empresa.test");
        var (requesterId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Raúl", "Solicitante", "EMP-RDL-A3", "raul.rdl.a@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "DeRrhh", "EMP-RDL-A2", "gestora.rdl.a@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        // A $300 credit (6 × $50) with DESCONTAR_SALDO, authorized BEFORE the retirement (a retired profile is
        // locked). Nothing charged → the whole $300 is owed.
        var body = SegmentedRecurringDeductionBody(
            segments: [new { fromInstallment = 1, toInstallment = (int?)6, installmentValue = 50m }]);
        var deductionId = await CreateAndAuthorizeVigenteDeductionAsync(scenario, client, employeeId, body);

        await ExecuteRetirementAsync(client, employeeId, requesterId);

        var created = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = plazaId, requestDate = DateTime.UtcNow.Date });
        var createdPayload = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.OK, $"Create failed: {(int)created.StatusCode} {createdPayload}");

        Guid settlementId, token, deductionLineId;
        decimal netWithLine, deductionsWithLine;
        using (var doc = JsonDocument.Parse(createdPayload))
        {
            var root = doc.RootElement;
            settlementId = root.GetProperty("publicId").GetGuid();
            token = root.GetProperty("concurrencyToken").GetGuid();

            var line = LineByConcept(root, "DESCUENTO_CICLICO_PENDIENTE");

            // [1 — CLASSIFIED AS A DEDUCTION] This is the load-bearing assertion of the whole PR: the engine's
            // ResolveClass switch defaults to Ingreso, so if the concept were not in its Descuento arm the balance
            // would be PAID to the employee instead of discounted.
            Assert.Equal("Descuento", line.GetProperty("conceptClass").GetString());

            // [2 — SUGGESTED, EDITABLE] Valued at the outstanding balance ($300), included, and MANUAL (seed -9928
            // has IsSystemCalculated=false → the liquidator can edit or exclude it).
            Assert.Equal(300m, line.GetProperty("calculatedAmount").GetDecimal());
            Assert.Equal(300m, line.GetProperty("finalAmount").GetDecimal());
            Assert.True(line.GetProperty("isIncluded").GetBoolean());
            Assert.False(line.GetProperty("isSystemCalculated").GetBoolean());

            // The creditor travels with the line so the liquidator knows whom the balance is owed to.
            Assert.Equal("Banco Agrícola", line.GetProperty("counterpartyName").GetString());

            deductionLineId = line.GetProperty("publicId").GetGuid();
            netWithLine = root.GetProperty("netPay").GetDecimal();
            deductionsWithLine = root.GetProperty("totalDeductions").GetDecimal();
        }

        // [3 — IT COUNTS AS A DEDUCTION] The $300 balance is inside the settlement's DEDUCTIONS total (had the
        // engine classified it as income, it would have landed on the income side and been paid instead).
        Assert.True(
            deductionsWithLine >= 300m,
            $"The credit balance must be part of the deductions: totalDeductions = {deductionsWithLine}.");

        // [4 — IT REDUCES THE NET / EXCLUDABLE] Excluding the line gives the employee back exactly the $300 —
        // which only holds if the line was SUBTRACTED from the net in the first place.
        var excluded = await SendSettlementAsync(client, HttpMethod.Put,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/lines/{deductionLineId}",
            token, new { isIncluded = false });
        Assert.Equal(HttpStatusCode.OK, excluded.StatusCode);
        using (var doc = await ReadJsonAsync(excluded))
        {
            token = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
            Assert.Equal(netWithLine + 300m, doc.RootElement.GetProperty("netPay").GetDecimal());
        }

        // Put it back so the issue path exercises the real case.
        var included = await SendSettlementAsync(client, HttpMethod.Put,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/lines/{deductionLineId}",
            token, new { isIncluded = true });
        Assert.Equal(HttpStatusCode.OK, included.StatusCode);
        token = await ReadTokenAsync(included);

        // [5 — ISSUE] Issuing ends the employee's VIGENTE credits (the balance was discounted here).
        var issued = await SendSettlementAsync(client, HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/issuance",
            token, new { confirmNegativeNet = false });
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        token = await ReadTokenAsync(issued);
        Assert.Equal("FINALIZADO", await GetRecurringDeductionStatusAsync(client, employeeId, deductionId));

        // [6 — ANNUL] Annulling reopens exactly the credits this settlement closed → VIGENTE, balance intact.
        var annulled = await SendSettlementAsync(client, HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/annulment",
            token, new { reason = "Corrección de la liquidación" });
        Assert.Equal(HttpStatusCode.OK, annulled.StatusCode);
        Assert.Equal("VIGENTE", await GetRecurringDeductionStatusAsync(client, employeeId, deductionId));
    }

    [Fact]
    public async Task RecurringDeductionSettlement_Cancelar_IsWrittenOffWithNoLine()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        var (employeeId, plazaId) = await SeedRecurringDeductionSettlementCandidateAsync(
            scenario.TenantId, "Carmen", "Condonada", "EMP-RDL-B", "carmen.rdl.b@empresa.test");
        var (requesterId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Rita", "Solicitante", "EMP-RDL-B3", "rita.rdl.b@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "DeRrhh", "EMP-RDL-B2", "gestora.rdl.b@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        // Same $300 credit, but the company writes it off on settlement (CANCELAR = condonación).
        var body = SegmentedRecurringDeductionBody(
            settlementActionCode: "CANCELAR",
            segments: [new { fromInstallment = 1, toInstallment = (int?)6, installmentValue = 50m }]);
        var deductionId = await CreateAndAuthorizeVigenteDeductionAsync(scenario, client, employeeId, body);

        await ExecuteRetirementAsync(client, employeeId, requesterId);

        var created = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = plazaId, requestDate = DateTime.UtcNow.Date });
        var createdPayload = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.OK, $"Create failed: {(int)created.StatusCode} {createdPayload}");

        Guid settlementId, token;
        using (var doc = JsonDocument.Parse(createdPayload))
        {
            // [1 — NO LINE] A written-off credit is NOT discounted: the employee keeps the money.
            Assert.Null(RecurringDeductionLineOrNull(doc.RootElement));
            settlementId = doc.RootElement.GetProperty("publicId").GetGuid();
            token = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
        }

        // [2 — ISSUE] It still ends on issue: every VIGENTE credit closes when the employee is settled.
        var issued = await SendSettlementAsync(client, HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/issuance",
            token, new { confirmNegativeNet = false });
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        Assert.Equal("FINALIZADO", await GetRecurringDeductionStatusAsync(client, employeeId, deductionId));
    }

    [Fact]
    public async Task RecurringDeductionSettlement_WithCompoundInterest_SuggestsTheOutstandingCapitalNotTheRemainingQuotas()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        var (employeeId, plazaId) = await SeedRecurringDeductionSettlementCandidateAsync(
            scenario.TenantId, "Ismael", "Interés", "EMP-RDL-C", "ismael.rdl.c@empresa.test");
        var (requesterId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Rosa", "Solicitante", "EMP-RDL-C3", "rosa.rdl.c@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "DeRrhh", "EMP-RDL-C2", "gestora.rdl.c@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        // The golden credit: $1,000 at 12% nominal annual over 12 monthly quotas. The employee would pay ~$1,066
        // by finishing the plan, but the PAYOFF is the capital still owed — $1,000 with nothing charged yet.
        var deductionId = await CreateAndAuthorizeVigenteDeductionAsync(
            scenario, client, employeeId, InterestRecurringDeductionBody());

        await ExecuteRetirementAsync(client, employeeId, requesterId);

        var created = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = plazaId, requestDate = DateTime.UtcNow.Date });
        var createdPayload = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.OK, $"Create failed: {(int)created.StatusCode} {createdPayload}");

        using var doc = JsonDocument.Parse(createdPayload);
        var line = LineByConcept(doc.RootElement, "DESCUENTO_CICLICO_PENDIENTE");

        // Paying a credit off early does NOT owe the future interest: the balance is the CAPITAL, not Σ quotas.
        Assert.Equal("Descuento", line.GetProperty("conceptClass").GetString());
        Assert.Equal(1000m, line.GetProperty("calculatedAmount").GetDecimal());
        Assert.Equal("Banco Cuscatlán", line.GetProperty("counterpartyName").GetString());

        _ = deductionId;
    }

    [Fact]
    public async Task RecurringDeductionSettlement_WithoutABalance_AddsNoLine_AndWritesNoLedgerRows()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        var (employeeId, plazaId) = await SeedRecurringDeductionSettlementCandidateAsync(
            scenario.TenantId, "Sonia", "SinDeuda", "EMP-RDL-D", "sonia.rdl.d@empresa.test");
        var (requesterId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Raquel", "Solicitante", "EMP-RDL-D3", "raquel.rdl.d@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "DeRrhh", "EMP-RDL-D2", "gestora.rdl.d@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        // No credit at all: the settlement must be exactly what it was before REQ-008 (retrocompatibility).
        var before = await CountLedgerRowsAsync(employeeId);

        await ExecuteRetirementAsync(client, employeeId, requesterId);

        var created = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = plazaId, requestDate = DateTime.UtcNow.Date });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        Guid settlementId, token;
        using (var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync()))
        {
            Assert.Null(RecurringDeductionLineOrNull(doc.RootElement));
            settlementId = doc.RootElement.GetProperty("publicId").GetGuid();
            token = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
        }

        var issued = await SendSettlementAsync(client, HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/issuance",
            token, new { confirmNegativeNet = false });
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);

        // RN-14 / RN-16: the whole flow never writes to the payroll-transaction ledger nor to the plaza's
        // compensation concepts — a credit is a credit, not a structural compensation item.
        var after = await CountLedgerRowsAsync(employeeId);
        Assert.Equal(before.PayrollTransactions, after.PayrollTransactions);
        Assert.Equal(before.CompensationConcepts, after.CompensationConcepts);
    }
}
