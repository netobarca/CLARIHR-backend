using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Domain.Leave;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Integration coverage for the recurring-income installment slice (REQ-005 PR-4): the derived schedule, the
/// UNITARY application (RF-006, amount from rules, plan finalization), the company-wide apply-period batch
/// (RF-007: postponement via exclusion, catch-up of overdue installments, atomicity), the CARRERA (two concurrent
/// apply-period runs → no duplicate installments — the advisory lock + partial-unique index net), and the
/// annulment-with-reopening (RF-008: FINALIZADO → VIGENTE → re-apply → FINALIZADO). Golden cases from Anexo A.3.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private async Task<(Guid IncomeId, Guid Token)> CreateAndAuthorizeVigenteIncomeAsync(
        HttpClient manager, HttpClient authorizer, Guid fileId, object body)
    {
        var (incomeId, createToken) = await CreateRecurringIncomeAsync(manager, fileId, body);
        var authorizeResponse = await PatchRecurringIncomeAsync(
            authorizer, fileId, incomeId, "resolution", createToken, new { targetStatusCode = "VIGENTE", note = (string?)null });
        authorizeResponse.EnsureSuccessStatusCode();
        return (incomeId, await ReadTokenAsync(authorizeResponse));
    }

    private static async Task<HttpResponseMessage> ApplyInstallmentAsync(
        HttpClient client, Guid fileId, Guid incomeId, Guid incomeToken, object? body = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/personnel-files/{fileId}/recurring-incomes/{incomeId}/installments")
        {
            Content = JsonContent.Create(body ?? new { })
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{incomeToken}\"");
        return await client.SendAsync(request);
    }

    private static async Task<(Guid InstallmentId, int Number, decimal Amount, string IncomeStatus, Guid IncomeToken)>
        ApplyNextInstallmentAsync(HttpClient client, Guid fileId, Guid incomeId, Guid incomeToken, object? body = null)
    {
        var response = await ApplyInstallmentAsync(client, fileId, incomeId, incomeToken, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, $"Apply failed: {(int)response.StatusCode} {payload}");
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var installment = root.GetProperty("installment");
        return (
            installment.GetProperty("installmentPublicId").GetGuid(),
            installment.GetProperty("installmentNumber").GetInt32(),
            installment.GetProperty("amount").GetDecimal(),
            root.GetProperty("recurringIncomeStatusCode").GetString()!,
            root.GetProperty("recurringIncomeConcurrencyToken").GetGuid());
    }

    private Task<HttpResponseMessage> ApplyPeriodAsync(HttpClient client, Guid companyId, object body) =>
        client.PostAsJsonAsync($"/api/v1/companies/{companyId}/recurring-incomes/apply-period", body);

    private async Task<Guid> SeedPayrollPeriodAsync(
        Guid tenantId, string payPeriodTypeCode, int year, int number, DateOnly start, DateOnly end)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var period = PayrollPeriodDefinition.Create(payPeriodTypeCode, year, number, $"{payPeriodTypeCode} {year}-{number:00}", start, end);
        period.SetTenantId(tenantId);
        dbContext.Set<PayrollPeriodDefinition>().Add(period);
        await dbContext.SaveChangesAsync();
        return period.PublicId;
    }

    private static async Task<JsonDocument> GetScheduleAsync(HttpClient client, Guid fileId, Guid incomeId)
    {
        var response = await client.GetAsync($"/api/v1/personnel-files/{fileId}/recurring-incomes/{incomeId}/schedule");
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static async Task<JsonDocument> GetInstallmentHistoryAsync(HttpClient client, Guid fileId, Guid incomeId)
    {
        var response = await client.GetAsync($"/api/v1/personnel-files/{fileId}/recurring-incomes/{incomeId}/installments");
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RecurringIncomeInstallments_A31_ApplySixInstallments_FinalizesWithZeroBalance()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringIncomeManagerContext(scenario, Guid.NewGuid()));
        using var authorizer = factory.CreateClientFor(RecurringIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Sara", "SeisCuotas", "EMP-RI-INST-A", "sara.riinst.a@empresa.test");

        var (incomeId, token) = await CreateAndAuthorizeVigenteIncomeAsync(
            manager, authorizer, fileId, FiniteRecurringIncomeBody(installmentValue: 50, installmentCount: 6));

        // A payroll period imputes the FIRST installment (snapshot label + FK); the rest go against the date only.
        var periodId = await SeedPayrollPeriodAsync(
            scenario.TenantId, "MENSUAL", 2026, 7, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));

        var status = "VIGENTE";
        for (var i = 1; i <= 6; i++)
        {
            object? body = i == 1 ? new { payrollPeriodPublicId = periodId } : null;
            var applied = await ApplyNextInstallmentAsync(manager, fileId, incomeId, token, body);
            Assert.Equal(i, applied.Number);
            Assert.Equal(50m, applied.Amount);
            token = applied.IncomeToken;
            status = applied.IncomeStatus;
        }

        Assert.Equal("FINALIZADO", status);

        using var schedule = await GetScheduleAsync(manager, fileId, incomeId);
        Assert.Equal(0m, schedule.RootElement.GetProperty("remainingAmount").GetDecimal());
        Assert.True(schedule.RootElement.GetProperty("isPlanComplete").GetBoolean());
        Assert.All(schedule.RootElement.GetProperty("installments").EnumerateArray(), item => Assert.True(item.GetProperty("isApplied").GetBoolean()));

        // The first installment carries the payroll-period snapshot (FK + label).
        using var history = await GetInstallmentHistoryAsync(manager, fileId, incomeId);
        var first = history.RootElement.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("installmentNumber").GetInt32() == 1);
        Assert.Equal(periodId, first.GetProperty("payrollPeriodPublicId").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(first.GetProperty("payrollPeriodLabel").GetString()));
        Assert.Equal(6, history.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task RecurringIncomeInstallments_A32_LastInstallmentAbsorbsRoundingAdjustment()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringIncomeManagerContext(scenario, Guid.NewGuid()));
        using var authorizer = factory.CreateClientFor(RecurringIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Ana", "Ajuste", "EMP-RI-INST-B", "ana.riinst.b@empresa.test");

        // value 33.33 + total 100 → count 3; installment 3 absorbs the remainder = 33.34.
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var body = new
        {
            registrationDate = today,
            reference = "AJUSTE",
            recurringIncomeTypeCode = "AYUDA_ALIMENTACION",
            conceptTypeCode = "BONO",
            observations = (string?)null,
            assignedPositionPublicId = (Guid?)null,
            installmentStartDate = today,
            currencyCode = "USD",
            payrollTypeCode = "MENSUAL",
            installmentFrequencyCode = "MENSUAL",
            isIndefinite = false,
            installmentValue = 33.33m,
            installmentCount = (int?)null,
            totalAmount = (decimal?)100m,
            settlementActionCode = "PAGAR_SALDO"
        };

        var (incomeId, token) = await CreateAndAuthorizeVigenteIncomeAsync(manager, authorizer, fileId, body);

        var a1 = await ApplyNextInstallmentAsync(manager, fileId, incomeId, token);
        Assert.Equal(33.33m, a1.Amount);
        var a2 = await ApplyNextInstallmentAsync(manager, fileId, incomeId, a1.IncomeToken);
        Assert.Equal(33.33m, a2.Amount);
        var a3 = await ApplyNextInstallmentAsync(manager, fileId, incomeId, a2.IncomeToken);
        Assert.Equal(33.34m, a3.Amount);
        Assert.Equal("FINALIZADO", a3.IncomeStatus);
    }

    [Fact]
    public async Task RecurringIncomeInstallments_A34_ExcludedIncomeIsPostponedAndAppliedNextBatch()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringIncomeManagerContext(scenario, Guid.NewGuid()));
        using var authorizer = factory.CreateClientFor(RecurringIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var file1 = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Uno", "Puntual", "EMP-RI-INST-C1", "uno.riinst.c@empresa.test");
        var file2 = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Dos", "Pospuesto", "EMP-RI-INST-C2", "dos.riinst.c@empresa.test");

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5);
        var (income1, _) = await CreateAndAuthorizeVigenteIncomeAsync(manager, authorizer, file1, MonthlyIncomeBodyStarting(startDate, 40, 3));
        var (income2, _) = await CreateAndAuthorizeVigenteIncomeAsync(manager, authorizer, file2, MonthlyIncomeBodyStarting(startDate, 40, 3));

        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        // Batch 1: exclude income2 → only income1's installment 1 applies; income2 is postponed.
        var batch1 = await ApplyPeriodAsync(manager, scenario.TenantId, new
        {
            payrollTypeCode = "MENSUAL",
            cutoffDate = cutoff,
            excludedIncomePublicIds = new[] { income2 }
        });
        var batch1Payload = await batch1.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == batch1.StatusCode, batch1Payload);
        using (var doc = JsonDocument.Parse(batch1Payload))
        {
            Assert.Equal(1, doc.RootElement.GetProperty("aplicadas").GetInt32());
            Assert.Equal(1, doc.RootElement.GetProperty("pospuestas").GetInt32());
        }

        // income2 has NOTHING applied yet.
        using (var history2 = await GetInstallmentHistoryAsync(manager, file2, income2))
        {
            Assert.Equal(0, history2.RootElement.GetProperty("totalCount").GetInt32());
        }

        // Batch 2: no exclusions → income2's still-due installment 1 catches up (theoretical due = its start).
        var batch2 = await ApplyPeriodAsync(manager, scenario.TenantId, new
        {
            payrollTypeCode = "MENSUAL",
            cutoffDate = cutoff,
            excludedIncomePublicIds = Array.Empty<Guid>()
        });
        var batch2Payload = await batch2.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == batch2.StatusCode, batch2Payload);
        using (var doc = JsonDocument.Parse(batch2Payload))
        {
            Assert.Equal(1, doc.RootElement.GetProperty("aplicadas").GetInt32());
            Assert.Equal(0, doc.RootElement.GetProperty("pospuestas").GetInt32());
        }

        using var history2b = await GetInstallmentHistoryAsync(manager, file2, income2);
        var item = history2b.RootElement.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(1, item.GetProperty("installmentNumber").GetInt32());
        Assert.Equal(startDate.ToString("yyyy-MM-dd"), item.GetProperty("theoreticalDueDate").GetString());
    }

    [Fact]
    public async Task RecurringIncomeInstallments_A35_ConcurrentApplyPeriod_NoDuplicateInstallments()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringIncomeManagerContext(scenario, Guid.NewGuid()));
        using var authorizer = factory.CreateClientFor(RecurringIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Kara", "Carrera", "EMP-RI-INST-D", "kara.riinst.d@empresa.test");
        var (incomeId, _) = await CreateAndAuthorizeVigenteIncomeAsync(
            manager, authorizer, fileId, FiniteRecurringIncomeBody(installmentValue: 50, installmentCount: 3));

        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        object batchBody() => new { payrollTypeCode = "MENSUAL", cutoffDate = cutoff, excludedIncomePublicIds = Array.Empty<Guid>() };

        // Two concurrent apply-period runs of the SAME filter: the advisory lock serializes them; the partial
        // unique index is the final net → the same installment is applied exactly once (no 500).
        var runA = ApplyPeriodAsync(manager, scenario.TenantId, batchBody());
        var runB = ApplyPeriodAsync(manager, scenario.TenantId, batchBody());
        var responses = await Task.WhenAll(runA, runB);

        foreach (var response in responses)
        {
            Assert.True(
                response.StatusCode is HttpStatusCode.OK or HttpStatusCode.UnprocessableEntity,
                $"Unexpected status {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        // Exactly one installment applied across both runs (the income has ONE active APLICADA installment #1).
        using var history = await GetInstallmentHistoryAsync(manager, fileId, incomeId);
        var applied = history.RootElement.GetProperty("items").EnumerateArray()
            .Where(item => item.GetProperty("statusCode").GetString() == "APLICADA")
            .ToArray();
        Assert.Single(applied);
        Assert.Equal(1, applied[0].GetProperty("installmentNumber").GetInt32());

        // Any conflicting run surfaces the conflict code (not a 500).
        var conflicts = responses.Where(response => response.StatusCode == HttpStatusCode.UnprocessableEntity).ToArray();
        if (conflicts.Length > 0)
        {
            await AssertProblemDetailsAsync(conflicts[0], HttpStatusCode.UnprocessableEntity, "RECURRING_INCOME_APPLY_PERIOD_CONFLICT");
        }
    }

    [Fact]
    public async Task RecurringIncomeInstallments_A36_AnnulLastInstallment_ReopensThenReapply_Finalizes()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringIncomeManagerContext(scenario, Guid.NewGuid()));
        using var authorizer = factory.CreateClientFor(RecurringIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Rita", "Reapertura", "EMP-RI-INST-E", "rita.riinst.e@empresa.test");
        var (incomeId, token) = await CreateAndAuthorizeVigenteIncomeAsync(
            manager, authorizer, fileId, FiniteRecurringIncomeBody(installmentValue: 25, installmentCount: 2));

        var a1 = await ApplyNextInstallmentAsync(manager, fileId, incomeId, token);
        var a2 = await ApplyNextInstallmentAsync(manager, fileId, incomeId, a1.IncomeToken);
        Assert.Equal("FINALIZADO", a2.IncomeStatus);

        // Annul the last installment (#2) → the plan is no longer complete → income reopens to VIGENTE.
        using var annulRequest = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/personnel-files/{fileId}/recurring-incomes/{incomeId}/installments/{a2.InstallmentId}/annulment")
        {
            Content = JsonContent.Create(new { reason = "Cuota mal aplicada" })
        };
        annulRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{a2.IncomeToken}\"");
        var annulResponse = await manager.SendAsync(annulRequest);
        var annulPayload = await annulResponse.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == annulResponse.StatusCode, annulPayload);

        Guid reopenedToken;
        using (var doc = JsonDocument.Parse(annulPayload))
        {
            Assert.Equal("ANULADA", doc.RootElement.GetProperty("installment").GetProperty("statusCode").GetString());
            Assert.Equal("VIGENTE", doc.RootElement.GetProperty("recurringIncomeStatusCode").GetString());
            reopenedToken = doc.RootElement.GetProperty("recurringIncomeConcurrencyToken").GetGuid();
        }

        // Re-apply installment #2 (the partial unique index allows the number to be re-used) → FINALIZADO again.
        var reapplied = await ApplyNextInstallmentAsync(manager, fileId, incomeId, reopenedToken);
        Assert.Equal(2, reapplied.Number);
        Assert.Equal("FINALIZADO", reapplied.IncomeStatus);
    }

    [Fact]
    public async Task RecurringIncomeInstallments_SuspendedIncome_NotIncludedInApplyPeriodBatch()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringIncomeManagerContext(scenario, Guid.NewGuid()));
        using var authorizer = factory.CreateClientFor(RecurringIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Sofi", "Suspendida", "EMP-RI-INST-F", "sofi.riinst.f@empresa.test");
        var (incomeId, vigenteToken) = await CreateAndAuthorizeVigenteIncomeAsync(
            manager, authorizer, fileId, FiniteRecurringIncomeBody(installmentValue: 50, installmentCount: 3));

        var suspend = await PatchRecurringIncomeAsync(
            manager, fileId, incomeId, "suspension", vigenteToken, new { suspend = true, note = "Pausa" });
        suspend.EnsureSuccessStatusCode();

        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var batch = await ApplyPeriodAsync(manager, scenario.TenantId, new
        {
            payrollTypeCode = "MENSUAL",
            cutoffDate = cutoff,
            excludedIncomePublicIds = Array.Empty<Guid>()
        });
        batch.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await batch.Content.ReadAsStringAsync()))
        {
            Assert.Equal(0, doc.RootElement.GetProperty("aplicadas").GetInt32());
        }

        using var history = await GetInstallmentHistoryAsync(manager, fileId, incomeId);
        Assert.Equal(0, history.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task RecurringIncomeInstallments_Schedule_ProjectsAppliedProjectedAndBalance()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringIncomeManagerContext(scenario, Guid.NewGuid()));
        using var authorizer = factory.CreateClientFor(RecurringIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Pia", "Proyeccion", "EMP-RI-INST-G", "pia.riinst.g@empresa.test");
        var (incomeId, token) = await CreateAndAuthorizeVigenteIncomeAsync(
            manager, authorizer, fileId, FiniteRecurringIncomeBody(installmentValue: 50, installmentCount: 3));

        _ = await ApplyNextInstallmentAsync(manager, fileId, incomeId, token);

        using var schedule = await GetScheduleAsync(manager, fileId, incomeId);
        var root = schedule.RootElement;
        Assert.Equal(2, root.GetProperty("nextInstallmentNumber").GetInt32());
        Assert.Equal(100m, root.GetProperty("remainingAmount").GetDecimal());
        Assert.False(root.GetProperty("isPlanComplete").GetBoolean());

        var items = root.GetProperty("installments").EnumerateArray().ToArray();
        Assert.Equal(3, items.Length);
        Assert.True(items[0].GetProperty("isApplied").GetBoolean());
        Assert.False(items[1].GetProperty("isApplied").GetBoolean());
        Assert.False(items[2].GetProperty("isApplied").GetBoolean());
    }

    private static object MonthlyIncomeBodyStarting(DateOnly startDate, int installmentValue, int installmentCount)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        return new
        {
            registrationDate = today,
            reference = "LOTE",
            recurringIncomeTypeCode = "AYUDA_ALIMENTACION",
            conceptTypeCode = "BONO",
            observations = (string?)null,
            assignedPositionPublicId = (Guid?)null,
            installmentStartDate = startDate.ToString("yyyy-MM-dd"),
            currencyCode = "USD",
            payrollTypeCode = "MENSUAL",
            installmentFrequencyCode = "MENSUAL",
            isIndefinite = false,
            installmentValue,
            installmentCount = (int?)installmentCount,
            totalAmount = (decimal?)null,
            settlementActionCode = "PAGAR_SALDO"
        };
    }
}
