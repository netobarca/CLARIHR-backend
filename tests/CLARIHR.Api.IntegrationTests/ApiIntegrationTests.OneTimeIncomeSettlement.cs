using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.CostCenters;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// End-to-end coverage of the one-time-income ↔ settlement integration (REQ-006 PR-6, §3.5). An AUTORIZADO one-off
/// income on the PRINCIPAL plaza feeds an editable/excludable <c>INGRESO_EVENTUAL_PENDIENTE</c> suggestion (concept
/// seed <c>-9905</c>, <c>IsSystemCalculated=false</c> ⇒ a MANUAL line, not engine-calculated) valued at the income
/// amount; issuing the settlement marks the incomes APLICADO — but ONLY those whose line stayed INCLUDED (unlike the
/// cyclic hook, an EXCLUDED line leaves the income AUTORIZADO), and annulling reopens exactly the ones that settlement
/// applied. On a multi-plaza retirement only the principal plaza's settlement carries the line; and — RN-14 / RN-16 —
/// the whole flow NEVER writes to <c>PersonnelFilePayrollTransaction</c> nor <c>PersonnelFileCompensationConcept</c>.
/// Reuses the settlement, retirement, one-time-income and ledger helpers of the sibling partials
/// (<see cref="SeedSettlementCandidateAsync"/>, <see cref="ExecuteRetirementAsync"/>, <see cref="AddSecondaryPlazaAsync"/>,
/// <see cref="CreateAndAuthorizeOneTimeIncomeAsync"/>, <see cref="CountLedgerRowsAsync"/>).
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static JsonElement? OneTimeIncomeLineOrNull(JsonElement settlementRoot) =>
        settlementRoot.GetProperty("lines").EnumerateArray()
            .Where(line => line.GetProperty("conceptCode").GetString() == "INGRESO_EVENTUAL_PENDIENTE")
            .Select(line => (JsonElement?)line)
            .FirstOrDefault();

    private static async Task<string> GetOneTimeIncomeStatusAsync(HttpClient client, Guid fileId, Guid incomeId)
    {
        var response = await client.GetAsync($"/api/v1/personnel-files/{fileId}/one-time-incomes/{incomeId}");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("statusCode").GetString()!;
    }

    /// <summary>
    /// Seeds a settlement candidate (profile with minimum wage, open contract, active primary assignment and
    /// SALARIO_BASE) whose PRIMARY plaza also carries a cost center, so a one-time income can be registered on it
    /// (P-15: the cost center is derived from the plaza and a plaza without one is rejected 422).
    /// </summary>
    private async Task<(Guid FileId, Guid AssignmentId)> SeedOneTimeIncomeSettlementCandidateAsync(
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

    [Fact]
    public async Task OneTimeIncomeSettlement_Autorizado_SuggestsLineAppliesOnIssueAndReopensOnAnnul()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));
        using var authorizer = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var (employeeId, plazaId) = await SeedOneTimeIncomeSettlementCandidateAsync(
            scenario.TenantId, "Elsa", "Eventual", "EMP-OTIL-A", "elsa.otil.a@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Rafael", "Solicitante", "EMP-OTIL-A3", "rafael.otil.a@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "DeRrhh", "EMP-OTIL-A2", "gestora.otil.a@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        // A $150 fixed one-time income, authorized to AUTORIZADO BEFORE the retirement (a retired profile is locked).
        var (incomeId, _) = await CreateAndAuthorizeOneTimeIncomeAsync(
            client, authorizer, employeeId, FixedOneTimeIncomeBody(requesterId, amount: 150));

        await ExecuteRetirementAsync(client, employeeId, requesterId);

        // [1 — SUGGESTED] The settlement of the principal plaza carries the INGRESO_EVENTUAL_PENDIENTE line at the
        // income amount ($150), INCLUDED and EDITABLE (a MANUAL line — isSystemCalculated=false, seed -9905).
        var created = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = plazaId, requestDate = DateTime.UtcNow.Date });
        var createdPayload = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.OK, $"Create failed: {(int)created.StatusCode} {createdPayload}");

        Guid settlementId, token, eventualLineId;
        using (var doc = JsonDocument.Parse(createdPayload))
        {
            var root = doc.RootElement;
            settlementId = root.GetProperty("publicId").GetGuid();
            token = root.GetProperty("concurrencyToken").GetGuid();

            var line = LineByConcept(root, "INGRESO_EVENTUAL_PENDIENTE");
            Assert.Equal(150m, line.GetProperty("calculatedAmount").GetDecimal());
            Assert.Equal(150m, line.GetProperty("finalAmount").GetDecimal());
            Assert.True(line.GetProperty("isIncluded").GetBoolean());
            Assert.False(line.GetProperty("isSystemCalculated").GetBoolean());
            eventualLineId = line.GetProperty("publicId").GetGuid();
        }

        // [2 — EDITABLE] The liquidator can edit the manual amount (proves the line is editable) → $120.
        var edited = await SendSettlementAsync(client, HttpMethod.Put,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/lines/{eventualLineId}",
            token, new { manualAmount = 120m });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        using (var doc = await ReadJsonAsync(edited))
        {
            token = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
            Assert.Equal(120m, LineByConcept(doc.RootElement, "INGRESO_EVENTUAL_PENDIENTE").GetProperty("finalAmount").GetDecimal());
        }

        // [3 — ISSUE] Issuing marks the AUTORIZADO income APLICADO (its line stayed included, §3.5).
        var issued = await SendSettlementAsync(client, HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/issuance",
            token, new { confirmNegativeNet = false });
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        token = await ReadTokenAsync(issued);
        Assert.Equal("APLICADO", await GetOneTimeIncomeStatusAsync(client, employeeId, incomeId));

        // [4 — ANNUL] Annulling the settlement reopens exactly the incomes it applied → AUTORIZADO again.
        var annulled = await SendSettlementAsync(client, HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/annulment",
            token, new { reason = "Corrección de la liquidación" });
        Assert.Equal(HttpStatusCode.OK, annulled.StatusCode);
        Assert.Equal("AUTORIZADO", await GetOneTimeIncomeStatusAsync(client, employeeId, incomeId));
    }

    [Fact]
    public async Task OneTimeIncomeSettlement_ExcludedLine_LeavesIncomeAutorizadoOnIssue()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));
        using var authorizer = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var (employeeId, plazaId) = await SeedOneTimeIncomeSettlementCandidateAsync(
            scenario.TenantId, "Noe", "Excluido", "EMP-OTIL-B", "noe.otil.b@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Rita", "Solicitante", "EMP-OTIL-B3", "rita.otil.b@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "DeRrhh", "EMP-OTIL-B2", "gestora.otil.b@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        var (incomeId, _) = await CreateAndAuthorizeOneTimeIncomeAsync(
            client, authorizer, employeeId, FixedOneTimeIncomeBody(requesterId, amount: 150));

        await ExecuteRetirementAsync(client, employeeId, requesterId);

        var created = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = plazaId, requestDate = DateTime.UtcNow.Date });
        var createdPayload = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.OK, $"Create failed: {(int)created.StatusCode} {createdPayload}");

        Guid settlementId, token, eventualLineId;
        using (var doc = JsonDocument.Parse(createdPayload))
        {
            var root = doc.RootElement;
            settlementId = root.GetProperty("publicId").GetGuid();
            token = root.GetProperty("concurrencyToken").GetGuid();
            eventualLineId = LineByConcept(root, "INGRESO_EVENTUAL_PENDIENTE").GetProperty("publicId").GetGuid();
        }

        // [1 — EXCLUDE] The liquidator EXCLUDES the eventual line (other income lines keep the settlement issuable).
        var excluded = await SendSettlementAsync(client, HttpMethod.Put,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/lines/{eventualLineId}",
            token, new { isIncluded = false });
        Assert.Equal(HttpStatusCode.OK, excluded.StatusCode);
        using (var doc = await ReadJsonAsync(excluded))
        {
            token = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
            Assert.False(LineByConcept(doc.RootElement, "INGRESO_EVENTUAL_PENDIENTE").GetProperty("isIncluded").GetBoolean());
        }

        // [2 — ISSUE] An EXCLUDED line ⇒ the income is NOT paid via the settlement and STAYS AUTORIZADO (§3.5).
        var issued = await SendSettlementAsync(client, HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/issuance",
            token, new { confirmNegativeNet = false });
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        Assert.Equal("AUTORIZADO", await GetOneTimeIncomeStatusAsync(client, employeeId, incomeId));
    }

    [Fact]
    public async Task OneTimeIncomeSettlement_MultiPlaza_LineOnlyOnPrincipalPlaza()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));
        using var authorizer = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var (employeeId, principalPlazaId) = await SeedOneTimeIncomeSettlementCandidateAsync(
            scenario.TenantId, "Ursula", "DosPlazas", "EMP-OTIL-C", "ursula.otil.c@empresa.test");
        var secondaryPlazaId = await AddSecondaryPlazaAsync(scenario.TenantId, employeeId);
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Rodrigo", "Solicitante", "EMP-OTIL-C3", "rodrigo.otil.c@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "DeRrhh", "EMP-OTIL-C2", "gestora.otil.c@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        // The one-time income lives on the PRINCIPAL plaza (default), AUTORIZADO → amount $150.
        _ = await CreateAndAuthorizeOneTimeIncomeAsync(
            client, authorizer, employeeId, FixedOneTimeIncomeBody(requesterId, amount: 150));

        await ExecuteRetirementAsync(client, employeeId, requesterId);

        // The SECONDARY plaza settlement carries NO line (the per-employee income is resolved only for the
        // principal plaza — a per-employee income vs per-plaza settlement must not double-suggest).
        var secondary = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = secondaryPlazaId, requestDate = DateTime.UtcNow.Date });
        var secondaryPayload = await secondary.Content.ReadAsStringAsync();
        Assert.True(secondary.StatusCode == HttpStatusCode.OK, $"Secondary create failed: {(int)secondary.StatusCode} {secondaryPayload}");
        using (var doc = JsonDocument.Parse(secondaryPayload))
        {
            Assert.Null(OneTimeIncomeLineOrNull(doc.RootElement));
        }

        // The PRINCIPAL plaza settlement DOES carry the line ($150).
        var principal = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = principalPlazaId, requestDate = DateTime.UtcNow.Date });
        var principalPayload = await principal.Content.ReadAsStringAsync();
        Assert.True(principal.StatusCode == HttpStatusCode.OK, $"Principal create failed: {(int)principal.StatusCode} {principalPayload}");
        using (var doc = JsonDocument.Parse(principalPayload))
        {
            var line = OneTimeIncomeLineOrNull(doc.RootElement);
            Assert.NotNull(line);
            Assert.Equal(150m, line!.Value.GetProperty("finalAmount").GetDecimal());
            Assert.False(line.Value.GetProperty("isSystemCalculated").GetBoolean());
        }
    }

    [Fact]
    public async Task OneTimeIncomeSettlement_NeverWritesToPayrollTransactionsNorCompensationConcepts()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));
        using var authorizer = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var (employeeId, plazaId) = await SeedOneTimeIncomeSettlementCandidateAsync(
            scenario.TenantId, "Bruno", "SinLedger", "EMP-OTIL-D", "bruno.otil.d@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Raquel", "Solicitante", "EMP-OTIL-D3", "raquel.otil.d@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "DeRrhh", "EMP-OTIL-D2", "gestora.otil.d@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        // Baseline after seeding: 0 payroll transactions, 1 compensation concept (the seeded SALARIO_BASE).
        var baseline = await CountLedgerRowsAsync(employeeId);

        // Full flow: create → authorize → apply a manual application → retire → create + issue a settlement.
        var (incomeId, token) = await CreateAndAuthorizeOneTimeIncomeAsync(
            client, authorizer, employeeId, FixedOneTimeIncomeBody(requesterId, amount: 150));
        var applied = await ApplyOneTimeIncomeAsync(client, employeeId, incomeId, token);
        Assert.Equal("APLICADO", applied.IncomeStatus);

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
            // The income is already APLICADO (manual) so it is NOT re-suggested — no eventual line on the settlement.
            Assert.Null(OneTimeIncomeLineOrNull(doc.RootElement));
        }

        var issued = await SendSettlementAsync(client, HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/issuance",
            settlementToken, new { confirmNegativeNet = false });
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);

        // The whole module (one-time income + application + settlement) never touches the payroll ledger nor the
        // plaza compensation concepts (RN-14 / RN-16): the row counts are unchanged.
        var after = await CountLedgerRowsAsync(employeeId);
        Assert.Equal(baseline.PayrollTransactions, after.PayrollTransactions);
        Assert.Equal(baseline.CompensationConcepts, after.CompensationConcepts);
    }
}
