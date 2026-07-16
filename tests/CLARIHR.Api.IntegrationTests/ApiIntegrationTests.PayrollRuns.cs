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
        decimal monthlySalary = 600m,
        Guid? linkedUserPublicId = null,
        bool withBankAccount = false)
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
        if (linkedUserPublicId is { } linkedUser)
        {
            file.Complete(linkedUser);
        }
        else
        {
            file.CompleteWithoutLinkedUser();
        }

        dbContext.Set<PersonnelFile>().Add(file);
        await dbContext.SaveChangesAsync();

        PersonnelFileBankAccount? bankAccount = null;
        if (withBankAccount)
        {
            bankAccount = PersonnelFileBankAccount.Create(
                bankCatalogItemId: null, "BANCO AGRICOLA", "USD", $"CTA-{employeeCode}", "AHORRO", isPrimary: true);
            bankAccount.SetTenantId(tenantId);
            file.AddBankAccount(bankAccount);
            await dbContext.SaveChangesAsync();
        }

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
            notes: null,
            paymentMethodCode: withBankAccount ? "TRANSFERENCIA" : null,
            paymentBankAccountPublicId: bankAccount?.PublicId);
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

    [Fact]
    public async Task PayrollRuns_Bandeja_ExportsAndBankReconciliation_Square()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(PayrollRunManagerContext(scenario));

        _ = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Cuenta", "Primaria", "EMP-PRUN-I", "cuenta.prun.i@empresa.test", withBankAccount: true);
        _ = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Sin", "Cuenta", "EMP-PRUN-I2", "sin.prun.i@empresa.test");
        var (definitionId, periodId) = await CreatePayrollDefinitionWithCalendarAsync(manager, scenario.TenantId);

        var generate = await GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, periodId);
        Assert.Equal(HttpStatusCode.Created, generate.StatusCode);
        Guid runId;
        decimal totalNet;
        using (var doc = JsonDocument.Parse(await generate.Content.ReadAsStringAsync()))
        {
            runId = doc.RootElement.GetProperty("publicId").GetGuid();
            totalNet = doc.RootElement.GetProperty("totalNet").GetDecimal();
        }

        // Bandeja: the persisted header travels as-is; statusCounts are the tab numbers.
        var bandeja = await manager.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/payroll-runs/query", new { });
        var bandejaPayload = await bandeja.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == bandeja.StatusCode, $"bandeja: {(int)bandeja.StatusCode} {bandejaPayload}");
        using (var doc = JsonDocument.Parse(bandejaPayload))
        {
            var item = Assert.Single(doc.RootElement.GetProperty("items").EnumerateArray().ToArray());
            Assert.Equal(runId, item.GetProperty("payrollRunPublicId").GetGuid());
            Assert.Equal(totalNet, item.GetProperty("totalNet").GetDecimal());
            Assert.Equal(definitionId, item.GetProperty("payrollDefinitionPublicId").GetGuid());
            Assert.Equal(1, doc.RootElement.GetProperty("statusCounts").GetProperty("GENERADA").GetInt32());
        }

        // Filtering to another status empties the ITEMS but never the counts (they span every status).
        var filtered = await manager.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/payroll-runs/query", new { statusCode = "CERRADA" });
        filtered.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await filtered.Content.ReadAsStringAsync()))
        {
            Assert.Empty(doc.RootElement.GetProperty("items").EnumerateArray().ToArray());
            Assert.Equal(1, doc.RootElement.GetProperty("statusCounts").GetProperty("GENERADA").GetInt32());
        }

        // Exports smoke: the two formats the client asked for (REQ-013 RF-020).
        var csv = await manager.GetAsync($"/api/v1/companies/{scenario.TenantId}/payroll-runs/export?format=csv");
        Assert.True(HttpStatusCode.OK == csv.StatusCode, $"csv export: {(int)csv.StatusCode}");
        Assert.StartsWith("text/csv", csv.Content.Headers.ContentType!.MediaType);
        var xlsx = await manager.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/lines/export?format=xlsx");
        Assert.True(HttpStatusCode.OK == xlsx.StatusCode, $"lines export: {(int)xlsx.StatusCode}");
        Assert.Contains("spreadsheetml", xlsx.Content.Headers.ContentType!.MediaType);

        // The payroll print carries detail AND summary rows (per concept / per cost center).
        var linesJson = await manager.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/lines/export?format=json");
        linesJson.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await linesJson.Content.ReadAsStringAsync()))
        {
            var tipos = doc.RootElement.EnumerateArray()
                .Select(row => row.GetProperty("TipoFila").GetString())
                .ToArray();
            Assert.Contains("DETALLE", tipos);
            Assert.Contains("TOTAL_POR_CONCEPTO", tipos);
            Assert.Contains("TOTAL_POR_CENTRO_COSTO", tipos);
        }

        // Bank reconciliation: Σ of the employee nets ≡ the run's net; the account-less employee travels
        // with the warning instead of blocking (advertir-nunca-bloquear).
        var reconciliation = await manager.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/bank-reconciliation/export?format=json");
        var reconciliationPayload = await reconciliation.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == reconciliation.StatusCode, $"reconciliation: {(int)reconciliation.StatusCode} {reconciliationPayload}");
        using (var doc = JsonDocument.Parse(reconciliationPayload))
        {
            var rows = doc.RootElement.EnumerateArray().ToArray();
            Assert.Equal(2, rows.Length);
            Assert.Equal(totalNet, rows.Sum(row => row.GetProperty("Neto").GetDecimal()));

            var withAccount = rows.Single(row => row.GetProperty("CodigoEmpleado").GetString() == "EMP-PRUN-I");
            Assert.Equal("BANCO AGRICOLA", withAccount.GetProperty("Banco").GetString());
            Assert.Equal("TRANSFERENCIA", withAccount.GetProperty("FormaPago").GetString());
            Assert.Equal(JsonValueKind.Null, withAccount.GetProperty("Advertencia").ValueKind);

            var withoutAccount = rows.Single(row => row.GetProperty("CodigoEmpleado").GetString() == "EMP-PRUN-I2");
            Assert.Equal("PAYROLL_WARNING_NO_BANK_ACCOUNT", withoutAccount.GetProperty("Advertencia").GetString());
        }
    }

    [Fact]
    public async Task PayrollRuns_EmployeeHistory_CorporateAndSelfService_FixedStates()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(PayrollRunManagerContext(scenario));
        using var authorizer = factory.CreateClientFor(PayrollRunAuthorizerContext(scenario, Guid.NewGuid()));
        var employeeUserId = Guid.NewGuid();
        // The employee: authenticated, ZERO permissions — the self-or-view gate must carry them.
        using var employee = factory.CreateClientFor(TestUserContext.Authenticated(employeeUserId, scenario.TenantId));

        var fileA = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Historial", "Propio", "EMP-PRUN-J", "historial.prun.j@empresa.test",
            linkedUserPublicId: employeeUserId);
        var fileB = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Otro", "Expediente", "EMP-PRUN-J2", "otro.prun.j@empresa.test");
        var (definitionId, periodId) = await CreatePayrollDefinitionWithCalendarAsync(manager, scenario.TenantId);

        var generate = await GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, periodId);
        Assert.Equal(HttpStatusCode.Created, generate.StatusCode);
        Guid runId;
        using (var doc = JsonDocument.Parse(await generate.Content.ReadAsStringAsync()))
        {
            runId = doc.RootElement.GetProperty("publicId").GetGuid();
        }

        // Corporate default = payment history (CERRADA+AUTORIZADA): the GENERADA run is NOT there…
        var historyDefault = await manager.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/payroll-runs/employee-history/query",
            new { personnelFilePublicId = fileA });
        historyDefault.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await historyDefault.Content.ReadAsStringAsync()))
        {
            Assert.Empty(doc.RootElement.GetProperty("items").EnumerateArray().ToArray());
        }

        // …and WITH the explicit GENERADA filter the SAME endpoint is the open-period view; the row's
        // sums square with the drill (fila ≡ Σ líneas — the PR-7 gate).
        decimal drillNet;
        using (var drill = await ReadEmployeeLinesAsync(manager, scenario.TenantId, runId, fileA))
        {
            drillNet = drill.RootElement.GetProperty("totalNet").GetDecimal();
        }

        var openPeriod = await manager.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/payroll-runs/employee-history/query",
            new { personnelFilePublicId = fileA, statusCodes = new[] { "GENERADA" } });
        openPeriod.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await openPeriod.Content.ReadAsStringAsync()))
        {
            var row = Assert.Single(doc.RootElement.GetProperty("items").EnumerateArray().ToArray());
            Assert.Equal(runId, row.GetProperty("payrollRunPublicId").GetGuid());
            Assert.Equal("GENERADA", row.GetProperty("statusCode").GetString());
            Assert.Equal(drillNet, row.GetProperty("totalNet").GetDecimal());
        }

        // Self-service while GENERADA: the fixed-states surface shows NOTHING (list empty, drill 404).
        var selfWhileDraft = await employee.GetAsync($"/api/v1/personnel-files/{fileA}/payroll-history");
        var selfWhileDraftPayload = await selfWhileDraft.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == selfWhileDraft.StatusCode, $"self draft: {(int)selfWhileDraft.StatusCode} {selfWhileDraftPayload}");
        using (var doc = JsonDocument.Parse(selfWhileDraftPayload))
        {
            Assert.Empty(doc.RootElement.GetProperty("items").EnumerateArray().ToArray());
        }

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await employee.GetAsync($"/api/v1/personnel-files/{fileA}/payroll-history/{runId}")).StatusCode);

        // Authorize (other user) + close — the run becomes payment history.
        var (_, token, _, _) = await ReadRunAsync(manager, scenario.TenantId, runId);
        var authorize = await PatchPayrollRunAsync(
            authorizer, $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/authorization", token);
        Assert.Equal(HttpStatusCode.OK, authorize.StatusCode);
        (_, token, _, _) = await ReadRunAsync(manager, scenario.TenantId, runId);
        var close = await PatchPayrollRunAsync(
            manager, $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runId}/closure", token);
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);

        var historyClosed = await manager.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/payroll-runs/employee-history/query",
            new { personnelFilePublicId = fileA });
        historyClosed.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await historyClosed.Content.ReadAsStringAsync()))
        {
            var row = Assert.Single(doc.RootElement.GetProperty("items").EnumerateArray().ToArray());
            Assert.Equal("CERRADA", row.GetProperty("statusCode").GetString());
        }

        // Self-service now sees their history and their OWN lines only.
        var selfHistory = await employee.GetAsync($"/api/v1/personnel-files/{fileA}/payroll-history");
        selfHistory.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await selfHistory.Content.ReadAsStringAsync()))
        {
            var row = Assert.Single(doc.RootElement.GetProperty("items").EnumerateArray().ToArray());
            Assert.Equal(runId, row.GetProperty("payrollRunPublicId").GetGuid());
            Assert.Equal(drillNet, row.GetProperty("totalNet").GetDecimal());
        }

        var selfLines = await employee.GetAsync($"/api/v1/personnel-files/{fileA}/payroll-history/{runId}");
        var selfLinesPayload = await selfLines.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == selfLines.StatusCode, $"self lines: {(int)selfLines.StatusCode} {selfLinesPayload}");
        using (var doc = JsonDocument.Parse(selfLinesPayload))
        {
            Assert.Equal(fileA, doc.RootElement.GetProperty("employeePublicId").GetGuid());
            Assert.All(
                doc.RootElement.GetProperty("lines").EnumerateArray(),
                line => Assert.Equal(fileA, line.GetProperty("employeePublicId").GetGuid()));
        }

        // Someone else's file: 403 (the file exists, the caller is neither linked nor HR)…
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await employee.GetAsync($"/api/v1/personnel-files/{fileB}/payroll-history")).StatusCode);

        // …an unknown file: 404 — and the corporate View grant passes the same surface.
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await employee.GetAsync($"/api/v1/personnel-files/{Guid.NewGuid()}/payroll-history")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await manager.GetAsync($"/api/v1/personnel-files/{fileA}/payroll-history")).StatusCode);
    }

    [Fact]
    public async Task PayrollRuns_IntegralE2E_CarryoverReleaseAndSlips()
    {
        var scenario = await factory.ResetDatabaseAsync();
        // The manager also registers the TNT input (REQ-011 grants on top of the payroll ones).
        using var manager = factory.CreateClientFor(TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            PersonnelFilePermissionCodes.Admin,
            PersonnelFilePermissionCodes.ManageNotWorkedTimeTypes,
            PersonnelFilePermissionCodes.ManageNotWorkedTimes,
            PersonnelFilePermissionCodes.ViewNotWorkedTimes,
            PayrollConfigurationPermissionCodes.Manage,
            LeaveConfigurationPermissionCodes.Admin));
        using var authorizer = factory.CreateClientFor(PayrollRunAuthorizerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedPayrollRunCandidateAsync(
            scenario.TenantId, "Rezago", "Integral", "EMP-PRUN-K", "rezago.prun.k@empresa.test");
        var (definitionId, periodOneId) = await CreatePayrollDefinitionWithCalendarAsync(manager, scenario.TenantId);

        // A LAGGED not-worked time (REQ-014 P-03): Mon-Wed BEFORE the 2026 calendar even starts.
        await LoadNotWorkedTimeTemplateAsync(manager, scenario.TenantId);
        var lagged = await CreateNotWorkedTimeAsync(manager, fileId, new
        {
            typeCode = "AUSENCIA_SIN_GOCE",
            assignedPositionPublicId = (Guid?)null,
            startDate = "2025-12-01",
            endDate = "2025-12-03",
            hours = (decimal?)null,
            reason = "rezago histórico",
        });
        var laggedId = lagged.GetProperty("notWorkedTimePublicId").GetGuid();
        var laggedAmount = lagged.GetProperty("discountAmount").GetDecimal();
        Assert.True(laggedAmount > 0m);

        Guid periodTwoId;
        {
            var periods = await manager.GetAsync(
                $"/api/v1/companies/{scenario.TenantId}/payroll-periods?payPeriodTypeCode=QUINCENAL&year=2026&pageSize=30");
            periods.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await periods.Content.ReadAsStringAsync());
            periodTwoId = doc.RootElement.GetProperty("items").EnumerateArray()
                .Single(item => item.GetProperty("number").GetInt32() == 2 &&
                                item.GetProperty("payrollDefinitionPublicId").GetGuid() == definitionId)
                .GetProperty("publicId").GetGuid();
        }

        // Run 1 CARRIES the lagged input (warning + line bound to the source record).
        var generateOne = await GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, periodOneId);
        var generateOnePayload = await generateOne.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == generateOne.StatusCode, $"generate 1: {(int)generateOne.StatusCode} {generateOnePayload}");
        Guid runOneId;
        using (var doc = JsonDocument.Parse(generateOnePayload))
        {
            runOneId = doc.RootElement.GetProperty("publicId").GetGuid();
            Assert.Contains(
                doc.RootElement.GetProperty("warnings").EnumerateArray(),
                warning => warning.GetProperty("code").GetString() == "PAYROLL_WARNING_CARRYOVER_INPUT");
        }

        Guid laggedLineId;
        using (var lines = await ReadEmployeeLinesAsync(manager, scenario.TenantId, runOneId, fileId))
        {
            var laggedLine = lines.RootElement.GetProperty("lines").EnumerateArray()
                .Single(line => line.GetProperty("sourceModule").GetString() == "NOT_WORKED_TIME");
            laggedLineId = laggedLine.GetProperty("publicId").GetGuid();
            Assert.Equal(laggedId, laggedLine.GetProperty("sourceReferencePublicId").GetGuid());
            Assert.Equal(laggedAmount, laggedLine.GetProperty("finalAmount").GetDecimal());
        }

        // While run 1 (active) consumes it, run 2 must NOT carry it — never in two active runs.
        var generateTwo = await GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, periodTwoId);
        Assert.Equal(HttpStatusCode.Created, generateTwo.StatusCode);
        Guid runTwoId;
        using (var doc = JsonDocument.Parse(await generateTwo.Content.ReadAsStringAsync()))
        {
            runTwoId = doc.RootElement.GetProperty("publicId").GetGuid();
        }

        using (var lines = await ReadEmployeeLinesAsync(manager, scenario.TenantId, runTwoId, fileId))
        {
            Assert.DoesNotContain(
                lines.RootElement.GetProperty("lines").EnumerateArray(),
                line => line.GetProperty("sourceModule").GetString() == "NOT_WORKED_TIME");
        }

        // Release the lag: EXCLUDE its line in run 1 (REQ-014 liberación) and clear run 2's slot…
        var (_, tokenOne, _, _) = await ReadRunAsync(manager, scenario.TenantId, runOneId);
        var exclude = await PatchPayrollRunAsync(
            manager,
            $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runOneId}/lines/{laggedLineId}",
            tokenOne,
            new { isIncluded = false });
        Assert.Equal(HttpStatusCode.OK, exclude.StatusCode);

        var (_, tokenTwo, _, _) = await ReadRunAsync(manager, scenario.TenantId, runTwoId);
        var annulTwo = await PatchPayrollRunAsync(
            manager, $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runTwoId}/annulment", tokenTwo,
            new { reason = "Regenerar con el rezago liberado" });
        Assert.Equal(HttpStatusCode.OK, annulTwo.StatusCode);

        // …and the NEXT generation of period 2 re-carries it (re-arrastrable tras liberar).
        var regenerateTwo = await GeneratePayrollRunAsync(manager, scenario.TenantId, definitionId, periodTwoId);
        var regeneratePayload = await regenerateTwo.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == regenerateTwo.StatusCode, $"generate 2b: {(int)regenerateTwo.StatusCode} {regeneratePayload}");
        Guid runTwoBId;
        using (var doc = JsonDocument.Parse(regeneratePayload))
        {
            runTwoBId = doc.RootElement.GetProperty("publicId").GetGuid();
            Assert.Contains(
                doc.RootElement.GetProperty("warnings").EnumerateArray(),
                warning => warning.GetProperty("code").GetString() == "PAYROLL_WARNING_CARRYOVER_INPUT");
        }

        using (var lines = await ReadEmployeeLinesAsync(manager, scenario.TenantId, runTwoBId, fileId))
        {
            var laggedLine = lines.RootElement.GetProperty("lines").EnumerateArray()
                .Single(line => line.GetProperty("sourceModule").GetString() == "NOT_WORKED_TIME");
            Assert.Equal(laggedId, laggedLine.GetProperty("sourceReferencePublicId").GetGuid());
        }

        // Slips only exist for FINAL figures: 422 while GENERADA…
        var draftSlip = await manager.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runTwoBId}/employees/{fileId}/slip");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, draftSlip.StatusCode);
        using (var doc = JsonDocument.Parse(await draftSlip.Content.ReadAsStringAsync()))
        {
            Assert.Equal("PAYROLL_RUN_STATE_RULE_VIOLATION", doc.RootElement.GetProperty("code").GetString());
        }

        // …after authorizing: the individual PDF and the zip batch.
        var (_, tokenTwoB, _, _) = await ReadRunAsync(manager, scenario.TenantId, runTwoBId);
        var authorize = await PatchPayrollRunAsync(
            authorizer, $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runTwoBId}/authorization", tokenTwoB);
        Assert.Equal(HttpStatusCode.OK, authorize.StatusCode);

        var slip = await manager.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runTwoBId}/employees/{fileId}/slip");
        Assert.True(HttpStatusCode.OK == slip.StatusCode, $"slip: {(int)slip.StatusCode}");
        Assert.Equal("application/pdf", slip.Content.Headers.ContentType!.MediaType);
        var pdfBytes = await slip.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 4));

        var slips = await manager.GetAsync($"/api/v1/companies/{scenario.TenantId}/payroll-runs/{runTwoBId}/slips");
        Assert.True(HttpStatusCode.OK == slips.StatusCode, $"slips: {(int)slips.StatusCode}");
        Assert.Equal("application/zip", slips.Content.Headers.ContentType!.MediaType);
        var zipBytes = await slips.Content.ReadAsByteArrayAsync();
        Assert.Equal((byte)'P', zipBytes[0]);
        Assert.Equal((byte)'K', zipBytes[1]);
        using (var archive = new System.IO.Compression.ZipArchive(new MemoryStream(zipBytes)))
        {
            var entry = Assert.Single(archive.Entries);
            Assert.Equal("boleta-EMP-PRUN-K.pdf", entry.Name);
        }
    }
}
