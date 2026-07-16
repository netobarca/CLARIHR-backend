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

    private static TestUserContext PayrollRunAuthorizerContext(IntegrationTestScenario scenario, Guid userId) =>
        TestUserContext.Authenticated(userId, scenario.TenantId, PersonnelFilePermissionCodes.AuthorizePayrollRuns);

    private static Task<HttpResponseMessage> PatchPayrollRunAsync(
        HttpClient client, string url, Guid concurrencyToken, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, url);
        request.Headers.TryAddWithoutValidation("If-Match", concurrencyToken.ToString("D"));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request);
    }

    private static async Task<(string Status, Guid Token, decimal TotalIncome, int RegeneratedCount)> ReadRunAsync(
        HttpClient client, Guid companyId, Guid runId)
    {
        var response = await client.GetAsync($"/api/v1/companies/{companyId}/payroll-runs/{runId}");
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == response.StatusCode, $"get run: {(int)response.StatusCode} {payload}");
        using var doc = JsonDocument.Parse(payload);
        return (
            doc.RootElement.GetProperty("statusCode").GetString()!,
            doc.RootElement.GetProperty("concurrencyToken").GetGuid(),
            doc.RootElement.GetProperty("totalIncome").GetDecimal(),
            doc.RootElement.GetProperty("regeneratedCount").GetInt32());
    }

    private static async Task<JsonDocument> ReadEmployeeLinesAsync(
        HttpClient client, Guid companyId, Guid runId, Guid fileId)
    {
        var response = await client.GetAsync(
            $"/api/v1/companies/{companyId}/payroll-runs/{runId}/employees/{fileId}");
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == response.StatusCode, $"employee lines: {(int)response.StatusCode} {payload}");
        return JsonDocument.Parse(payload);
    }

    [Fact]
    public async Task PayrollRuns_ReviewLifecycle_AdjustAuthorizeReturnClose_EndToEnd()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(PayrollRunManagerContext(scenario));
        // The GENERATOR holding the authorize grant — the double anti-self must still reject them.
        using var selfAuthorizer = factory.CreateClientFor(PayrollRunAuthorizerContext(scenario, scenario.ActorUserId));
        using var authorizer = factory.CreateClientFor(PayrollRunAuthorizerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Ciclo", "Completo", "EMP-PRUN-E", "ciclo.prun.e@empresa.test");
        var (definitionId, periodId) = await CreatePayrollDefinitionWithCalendarAsync(manager, scenario.TenantId);

        var generate = await GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, periodId);
        Assert.Equal(HttpStatusCode.Created, generate.StatusCode);
        Guid runId;
        using (var doc = JsonDocument.Parse(await generate.Content.ReadAsStringAsync()))
        {
            runId = doc.RootElement.GetProperty("publicId").GetGuid();
        }

        var (status, token, totalIncome, _) = await ReadRunAsync(manager, scenario.TenantId, runId);
        Assert.Equal("GENERADA", status);
        Assert.Equal(300.00m, totalIncome);

        // Per-employee drill: the salary line travels with its class, source and calculated amount.
        Guid salaryLineId;
        using (var lines = await ReadEmployeeLinesAsync(manager, scenario.TenantId, runId, fileId))
        {
            var salary = lines.RootElement.GetProperty("lines").EnumerateArray()
                .Single(line => line.GetProperty("sourceModule").GetString() == "SALARIO");
            salaryLineId = salary.GetProperty("publicId").GetGuid();
            Assert.Equal("Ingreso", salary.GetProperty("lineClass").GetString());
            Assert.Equal(300.00m, salary.GetProperty("calculatedAmount").GetDecimal());
        }

        // Audited override (note MANDATORY) — the run's totals recompute over the final amounts.
        var adjust = await PatchPayrollRunAsync(
            manager,
            $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/lines/{salaryLineId}",
            token,
            new { overrideAmount = 350.00m, overrideNote = "Ajuste autorizado por gerencia" });
        var adjustPayload = await adjust.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == adjust.StatusCode, $"adjust: {(int)adjust.StatusCode} {adjustPayload}");
        using (var doc = JsonDocument.Parse(adjustPayload))
        {
            Assert.Equal(350.00m, doc.RootElement.GetProperty("totalIncome").GetDecimal());
        }

        // Double anti-self: the generator, even holding the dedicated grant, cannot authorize their run.
        (_, token, _, _) = await ReadRunAsync(manager, scenario.TenantId, runId);
        var selfAttempt = await PatchPayrollRunAsync(
            selfAuthorizer, $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/authorization", token);
        Assert.Equal(HttpStatusCode.Forbidden, selfAttempt.StatusCode);
        using (var doc = JsonDocument.Parse(await selfAttempt.Content.ReadAsStringAsync()))
        {
            Assert.Equal("PAYROLL_RUN_SELF_AUTHORIZATION_FORBIDDEN", doc.RootElement.GetProperty("code").GetString());
        }

        var authorize = await PatchPayrollRunAsync(
            authorizer, $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/authorization", token);
        var authorizePayload = await authorize.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == authorize.StatusCode, $"authorize: {(int)authorize.StatusCode} {authorizePayload}");
        using (var doc = JsonDocument.Parse(authorizePayload))
        {
            Assert.Equal("AUTORIZADA", doc.RootElement.GetProperty("statusCode").GetString());
        }

        // AUTORIZADA freezes the calculations: no adjustment until a return.
        (_, token, _, _) = await ReadRunAsync(manager, scenario.TenantId, runId);
        var frozenAdjust = await PatchPayrollRunAsync(
            manager,
            $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/lines/{salaryLineId}",
            token,
            new { overrideAmount = 999.99m, overrideNote = "no debe pasar" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, frozenAdjust.StatusCode);
        using (var doc = JsonDocument.Parse(await frozenAdjust.Content.ReadAsStringAsync()))
        {
            Assert.Equal("PAYROLL_RUN_STATE_RULE_VIOLATION", doc.RootElement.GetProperty("code").GetString());
        }

        // Return demands a reason (REQ-013 P-02 — the ONLY pre-closure reopening) …
        var returnEmpty = await PatchPayrollRunAsync(
            authorizer, $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/return", token, new { reason = " " });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, returnEmpty.StatusCode);
        using (var doc = JsonDocument.Parse(await returnEmpty.Content.ReadAsStringAsync()))
        {
            Assert.Equal("PAYROLL_RUN_RETURN_REASON_REQUIRED", doc.RootElement.GetProperty("code").GetString());
        }

        var returned = await PatchPayrollRunAsync(
            authorizer, $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/return", token,
            new { reason = "Revisar el ajuste del salario" });
        var returnedPayload = await returned.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == returned.StatusCode, $"return: {(int)returned.StatusCode} {returnedPayload}");
        using (var doc = JsonDocument.Parse(returnedPayload))
        {
            Assert.Equal("GENERADA", doc.RootElement.GetProperty("statusCode").GetString());
        }

        // … the run is editable again, re-authorizes and closes; the PERIOD closes with it (same tx).
        (_, token, _, _) = await ReadRunAsync(manager, scenario.TenantId, runId);
        var reauthorize = await PatchPayrollRunAsync(
            authorizer, $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/authorization", token);
        Assert.Equal(HttpStatusCode.OK, reauthorize.StatusCode);

        (_, token, _, _) = await ReadRunAsync(manager, scenario.TenantId, runId);
        var close = await PatchPayrollRunAsync(
            manager, $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/closure", token);
        var closePayload = await close.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == close.StatusCode, $"close: {(int)close.StatusCode} {closePayload}");
        using (var doc = JsonDocument.Parse(closePayload))
        {
            Assert.Equal("CERRADA", doc.RootElement.GetProperty("statusCode").GetString());
        }

        var periods = await manager.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/payroll-periods?payPeriodTypeCode=QUINCENAL&year=2026&pageSize=30");
        periods.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await periods.Content.ReadAsStringAsync()))
        {
            var period = doc.RootElement.GetProperty("items").EnumerateArray()
                .Single(item => item.GetProperty("publicId").GetGuid() == periodId);
            Assert.Equal("CERRADO", period.GetProperty("statusCode").GetString());
        }

        // CERRADA is terminal: no adjustment and no annulment.
        (_, token, _, _) = await ReadRunAsync(manager, scenario.TenantId, runId);
        var closedAdjust = await PatchPayrollRunAsync(
            manager,
            $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/lines/{salaryLineId}",
            token,
            new { overrideAmount = 1.00m, overrideNote = "tarde" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, closedAdjust.StatusCode);
        var closedAnnul = await PatchPayrollRunAsync(
            manager, $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/annulment", token,
            new { reason = "tarde" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, closedAnnul.StatusCode);
        using (var doc = JsonDocument.Parse(await closedAnnul.Content.ReadAsStringAsync()))
        {
            Assert.Equal("PAYROLL_RUN_STATE_RULE_VIOLATION", doc.RootElement.GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task PayrollRuns_Annul_RevertsPoolsSymmetrically_AndReleasesTheSlot()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(PayrollRunManagerContext(scenario));
        using var incomeAuthorizer = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Rever", "Simetrica", "EMP-PRUN-F", "rever.prun.f@empresa.test");
        var requesterId = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Rosa", "Pide", "EMP-PRUN-F2", "rosa.prun.f@empresa.test");
        var (definitionId, periodId) = await CreatePayrollDefinitionWithCalendarAsync(manager, scenario.TenantId);
        var (incomeId, _) = await CreateAndAuthorizeOneTimeIncomeAsync(
            manager, incomeAuthorizer, fileId, FixedOneTimeIncomeBody(requesterId));

        var generate = await GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, periodId);
        Assert.Equal(HttpStatusCode.Created, generate.StatusCode);
        Guid runId;
        decimal firstTotalIncome;
        using (var doc = JsonDocument.Parse(await generate.Content.ReadAsStringAsync()))
        {
            runId = doc.RootElement.GetProperty("publicId").GetGuid();
            firstTotalIncome = doc.RootElement.GetProperty("totalIncome").GetDecimal();
        }

        var (_, token, _, _) = await ReadRunAsync(manager, scenario.TenantId, runId);

        // The annulment reason is mandatory.
        var annulEmpty = await PatchPayrollRunAsync(
            manager, $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/annulment", token, new { reason = "" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, annulEmpty.StatusCode);
        using (var doc = JsonDocument.Parse(await annulEmpty.Content.ReadAsStringAsync()))
        {
            Assert.Equal("PAYROLL_RUN_ANNULMENT_REASON_REQUIRED", doc.RootElement.GetProperty("code").GetString());
        }

        var annul = await PatchPayrollRunAsync(
            manager, $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/annulment", token,
            new { reason = "Generada con datos incompletos" });
        var annulPayload = await annul.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == annul.StatusCode, $"annul: {(int)annul.StatusCode} {annulPayload}");
        using (var doc = JsonDocument.Parse(annulPayload))
        {
            Assert.Equal("ANULADA", doc.RootElement.GetProperty("statusCode").GetString());
        }

        // Symmetric reversal: the income is AUTORIZADO again and its MOTOR application ended ANULADA — the
        // pool is byte-identical to its pre-generation state (record re-applicable).
        var detail = await manager.GetAsync($"/api/v1/personnel-files/{fileId}/one-time-incomes/{incomeId}");
        detail.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await detail.Content.ReadAsStringAsync()))
        {
            Assert.Equal("AUTORIZADO", doc.RootElement.GetProperty("statusCode").GetString());
        }

        using (var history = await GetOneTimeIncomeApplicationsAsync(manager, fileId, incomeId))
        {
            var application = Assert.Single(history.RootElement.EnumerateArray().ToArray());
            Assert.Equal("MOTOR", application.GetProperty("originCode").GetString());
            Assert.Equal("ANULADA", application.GetProperty("statusCode").GetString());
        }

        // The one-active-run slot is released: a fresh generation re-applies the SAME income and squares
        // to the SAME totals.
        var regenerate = await GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, periodId);
        var regeneratePayload = await regenerate.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == regenerate.StatusCode, $"second generate: {(int)regenerate.StatusCode} {regeneratePayload}");
        using (var doc = JsonDocument.Parse(regeneratePayload))
        {
            Assert.Equal(firstTotalIncome, doc.RootElement.GetProperty("totalIncome").GetDecimal());
        }

        detail = await manager.GetAsync($"/api/v1/personnel-files/{fileId}/one-time-incomes/{incomeId}");
        detail.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await detail.Content.ReadAsStringAsync()))
        {
            Assert.Equal("APLICADO", doc.RootElement.GetProperty("statusCode").GetString());
        }
    }

    [Fact]
    public async Task PayrollRuns_ExcludePoolLine_FreesTheRecord_AndReincludeReapplies()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(PayrollRunManagerContext(scenario));
        using var incomeAuthorizer = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Libre", "Otra", "EMP-PRUN-G", "libre.prun.g@empresa.test");
        var requesterId = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Rene", "Manda", "EMP-PRUN-G2", "rene.prun.g@empresa.test");
        var (definitionId, periodId) = await CreatePayrollDefinitionWithCalendarAsync(manager, scenario.TenantId);
        var (incomeId, _) = await CreateAndAuthorizeOneTimeIncomeAsync(
            manager, incomeAuthorizer, fileId, FixedOneTimeIncomeBody(requesterId));
        var incomeAmount = await ReadOneTimeIncomeAmountAsync(manager, fileId, incomeId);

        var generate = await GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, periodId);
        Assert.Equal(HttpStatusCode.Created, generate.StatusCode);
        Guid runId;
        using (var doc = JsonDocument.Parse(await generate.Content.ReadAsStringAsync()))
        {
            runId = doc.RootElement.GetProperty("publicId").GetGuid();
        }

        // The applied pool line references the CREATED application child (§3.5), not the income itself.
        Guid poolLineId;
        using (var lines = await ReadEmployeeLinesAsync(manager, scenario.TenantId, runId, fileId))
        {
            var poolLine = lines.RootElement.GetProperty("lines").EnumerateArray()
                .Single(line => line.GetProperty("sourceModule").GetString() == "ONE_TIME_INCOME");
            poolLineId = poolLine.GetProperty("publicId").GetGuid();
            Assert.NotEqual(incomeId, poolLine.GetProperty("sourceReferencePublicId").GetGuid());
        }

        var (_, token, includedIncome, _) = await ReadRunAsync(manager, scenario.TenantId, runId);

        // EXCLUDE → the MOTOR application is annulled, the income is AUTORIZADO again (free for another
        // run — never in two active ones: this run no longer counts it) and the line re-binds to the PARENT.
        var exclude = await PatchPayrollRunAsync(
            manager,
            $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/lines/{poolLineId}",
            token,
            new { isIncluded = false });
        var excludePayload = await exclude.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == exclude.StatusCode, $"exclude: {(int)exclude.StatusCode} {excludePayload}");
        using (var doc = JsonDocument.Parse(excludePayload))
        {
            Assert.Equal(includedIncome - incomeAmount, doc.RootElement.GetProperty("totalIncome").GetDecimal());
        }

        var detail = await manager.GetAsync($"/api/v1/personnel-files/{fileId}/one-time-incomes/{incomeId}");
        detail.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await detail.Content.ReadAsStringAsync()))
        {
            Assert.Equal("AUTORIZADO", doc.RootElement.GetProperty("statusCode").GetString());
        }

        using (var lines = await ReadEmployeeLinesAsync(manager, scenario.TenantId, runId, fileId))
        {
            var poolLine = lines.RootElement.GetProperty("lines").EnumerateArray()
                .Single(line => line.GetProperty("sourceModule").GetString() == "ONE_TIME_INCOME");
            Assert.False(poolLine.GetProperty("isIncluded").GetBoolean());
            Assert.Equal(incomeId, poolLine.GetProperty("sourceReferencePublicId").GetGuid());
        }

        // RE-INCLUDE → the §3.5 flow re-applies it (new MOTOR child) and the totals square again.
        (_, token, _, _) = await ReadRunAsync(manager, scenario.TenantId, runId);
        var reinclude = await PatchPayrollRunAsync(
            manager,
            $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/lines/{poolLineId}",
            token,
            new { isIncluded = true });
        var reincludePayload = await reinclude.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == reinclude.StatusCode, $"re-include: {(int)reinclude.StatusCode} {reincludePayload}");
        using (var doc = JsonDocument.Parse(reincludePayload))
        {
            Assert.Equal(includedIncome, doc.RootElement.GetProperty("totalIncome").GetDecimal());
        }

        detail = await manager.GetAsync($"/api/v1/personnel-files/{fileId}/one-time-incomes/{incomeId}");
        detail.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await detail.Content.ReadAsStringAsync()))
        {
            Assert.Equal("APLICADO", doc.RootElement.GetProperty("statusCode").GetString());
        }

        using (var lines = await ReadEmployeeLinesAsync(manager, scenario.TenantId, runId, fileId))
        {
            var poolLine = lines.RootElement.GetProperty("lines").EnumerateArray()
                .Single(line => line.GetProperty("sourceModule").GetString() == "ONE_TIME_INCOME");
            Assert.True(poolLine.GetProperty("isIncluded").GetBoolean());
            Assert.NotEqual(incomeId, poolLine.GetProperty("sourceReferencePublicId").GetGuid());
        }
    }

    [Fact]
    public async Task PayrollRuns_RegenerateAndRecalculate_RederiveFromCurrentInputs()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(PayrollRunManagerContext(scenario));
        using var incomeAuthorizer = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Nueva", "Base", "EMP-PRUN-H", "nueva.prun.h@empresa.test");
        var requesterId = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Rey", "Firma", "EMP-PRUN-H2", "rey.prun.h@empresa.test");
        var (definitionId, periodId) = await CreatePayrollDefinitionWithCalendarAsync(manager, scenario.TenantId);

        var generate = await GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, periodId);
        Assert.Equal(HttpStatusCode.Created, generate.StatusCode);
        Guid runId;
        using (var doc = JsonDocument.Parse(await generate.Content.ReadAsStringAsync()))
        {
            runId = doc.RootElement.GetProperty("publicId").GetGuid();
        }

        // An override rides the run …
        Guid salaryLineId;
        using (var lines = await ReadEmployeeLinesAsync(manager, scenario.TenantId, runId, fileId))
        {
            salaryLineId = lines.RootElement.GetProperty("lines").EnumerateArray()
                .Single(line => line.GetProperty("sourceModule").GetString() == "SALARIO")
                .GetProperty("publicId").GetGuid();
        }

        var (_, token, _, _) = await ReadRunAsync(manager, scenario.TenantId, runId);
        var adjust = await PatchPayrollRunAsync(
            manager,
            $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/lines/{salaryLineId}",
            token,
            new { overrideAmount = 999.00m, overrideNote = "se descarta al regenerar" });
        Assert.Equal(HttpStatusCode.OK, adjust.StatusCode);

        // … REGENERATE discards it (full rebuild from current inputs) and bumps the counter.
        (_, token, _, _) = await ReadRunAsync(manager, scenario.TenantId, runId);
        var regenerate = await PatchPayrollRunAsync(
            manager, $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/regeneration", token);
        var regeneratePayload = await regenerate.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == regenerate.StatusCode, $"regenerate: {(int)regenerate.StatusCode} {regeneratePayload}");
        using (var doc = JsonDocument.Parse(regeneratePayload))
        {
            Assert.Equal(1, doc.RootElement.GetProperty("regeneratedCount").GetInt32());
            Assert.Equal(600.00m, doc.RootElement.GetProperty("totalIncome").GetDecimal());
        }

        // A pool income registered AFTER the generation joins via selective RECALCULATION of its employee.
        var (incomeId, _) = await CreateAndAuthorizeOneTimeIncomeAsync(
            manager, incomeAuthorizer, fileId, FixedOneTimeIncomeBody(requesterId));
        var incomeAmount = await ReadOneTimeIncomeAmountAsync(manager, fileId, incomeId);

        (_, token, _, _) = await ReadRunAsync(manager, scenario.TenantId, runId);
        var recalculate = await PatchPayrollRunAsync(
            manager,
            $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/recalculation",
            token,
            new { employeeIds = new[] { fileId } });
        var recalculatePayload = await recalculate.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == recalculate.StatusCode, $"recalculate: {(int)recalculate.StatusCode} {recalculatePayload}");
        using (var doc = JsonDocument.Parse(recalculatePayload))
        {
            Assert.Equal(600.00m + incomeAmount, doc.RootElement.GetProperty("totalIncome").GetDecimal());
        }

        var detail = await manager.GetAsync($"/api/v1/personnel-files/{fileId}/one-time-incomes/{incomeId}");
        detail.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await detail.Content.ReadAsStringAsync()))
        {
            Assert.Equal("APLICADO", doc.RootElement.GetProperty("statusCode").GetString());
        }
    }
}
