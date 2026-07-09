using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Integration coverage for the recurring-income bandejas + exports slice (REQ-005 PR-5): the company bandeja with
/// per-status counts (RF-010), the pending / overdue installments bandeja (RF-011, projected + overdue), the
/// tabular exports (xlsx 200), and the PAYROLL INPUT (§5): it cuadra EXACTLY against the pending installments of
/// the same filter once applied (A.3-10), excludes suspended incomes and annulled installments, and demands the
/// mandatory date range (422 when missing). Reuses the CRUD / apply helpers from the sibling partials.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private async Task<JsonDocument> QueryRecurringIncomesBandejaAsync(HttpClient client, Guid companyId, object body)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/recurring-incomes/query", body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == response.StatusCode, $"Bandeja query failed: {(int)response.StatusCode} {payload}");
        return JsonDocument.Parse(payload);
    }

    private async Task<JsonDocument> QueryPendingInstallmentsAsync(HttpClient client, Guid companyId, object body)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/recurring-incomes/pending-installments/query", body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == response.StatusCode, $"Pending query failed: {(int)response.StatusCode} {payload}");
        return JsonDocument.Parse(payload);
    }

    private static async Task<Guid> ReadIncomeTokenAsync(HttpClient client, Guid fileId, Guid incomeId)
    {
        var response = await client.GetAsync($"/api/v1/personnel-files/{fileId}/recurring-incomes/{incomeId}");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("concurrencyToken").GetGuid();
    }

    [Fact]
    public async Task RecurringIncomesBandeja_WithMultipleStatuses_ReturnsStatusCountsAndFilters()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringIncomeManagerContext(scenario, Guid.NewGuid()));
        using var authorizer = factory.CreateClientFor(RecurringIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        // VIGENTE
        var fileVigente = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Vera", "Vigente", "EMP-RI-BAN-V", "vera.riban.v@empresa.test");
        await CreateAndAuthorizeVigenteIncomeAsync(manager, authorizer, fileVigente, FiniteRecurringIncomeBody());

        // EN_REVISION
        var fileRevision = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Eda", "Revision", "EMP-RI-BAN-E", "eda.riban.e@empresa.test");
        await CreateRecurringIncomeAsync(manager, fileRevision, FiniteRecurringIncomeBody());

        // RECHAZADO (reject via resolution)
        var fileRejected = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Rea", "Rechazada", "EMP-RI-BAN-R", "rea.riban.r@empresa.test");
        var (rejectedId, rejectedToken) = await CreateRecurringIncomeAsync(manager, fileRejected, FiniteRecurringIncomeBody());
        var reject = await PatchRecurringIncomeAsync(
            authorizer, fileRejected, rejectedId, "resolution", rejectedToken, new { targetStatusCode = "RECHAZADO", note = "No procede" });
        reject.EnsureSuccessStatusCode();

        // ANULADO (annul from EN_REVISION)
        var fileAnnulled = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Ana", "Anulada", "EMP-RI-BAN-A", "ana.riban.a@empresa.test");
        var (annulledId, annulledToken) = await CreateRecurringIncomeAsync(manager, fileAnnulled, FiniteRecurringIncomeBody());
        var annul = await PatchRecurringIncomeAsync(
            manager, fileAnnulled, annulledId, "annulment", annulledToken, new { reason = "Registro erróneo" });
        annul.EnsureSuccessStatusCode();

        // No status filter → every status listed; StatusCounts cover all four.
        using (var doc = await QueryRecurringIncomesBandejaAsync(manager, scenario.TenantId, new { }))
        {
            var root = doc.RootElement;
            Assert.Equal(4, root.GetProperty("totalCount").GetInt32());
            var counts = root.GetProperty("statusCounts");
            Assert.Equal(1, counts.GetProperty("VIGENTE").GetInt32());
            Assert.Equal(1, counts.GetProperty("EN_REVISION").GetInt32());
            Assert.Equal(1, counts.GetProperty("RECHAZADO").GetInt32());
            Assert.Equal(1, counts.GetProperty("ANULADO").GetInt32());
        }

        // Status filter narrows the items but the StatusCounts still cover every status.
        using (var doc = await QueryRecurringIncomesBandejaAsync(manager, scenario.TenantId, new { statusCode = "VIGENTE" }))
        {
            var root = doc.RootElement;
            Assert.Equal(1, root.GetProperty("totalCount").GetInt32());
            var items = root.GetProperty("items").EnumerateArray().ToArray();
            Assert.Single(items);
            Assert.Equal("VIGENTE", items[0].GetProperty("statusCode").GetString());
            Assert.Equal(4, root.GetProperty("statusCounts").GetProperty("ANULADO").GetInt32() +
                            root.GetProperty("statusCounts").GetProperty("VIGENTE").GetInt32() +
                            root.GetProperty("statusCounts").GetProperty("EN_REVISION").GetInt32() +
                            root.GetProperty("statusCounts").GetProperty("RECHAZADO").GetInt32());
        }
    }

    [Fact]
    public async Task RecurringIncomesBandeja_Export_ReturnsSpreadsheet()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringIncomeManagerContext(scenario, Guid.NewGuid()));
        using var authorizer = factory.CreateClientFor(RecurringIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Xio", "Export", "EMP-RI-BAN-X", "xio.riban.x@empresa.test");
        await CreateAndAuthorizeVigenteIncomeAsync(manager, authorizer, fileId, FiniteRecurringIncomeBody());

        var xlsx = await manager.GetAsync($"/api/v1/companies/{scenario.TenantId}/recurring-incomes/export?format=xlsx");
        Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            xlsx.Content.Headers.ContentType?.MediaType);

        // JSON variant → one row for the VIGENTE income with the Spanish headers.
        var json = await manager.GetAsync($"/api/v1/companies/{scenario.TenantId}/recurring-incomes/export?format=json");
        Assert.Equal(HttpStatusCode.OK, json.StatusCode);
        using var doc = JsonDocument.Parse(await json.Content.ReadAsStringAsync());
        var rows = doc.RootElement.EnumerateArray().ToArray();
        Assert.Single(rows);
        Assert.Equal("VIGENTE", rows[0].GetProperty("Estado").GetString());
        Assert.Equal("AYUDA_ALIMENTACION", rows[0].GetProperty("Tipo").GetString());
    }

    [Fact]
    public async Task RecurringIncomesPendingInstallments_ProjectsPendingAndOverdue()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringIncomeManagerContext(scenario, Guid.NewGuid()));
        using var authorizer = factory.CreateClientFor(RecurringIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Pia", "Pendiente", "EMP-RI-PEND-A", "pia.ripend.a@empresa.test");

        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-40);
        await CreateAndAuthorizeVigenteIncomeAsync(manager, authorizer, fileId, MonthlyIncomeBodyStarting(start, 100, 3));

        // Cutoff wide enough that all three monthly installments project; #1/#2 are overdue, #3 is future.
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60).ToString("yyyy-MM-dd");
        using var doc = await QueryPendingInstallmentsAsync(manager, scenario.TenantId, new
        {
            payrollTypeCode = "MENSUAL",
            cutoffDate = cutoff
        });

        var root = doc.RootElement;
        Assert.Equal(3, root.GetProperty("totalCount").GetInt32());
        var items = root.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(3, items.Length);

        var overdue = items.Where(item => item.GetProperty("isOverdue").GetBoolean()).ToArray();
        var projected = items.Where(item => !item.GetProperty("isOverdue").GetBoolean()).ToArray();
        Assert.Equal(2, overdue.Length);
        Assert.Single(projected);
        Assert.All(items, item => Assert.Equal(100m, item.GetProperty("amount").GetDecimal()));
        Assert.Equal(3, projected[0].GetProperty("installmentNumber").GetInt32());
    }

    [Fact]
    public async Task RecurringIncomesPayrollInput_CuadraAgainstPendingOfTheSameFilter()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringIncomeManagerContext(scenario, Guid.NewGuid()));
        using var authorizer = factory.CreateClientFor(RecurringIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var file1 = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Uno", "Insumo", "EMP-RI-INS-1", "uno.riins.1@empresa.test");
        var file2 = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Dos", "Insumo", "EMP-RI-INS-2", "dos.riins.2@empresa.test");

        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-40);
        await CreateAndAuthorizeVigenteIncomeAsync(manager, authorizer, file1, MonthlyIncomeBodyStarting(start, 40, 3));
        await CreateAndAuthorizeVigenteIncomeAsync(manager, authorizer, file2, MonthlyIncomeBodyStarting(start, 60, 2));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cutoff = today.ToString("yyyy-MM-dd");

        // Pending of the filter BEFORE applying (count + amount total).
        int pendingCount;
        decimal pendingSum;
        using (var doc = await QueryPendingInstallmentsAsync(manager, scenario.TenantId, new { payrollTypeCode = "MENSUAL", cutoffDate = cutoff }))
        {
            var items = doc.RootElement.GetProperty("items").EnumerateArray().ToArray();
            pendingCount = items.Length;
            pendingSum = items.Sum(item => item.GetProperty("amount").GetDecimal());
        }
        Assert.Equal(4, pendingCount);
        Assert.Equal(200m, pendingSum);

        // Apply the period → exactly the pending installments become APLICADA.
        var batch = await ApplyPeriodAsync(manager, scenario.TenantId, new
        {
            payrollTypeCode = "MENSUAL",
            cutoffDate = cutoff,
            excludedIncomePublicIds = Array.Empty<Guid>()
        });
        batch.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await batch.Content.ReadAsStringAsync()))
        {
            Assert.Equal(pendingCount, doc.RootElement.GetProperty("aplicadas").GetInt32());
        }

        // Payroll input over the same filter/range cuadra EXACTLY against the (now applied) pending set.
        var startDate = today.AddDays(-1).ToString("yyyy-MM-dd");
        var endDate = today.AddDays(1).ToString("yyyy-MM-dd");
        var input = await manager.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/recurring-incomes/payroll-input/export?format=json&payrollTypeCode=MENSUAL&startDate={startDate}&endDate={endDate}");
        Assert.Equal(HttpStatusCode.OK, input.StatusCode);
        using (var doc = JsonDocument.Parse(await input.Content.ReadAsStringAsync()))
        {
            var rows = doc.RootElement.EnumerateArray().ToArray();
            Assert.Equal(pendingCount, rows.Length);
            Assert.Equal(pendingSum, rows.Sum(row => row.GetProperty("Monto").GetDecimal()));
            Assert.All(rows, row => Assert.Equal("MENSUAL", row.GetProperty("PayrollType").GetString()));
        }

        // After applying, the pending bandeja of the same filter is drained of those installments.
        using (var doc = await QueryPendingInstallmentsAsync(manager, scenario.TenantId, new { payrollTypeCode = "MENSUAL", cutoffDate = cutoff }))
        {
            Assert.Equal(0, doc.RootElement.GetProperty("totalCount").GetInt32());
        }
    }

    [Fact]
    public async Task RecurringIncomesPayrollInput_ExcludesSuspendedIncomesAndAnnulledInstallments()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringIncomeManagerContext(scenario, Guid.NewGuid()));
        using var authorizer = factory.CreateClientFor(RecurringIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var fileActive = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Act", "Insumo", "EMP-RI-INSX-A", "act.riinsx.a@empresa.test");
        var fileSuspended = await SeedRecurringIncomeCandidateAsync(
            scenario.TenantId, "Sus", "Insumo", "EMP-RI-INSX-S", "sus.riinsx.s@empresa.test");

        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-40);
        var (activeIncomeId, _) = await CreateAndAuthorizeVigenteIncomeAsync(manager, authorizer, fileActive, MonthlyIncomeBodyStarting(start, 50, 3));
        var (suspendedIncomeId, suspendedToken) = await CreateAndAuthorizeVigenteIncomeAsync(manager, authorizer, fileSuspended, MonthlyIncomeBodyStarting(start, 50, 3));

        // Suspend the second income → it contributes nothing to the batch nor the input.
        var suspend = await PatchRecurringIncomeAsync(
            manager, fileSuspended, suspendedIncomeId, "suspension", suspendedToken, new { suspend = true, note = "Pausa" });
        suspend.EnsureSuccessStatusCode();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cutoff = today.ToString("yyyy-MM-dd");
        var batch = await ApplyPeriodAsync(manager, scenario.TenantId, new
        {
            payrollTypeCode = "MENSUAL",
            cutoffDate = cutoff,
            excludedIncomePublicIds = Array.Empty<Guid>()
        });
        batch.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await batch.Content.ReadAsStringAsync()))
        {
            // Only the active income's 2 due installments apply (suspended income skipped).
            Assert.Equal(2, doc.RootElement.GetProperty("aplicadas").GetInt32());
        }

        // Annul the first applied installment of the active income → it drops out of the input.
        var historyResponse = await manager.GetAsync($"/api/v1/personnel-files/{fileActive}/recurring-incomes/{activeIncomeId}/installments");
        historyResponse.EnsureSuccessStatusCode();
        Guid installmentToAnnul;
        using (var doc = JsonDocument.Parse(await historyResponse.Content.ReadAsStringAsync()))
        {
            installmentToAnnul = doc.RootElement.GetProperty("items").EnumerateArray()
                .First(item => item.GetProperty("installmentNumber").GetInt32() == 1)
                .GetProperty("installmentPublicId").GetGuid();
        }

        var incomeToken = await ReadIncomeTokenAsync(manager, fileActive, activeIncomeId);
        using var annulRequest = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/personnel-files/{fileActive}/recurring-incomes/{activeIncomeId}/installments/{installmentToAnnul}/annulment")
        {
            Content = JsonContent.Create(new { reason = "Cuota mal aplicada" })
        };
        annulRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{incomeToken}\"");
        var annulResponse = await manager.SendAsync(annulRequest);
        annulResponse.EnsureSuccessStatusCode();

        var startDate = today.AddDays(-1).ToString("yyyy-MM-dd");
        var endDate = today.AddDays(1).ToString("yyyy-MM-dd");
        var input = await manager.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/recurring-incomes/payroll-input/export?format=json&payrollTypeCode=MENSUAL&startDate={startDate}&endDate={endDate}");
        Assert.Equal(HttpStatusCode.OK, input.StatusCode);
        using (var doc = JsonDocument.Parse(await input.Content.ReadAsStringAsync()))
        {
            var rows = doc.RootElement.EnumerateArray().ToArray();
            // Only installment #2 of the active income survives (suspended income + annulled installment excluded).
            Assert.Single(rows);
            Assert.Equal("EMP-RI-INSX-A", rows[0].GetProperty("CodigoEmpleado").GetString());
            Assert.Equal(2, rows[0].GetProperty("NumeroCuota").GetInt32());
        }
    }

    [Fact]
    public async Task RecurringIncomesPayrollInput_MissingRange_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringIncomeManagerContext(scenario, Guid.NewGuid()));

        var missing = await manager.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/recurring-incomes/payroll-input/export?format=json&payrollTypeCode=MENSUAL");
        await AssertProblemDetailsAsync(missing, HttpStatusCode.UnprocessableEntity, "RECURRING_INCOME_PAYROLL_INPUT_RANGE_REQUIRED");
    }
}
