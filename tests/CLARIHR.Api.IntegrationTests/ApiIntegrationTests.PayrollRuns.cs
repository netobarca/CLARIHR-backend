using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Application.Features.Leave.Common;
using CLARIHR.Application.Features.Payroll.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Integration coverage for the payroll-run generation slice (REQ-012 PR-5): pre-flight ≡ generation
/// (same inputs, no writes), the happy path (population → engine → persisted run with totals), the
/// one-active-run slot (sequential 409 + the CARRERA: two concurrent generates → exactly one run), the
/// pool application with origin MOTOR (the one-time income flips to APLICADO and its amount squares into
/// the run's totals — golden 10 e2e) and the no-external-write invariant (RN-13: the engine never touches
/// the external payroll ledger).
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static readonly DateTime PayrollRunHireDate = new(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static TestUserContext PayrollRunManagerContext(IntegrationTestScenario scenario, Guid? userId = null) =>
        TestUserContext.Authenticated(
            userId ?? scenario.ActorUserId,
            scenario.TenantId,
            PersonnelFilePermissionCodes.Admin,
            PayrollConfigurationPermissionCodes.Manage,
            LeaveConfigurationPermissionCodes.Admin);

    /// <summary>Employee + ACTIVE primary plaza of the QUINCENAL payroll type with a $600/month base salary.</summary>
    private async Task<Guid> SeedPayrollRunCandidateAsync(
        Guid tenantId,
        string firstName,
        string lastName,
        string employeeCode,
        string institutionalEmail,
        decimal monthlySalary = 600m)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // A cost center on the plaza — the one-time income registration demands it (RN of REQ-006).
        var costCenterType = CLARIHR.Domain.CostCenters.CostCenterType.Create($"CCT-{employeeCode}", $"Tipo {employeeCode}", null);
        costCenterType.SetTenantId(tenantId);
        dbContext.Set<CLARIHR.Domain.CostCenters.CostCenterType>().Add(costCenterType);
        await dbContext.SaveChangesAsync();
        var costCenter = CLARIHR.Domain.CostCenters.CostCenter.Create(
            $"CC-{employeeCode}", $"Centro de costo {employeeCode}", costCenterType.Id, null, null, null, null);
        costCenter.SetTenantId(tenantId);
        dbContext.Set<CLARIHR.Domain.CostCenters.CostCenter>().Add(costCenter);
        await dbContext.SaveChangesAsync();

        var file = PersonnelFile.Create(
            PersonnelFileRecordType.Employee,
            firstName,
            lastName,
            new DateTime(1990, 3, 15, 0, 0, 0, DateTimeKind.Utc),
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

        var profile = PersonnelFileEmployeeProfile.Create(employeeCode, "ACTIVO", PayrollRunHireDate, 365m);
        profile.BindToPersonnelFile(file.Id);
        profile.SetTenantId(tenantId);
        dbContext.Set<PersonnelFileEmployeeProfile>().Add(profile);

        var assignment = PersonnelFileEmploymentAssignment.Create(
            "INDEFINIDO",
            contractTypeCode: null,
            workdayCode: null,
            payrollTypeCode: "QUINCENAL",
            positionSlotPublicId: null,
            orgUnitPublicId: null,
            workCenterPublicId: null,
            costCenterPublicId: costCenter.PublicId,
            startDate: PayrollRunHireDate,
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
            startDate: PayrollRunHireDate,
            endDate: null,
            isActive: true,
            isSystemSuggested: false,
            notes: null);
        salary.BindToPersonnelFile(file.Id);
        salary.SetTenantId(tenantId);
        dbContext.Set<PersonnelFileCompensationConcept>().Add(salary);
        await dbContext.SaveChangesAsync();

        return file.PublicId;
    }

    /// <summary>QUINCENAL Nómina + its 2026 calendar; returns the definition and the first period's id.</summary>
    private static async Task<(Guid DefinitionId, Guid PeriodId)> CreatePayrollDefinitionWithCalendarAsync(
        HttpClient manager, Guid companyId)
    {
        var definitionResponse = await manager.PostAsJsonAsync(
            $"/api/v1/companies/{companyId}/payroll-definitions",
            new
            {
                code = "NOM-QUINCENAL",
                name = "Nómina quincenal",
                payrollTypeCode = "QUINCENAL",
                payPeriodCode = "QUINCENAL",
                totalPeriods = 24,
                currencyCode = "USD",
            });
        var definitionPayload = await definitionResponse.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == definitionResponse.StatusCode, $"definition: {(int)definitionResponse.StatusCode} {definitionPayload}");
        Guid definitionId;
        using (var doc = JsonDocument.Parse(definitionPayload))
        {
            Assert.True(
                doc.RootElement.TryGetProperty("publicId", out var idProperty),
                $"definition payload without 'publicId': {definitionPayload}");
            definitionId = idProperty.GetGuid();
        }

        var calendarResponse = await manager.PostAsync(
            $"/api/v1/companies/{companyId}/payroll-definitions/{definitionId}/periods/generate?year=2026", content: null);
        var calendarPayload = await calendarResponse.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == calendarResponse.StatusCode, $"calendar: {(int)calendarResponse.StatusCode} {calendarPayload}");
        using (var doc = JsonDocument.Parse(calendarPayload))
        {
            Assert.True(
                doc.RootElement.TryGetProperty("created", out var created),
                $"calendar payload without 'created': {calendarPayload}");
            Assert.Equal(24, created.GetInt32());
        }

        var periods = await manager.GetAsync(
            $"/api/v1/companies/{companyId}/payroll-periods?payPeriodTypeCode=QUINCENAL&year=2026&pageSize=30");
        periods.EnsureSuccessStatusCode();
        using var periodsDoc = JsonDocument.Parse(await periods.Content.ReadAsStringAsync());
        var period = periodsDoc.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("number").GetInt32() == 1 &&
                            item.GetProperty("payrollDefinitionPublicId").GetGuid() == definitionId);

        return (definitionId, period.GetProperty("publicId").GetGuid());
    }

    private static Task<HttpResponseMessage> GeneratePayrollRunAsync(
        HttpClient client, Guid companyId, Guid definitionId, Guid periodId) =>
        client.PostAsJsonAsync(
            $"/api/v1/companies/{companyId}/payroll-runs",
            new { payrollDefinitionPublicId = definitionId, payrollPeriodPublicId = periodId });

    [Fact]
    public async Task PayrollRuns_PreflightAndGenerate_SalaryBaseSquares()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(PayrollRunManagerContext(scenario));

        _ = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Quin", "Base", "EMP-PRUN-A", "quin.prun.a@empresa.test");
        var (definitionId, periodId) = await CreatePayrollDefinitionWithCalendarAsync(manager, scenario.TenantId);

        // Pre-flight: 1 employee, quincena base = 600/2 = 300.00 projected, nothing written.
        var preflight = await manager.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/payroll-runs/preflight",
            new { payrollDefinitionPublicId = definitionId, payrollPeriodPublicId = periodId });
        var preflightPayload = await preflight.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == preflight.StatusCode, $"preflight: {(int)preflight.StatusCode} {preflightPayload}");
        decimal projectedNet;
        using (var doc = JsonDocument.Parse(preflightPayload))
        {
            Assert.Equal(1, doc.RootElement.GetProperty("employeeCount").GetInt32());
            Assert.Equal(300.00m, doc.RootElement.GetProperty("projectedTotalIncome").GetDecimal());
            projectedNet = doc.RootElement.GetProperty("projectedTotalNet").GetDecimal();
        }

        var response = await GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, periodId);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, $"generate: {(int)response.StatusCode} {payload}");
        using var runDoc = JsonDocument.Parse(payload);
        var root = runDoc.RootElement;
        Assert.Equal("GENERADA", root.GetProperty("statusCode").GetString());
        Assert.Equal(1, root.GetProperty("employeeCount").GetInt32());
        Assert.Equal(300.00m, root.GetProperty("totalIncome").GetDecimal());
        // Generation ≡ pre-flight: same inputs, same figures (the pre-flight wrote nothing).
        Assert.Equal(projectedNet, root.GetProperty("totalNet").GetDecimal());
        Assert.Equal(
            root.GetProperty("totalIncome").GetDecimal() - root.GetProperty("totalDeductions").GetDecimal(),
            root.GetProperty("totalNet").GetDecimal());
    }

    [Fact]
    public async Task PayrollRuns_OneActiveRunPerPeriod_SequentialAndConcurrent()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(PayrollRunManagerContext(scenario));

        _ = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Uno", "Activo", "EMP-PRUN-B", "uno.prun.b@empresa.test");
        var (definitionId, periodId) = await CreatePayrollDefinitionWithCalendarAsync(manager, scenario.TenantId);

        var first = await GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, periodId);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Sequential second submit → clean 409.
        var second = await GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, periodId);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        using (var doc = JsonDocument.Parse(await second.Content.ReadAsStringAsync()))
        {
            Assert.Equal("PAYROLL_RUN_ALREADY_ACTIVE", doc.RootElement.GetProperty("code").GetString());
        }

        // CARRERA on a fresh period: two concurrent generates → exactly one 201 (the advisory lock + the
        // partial unique index close the race; the loser gets the clean 409).
        var periods = await manager.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/payroll-periods?payPeriodTypeCode=QUINCENAL&year=2026&pageSize=30");
        periods.EnsureSuccessStatusCode();
        Guid secondPeriodId;
        using (var doc = JsonDocument.Parse(await periods.Content.ReadAsStringAsync()))
        {
            secondPeriodId = doc.RootElement.GetProperty("items").EnumerateArray()
                .Single(item => item.GetProperty("number").GetInt32() == 2 &&
                                item.GetProperty("payrollDefinitionPublicId").GetGuid() == definitionId)
                .GetProperty("publicId").GetGuid();
        }

        var race = await Task.WhenAll(
            GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, secondPeriodId),
            GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, secondPeriodId));
        Assert.Equal(1, race.Count(item => item.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, race.Count(item => item.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task PayrollRuns_AppliesOneTimeIncomeWithMotorOrigin_AndSquares()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(PayrollRunManagerContext(scenario));
        using var authorizer = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Bono", "Motor", "EMP-PRUN-C", "bono.prun.c@empresa.test");
        var requesterId = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Rita", "Solicita", "EMP-PRUN-C2", "rita.prun.c@empresa.test");
        var (definitionId, periodId) = await CreatePayrollDefinitionWithCalendarAsync(manager, scenario.TenantId);

        // An AUTORIZADO one-time income of $75.50 (QUINCENAL type, no destination) joins the run.
        var (incomeId, _) = await CreateAndAuthorizeOneTimeIncomeAsync(
            manager, authorizer, fileId, FixedOneTimeIncomeBody(requesterId));

        var response = await GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, periodId);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, $"generate: {(int)response.StatusCode} {payload}");
        using (var doc = JsonDocument.Parse(payload))
        {
            // Salary 300.00 (two candidates seeded → 600.00) + the pool income — the run squares with its
            // inputs (golden 10 e2e).
            Assert.Equal(2, doc.RootElement.GetProperty("employeeCount").GetInt32());
            var expectedIncome = 600.00m + await ReadOneTimeIncomeAmountAsync(manager, fileId, incomeId);
            Assert.Equal(expectedIncome, doc.RootElement.GetProperty("totalIncome").GetDecimal());
        }

        // The income flipped AUTORIZADO → APLICADO and its single application carries origin MOTOR.
        var detail = await manager.GetAsync($"/api/v1/personnel-files/{fileId}/one-time-incomes/{incomeId}");
        detail.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await detail.Content.ReadAsStringAsync()))
        {
            Assert.Equal("APLICADO", doc.RootElement.GetProperty("statusCode").GetString());
        }

        using var history = await GetOneTimeIncomeApplicationsAsync(manager, fileId, incomeId);
        var application = Assert.Single(history.RootElement.EnumerateArray().ToArray());
        Assert.Equal("MOTOR", application.GetProperty("originCode").GetString());
        Assert.Equal("APLICADA", application.GetProperty("statusCode").GetString());
    }

    [Fact]
    public async Task PayrollRuns_Generate_NeverWritesTheExternalLedger()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(PayrollRunManagerContext(scenario));

        _ = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Led", "Guer", "EMP-PRUN-D", "led.prun.d@empresa.test");
        var (definitionId, periodId) = await CreatePayrollDefinitionWithCalendarAsync(manager, scenario.TenantId);

        int ledgerBefore, conceptsBefore;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            ledgerBefore = await dbContext.Set<PersonnelFilePayrollTransaction>().IgnoreQueryFilters().CountAsync();
            conceptsBefore = await dbContext.Set<PersonnelFileCompensationConcept>().IgnoreQueryFilters().CountAsync();
        }

        var response = await GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, periodId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // RN-13: the engine reads compensation and NEVER writes the external payroll ledger nor the
        // compensation concepts — a silent write here would corrupt certified payroll data.
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(ledgerBefore, await dbContext.Set<PersonnelFilePayrollTransaction>().IgnoreQueryFilters().CountAsync());
            Assert.Equal(conceptsBefore, await dbContext.Set<PersonnelFileCompensationConcept>().IgnoreQueryFilters().CountAsync());
        }
    }

    private static async Task<decimal> ReadOneTimeIncomeAmountAsync(HttpClient client, Guid fileId, Guid incomeId)
    {
        var response = await client.GetAsync($"/api/v1/personnel-files/{fileId}/one-time-incomes/{incomeId}");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("amount").GetDecimal();
    }
}
