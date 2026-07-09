using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Integration coverage for the one-time-income bandejas + exports slice (REQ-006 PR-5): the advanced search with
/// per-status counts + amount totals BY CURRENCY (RF-008), the 8-dimension aggregation (№14) — which CUADRA
/// against the flat search of the same filter and never sums across currencies (RN-13) — the invalid-dimension
/// guard (400), the tabular exports (xlsx 200 + json headers), and the PAYROLL INPUT (§5): it demands the
/// mandatory payroll type + period (400 when missing) and cuadra EXACTLY against the pending tray of the same
/// filter (excludes annulled + applied). Reuses the CRUD / apply helpers from the sibling partials.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static object OneTimeIncomeCurrencyBody(
        Guid requesterFilePublicId,
        decimal amount,
        string currencyCode,
        string conceptTypeCode = "BONO",
        string payrollTypeCode = "QUINCENAL",
        string payrollPeriodLabel = "Quincena 13/2026",
        string? incomeDate = null)
    {
        var date = incomeDate ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        return new
        {
            incomeDate = date,
            reference = "REF-BAN",
            conceptTypeCode,
            observations = (string?)null,
            isFixedValue = true,
            calculationMethod = (string?)null,
            quantity = (decimal?)null,
            unitValue = (decimal?)null,
            multiplier = (decimal?)null,
            percentage = (decimal?)null,
            baseAmount = (decimal?)null,
            amount = (decimal?)amount,
            currencyCode,
            assignedPositionPublicId = (Guid?)null,
            requesterFilePublicId,
            payrollTypeCode,
            payrollPeriodPublicId = (Guid?)null,
            payrollPeriodLabel,
            payrollPeriodEndDate = (string?)null
        };
    }

    private async Task<JsonDocument> QueryOneTimeIncomesBandejaAsync(HttpClient client, Guid companyId, object body)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/one-time-incomes/query", body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == response.StatusCode, $"Bandeja query failed: {(int)response.StatusCode} {payload}");
        return JsonDocument.Parse(payload);
    }

    [Fact]
    public async Task OneTimeIncomesBandeja_MultipleStatuses_ReturnsStatusCountsTotalsAndFilters()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario));
        using var authorizer = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Sol", "Solicitante", "EMP-OTI-BAN-REQ", "sol.otiban.req@empresa.test");

        // AUTORIZADO (USD 100)
        var fileAuth = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Ada", "Autorizada", "EMP-OTI-BAN-A", "ada.otiban.a@empresa.test");
        await CreateAndAuthorizeOneTimeIncomeAsync(manager, authorizer, fileAuth, OneTimeIncomeCurrencyBody(requesterId, 100m, "USD"));

        // EN_REVISION (USD 40)
        var fileRev = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Eda", "Revision", "EMP-OTI-BAN-E", "eda.otiban.e@empresa.test");
        await CreateOneTimeIncomeAsync(manager, fileRev, OneTimeIncomeCurrencyBody(requesterId, 40m, "USD"));

        // RECHAZADO
        var fileRej = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Rea", "Rechazada", "EMP-OTI-BAN-R", "rea.otiban.r@empresa.test");
        var (rejId, rejToken) = await CreateOneTimeIncomeAsync(manager, fileRej, OneTimeIncomeCurrencyBody(requesterId, 33m, "USD"));
        var reject = await PatchOneTimeIncomeAsync(authorizer, fileRej, rejId, "resolution", rejToken, new { targetStatusCode = "RECHAZADO", note = "No procede" });
        reject.EnsureSuccessStatusCode();

        // ANULADO (from EN_REVISION)
        var fileAnn = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Ana", "Anulada", "EMP-OTI-BAN-N", "ana.otiban.n@empresa.test");
        var (annId, annToken) = await CreateOneTimeIncomeAsync(manager, fileAnn, OneTimeIncomeCurrencyBody(requesterId, 22m, "USD"));
        var annul = await PatchOneTimeIncomeAsync(manager, fileAnn, annId, "annulment", annToken, new { reason = "Registro erróneo" });
        annul.EnsureSuccessStatusCode();

        // No status filter → every status listed; StatusCounts cover all four.
        using (var doc = await QueryOneTimeIncomesBandejaAsync(manager, scenario.TenantId, new { }))
        {
            var root = doc.RootElement;
            Assert.Equal(4, root.GetProperty("totalCount").GetInt32());
            var counts = root.GetProperty("statusCounts");
            Assert.Equal(1, counts.GetProperty("AUTORIZADO").GetInt32());
            Assert.Equal(1, counts.GetProperty("EN_REVISION").GetInt32());
            Assert.Equal(1, counts.GetProperty("RECHAZADO").GetInt32());
            Assert.Equal(1, counts.GetProperty("ANULADO").GetInt32());
            // TotalsByCurrency over the (unfiltered) set: 100 + 40 + 33 + 22 = 195 USD.
            Assert.Equal(195m, root.GetProperty("totalsByCurrency").GetProperty("USD").GetDecimal());
            // No groupBy → groups omitted / null.
            Assert.True(!root.TryGetProperty("groups", out var groups) || groups.ValueKind == JsonValueKind.Null);
        }

        // Status filter narrows the items + totals, but the StatusCounts still span every status.
        using (var doc = await QueryOneTimeIncomesBandejaAsync(manager, scenario.TenantId, new { statusCodes = new[] { "AUTORIZADO" } }))
        {
            var root = doc.RootElement;
            Assert.Equal(1, root.GetProperty("totalCount").GetInt32());
            var items = root.GetProperty("items").EnumerateArray().ToArray();
            Assert.Single(items);
            Assert.Equal("AUTORIZADO", items[0].GetProperty("statusCode").GetString());
            Assert.Equal(100m, root.GetProperty("totalsByCurrency").GetProperty("USD").GetDecimal());
            var counts = root.GetProperty("statusCounts");
            Assert.Equal(4, counts.GetProperty("AUTORIZADO").GetInt32()
                + counts.GetProperty("EN_REVISION").GetInt32()
                + counts.GetProperty("RECHAZADO").GetInt32()
                + counts.GetProperty("ANULADO").GetInt32());
        }
    }

    [Fact]
    public async Task OneTimeIncomesBandeja_GroupBy_CuadraAgainstFlatSearch_AndNeverCrossesCurrencies()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario));
        using var authorizer = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Gru", "Solicitante", "EMP-OTI-GRP-REQ", "gru.otigrp.req@empresa.test");

        // 3 AUTORIZADO incomes of the SAME payroll type across 2 currencies (USD 100 + USD 200 + EUR 50).
        var file1 = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Uno", "Grupo", "EMP-OTI-GRP-1", "uno.otigrp.1@empresa.test");
        var file2 = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Dos", "Grupo", "EMP-OTI-GRP-2", "dos.otigrp.2@empresa.test");
        var file3 = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Tri", "Grupo", "EMP-OTI-GRP-3", "tri.otigrp.3@empresa.test");
        await CreateAndAuthorizeOneTimeIncomeAsync(manager, authorizer, file1, OneTimeIncomeCurrencyBody(requesterId, 100m, "USD"));
        await CreateAndAuthorizeOneTimeIncomeAsync(manager, authorizer, file2, OneTimeIncomeCurrencyBody(requesterId, 200m, "USD"));
        await CreateAndAuthorizeOneTimeIncomeAsync(manager, authorizer, file3, OneTimeIncomeCurrencyBody(requesterId, 50m, "EUR"));

        // Decoy in EN_REVISION → the AUTORIZADO filter must exclude it (proves the filter is respected).
        var fileDecoy = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Dec", "Grupo", "EMP-OTI-GRP-D", "dec.otigrp.d@empresa.test");
        await CreateOneTimeIncomeAsync(manager, fileDecoy, OneTimeIncomeCurrencyBody(requesterId, 999m, "USD"));

        // Flat search of the SAME filter (no groupBy).
        int flatCount;
        decimal flatUsd, flatEur;
        using (var doc = await QueryOneTimeIncomesBandejaAsync(manager, scenario.TenantId, new { statusCodes = new[] { "AUTORIZADO" } }))
        {
            var root = doc.RootElement;
            flatCount = root.GetProperty("totalCount").GetInt32();
            flatUsd = root.GetProperty("totalsByCurrency").GetProperty("USD").GetDecimal();
            flatEur = root.GetProperty("totalsByCurrency").GetProperty("EUR").GetDecimal();
        }
        Assert.Equal(3, flatCount);
        Assert.Equal(300m, flatUsd);
        Assert.Equal(50m, flatEur);

        // groupBy = tipoPlanilla → ONE bucket (QUINCENAL) spanning both currencies (RN-13: never a merged total).
        using (var doc = await QueryOneTimeIncomesBandejaAsync(manager, scenario.TenantId, new { statusCodes = new[] { "AUTORIZADO" }, groupBy = "tipoPlanilla" }))
        {
            var groups = doc.RootElement.GetProperty("groups").EnumerateArray().ToArray();
            Assert.Single(groups);
            var bucket = groups[0];
            Assert.Equal("QUINCENAL", bucket.GetProperty("key").GetString());
            Assert.Equal(3, bucket.GetProperty("count").GetInt32());
            var totals = bucket.GetProperty("totalsByCurrency");
            Assert.Equal(300m, totals.GetProperty("USD").GetDecimal());
            Assert.Equal(50m, totals.GetProperty("EUR").GetDecimal());

            // CUADRA: Σ group.count == flat totalCount; Σ per-currency == flat totalsByCurrency.
            var sumCount = groups.Sum(group => group.GetProperty("count").GetInt32());
            Assert.Equal(flatCount, sumCount);
            Assert.Equal(flatUsd, groups.Sum(group => SumCurrency(group, "USD")));
            Assert.Equal(flatEur, groups.Sum(group => SumCurrency(group, "EUR")));
        }

        // groupBy = empleado → 3 buckets (one per file) → still cuadra by count.
        using (var doc = await QueryOneTimeIncomesBandejaAsync(manager, scenario.TenantId, new { statusCodes = new[] { "AUTORIZADO" }, groupBy = "empleado" }))
        {
            var groups = doc.RootElement.GetProperty("groups").EnumerateArray().ToArray();
            Assert.Equal(3, groups.Length);
            Assert.Equal(flatCount, groups.Sum(group => group.GetProperty("count").GetInt32()));
            Assert.Equal(flatUsd, groups.Sum(group => SumCurrency(group, "USD")));
            Assert.Equal(flatEur, groups.Sum(group => SumCurrency(group, "EUR")));
        }
    }

    private static decimal SumCurrency(JsonElement group, string currency)
    {
        var totals = group.GetProperty("totalsByCurrency");
        return totals.TryGetProperty(currency, out var value) ? value.GetDecimal() : 0m;
    }

    [Fact]
    public async Task OneTimeIncomesBandeja_GroupByMonth_TruncatesToMonth()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario));
        using var authorizer = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Mes", "Solicitante", "EMP-OTI-MES-REQ", "mes.otimes.req@empresa.test");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var lastMonth = today.AddMonths(-1);

        var fileThis = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Est", "Mes", "EMP-OTI-MES-1", "est.otimes.1@empresa.test");
        var filePrev = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Pre", "Mes", "EMP-OTI-MES-2", "pre.otimes.2@empresa.test");
        await CreateAndAuthorizeOneTimeIncomeAsync(manager, authorizer, fileThis, OneTimeIncomeCurrencyBody(requesterId, 10m, "USD", incomeDate: today.ToString("yyyy-MM-dd")));
        await CreateAndAuthorizeOneTimeIncomeAsync(manager, authorizer, filePrev, OneTimeIncomeCurrencyBody(requesterId, 20m, "USD", incomeDate: lastMonth.ToString("yyyy-MM-dd")));

        using var doc = await QueryOneTimeIncomesBandejaAsync(manager, scenario.TenantId, new { statusCodes = new[] { "AUTORIZADO" }, groupBy = "mes" });
        var groups = doc.RootElement.GetProperty("groups").EnumerateArray().ToArray();

        var thisKey = today.ToString("yyyy-MM");
        var prevKey = lastMonth.ToString("yyyy-MM");
        // Two month buckets when the two dates fall in different months (guarded so month-boundary runs stay green).
        if (thisKey != prevKey)
        {
            Assert.Equal(2, groups.Length);
            Assert.Contains(groups, group => group.GetProperty("key").GetString() == thisKey);
            Assert.Contains(groups, group => group.GetProperty("key").GetString() == prevKey);
        }

        Assert.Equal(2, groups.Sum(group => group.GetProperty("count").GetInt32()));
        Assert.Equal(30m, groups.Sum(group => SumCurrency(group, "USD")));
    }

    [Fact]
    public async Task OneTimeIncomesBandeja_GroupByInvalidDimension_Returns400()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario));

        var response = await manager.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/one-time-incomes/query",
            new { groupBy = "banana" });
        await AssertProblemDetailsAsync(response, HttpStatusCode.BadRequest, "ONE_TIME_INCOME_GROUP_DIMENSION_INVALID");
    }

    [Fact]
    public async Task OneTimeIncomesBandeja_Export_ReturnsSpreadsheet()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario));

        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Exp", "Solicitante", "EMP-OTI-EXP-REQ", "exp.otiexp.req@empresa.test");
        var fileId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Xio", "Export", "EMP-OTI-EXP", "xio.otiexp@empresa.test");
        await CreateOneTimeIncomeAsync(manager, fileId, OneTimeIncomeCurrencyBody(requesterId, 150m, "USD"));

        var xlsx = await manager.GetAsync($"/api/v1/companies/{scenario.TenantId}/one-time-incomes/export?format=xlsx");
        Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            xlsx.Content.Headers.ContentType?.MediaType);

        var json = await manager.GetAsync($"/api/v1/companies/{scenario.TenantId}/one-time-incomes/export?format=json");
        Assert.Equal(HttpStatusCode.OK, json.StatusCode);
        using var doc = JsonDocument.Parse(await json.Content.ReadAsStringAsync());
        var rows = doc.RootElement.EnumerateArray().ToArray();
        Assert.Single(rows);
        Assert.Equal("EN_REVISION", rows[0].GetProperty("Estado").GetString());
        Assert.Equal("BONO", rows[0].GetProperty("Tipo").GetString());
        Assert.Equal(150m, rows[0].GetProperty("Monto").GetDecimal());
        Assert.Equal("USD", rows[0].GetProperty("Moneda").GetString());
    }

    [Fact]
    public async Task OneTimeIncomesBandeja_PendingExport_MarksOverdue()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario));
        using var authorizer = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Pen", "Solicitante", "EMP-OTI-PEX-REQ", "pen.otipex.req@empresa.test");
        var fileId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Olga", "Overdue", "EMP-OTI-PEX", "olga.otipex@empresa.test");

        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        await CreateAndAuthorizeOneTimeIncomeAsync(manager, authorizer, fileId, OverdueOneTimeIncomeBody(requesterId, yesterday));

        var json = await manager.GetAsync($"/api/v1/companies/{scenario.TenantId}/one-time-incomes/pending/export?format=json&payrollTypeCode=QUINCENAL");
        Assert.Equal(HttpStatusCode.OK, json.StatusCode);
        using var doc = JsonDocument.Parse(await json.Content.ReadAsStringAsync());
        var rows = doc.RootElement.EnumerateArray().ToArray();
        Assert.Single(rows);
        Assert.True(rows[0].GetProperty("Vencido").GetBoolean());
        Assert.Equal("Quincena vencida", rows[0].GetProperty("Periodo").GetString());
    }

    [Fact]
    public async Task OneTimeIncomesBandeja_PayrollInput_CuadraAgainstPending()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario));
        using var authorizer = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Ins", "Solicitante", "EMP-OTI-INS-REQ", "ins.otiins.req@empresa.test");

        // 3 AUTORIZADO incomes, all QUINCENAL + "Quincena 13/2026".
        var file1 = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Uno", "Insumo", "EMP-OTI-INS-1", "uno.otiins.1@empresa.test");
        var file2 = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Dos", "Insumo", "EMP-OTI-INS-2", "dos.otiins.2@empresa.test");
        var file3 = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Tri", "Insumo", "EMP-OTI-INS-3", "tri.otiins.3@empresa.test");
        await CreateAndAuthorizeOneTimeIncomeAsync(manager, authorizer, file1, OneTimeIncomeCurrencyBody(requesterId, 40m, "USD"));
        await CreateAndAuthorizeOneTimeIncomeAsync(manager, authorizer, file2, OneTimeIncomeCurrencyBody(requesterId, 60m, "USD"));
        var (income3, token3) = await CreateAndAuthorizeOneTimeIncomeAsync(manager, authorizer, file3, OneTimeIncomeCurrencyBody(requesterId, 25m, "USD"));

        // Revoke income3 → it drops out of BOTH the pending tray and the insumo.
        var revoke = await PatchOneTimeIncomeAsync(authorizer, file3, income3, "revocation", token3, new { reason = "Ya no aplica" });
        revoke.EnsureSuccessStatusCode();

        // Pending tray of the filter (BEFORE applying): count + sum.
        int pendingCount;
        decimal pendingSum;
        var pending = await QueryOneTimeIncomePendingAsync(manager, scenario.TenantId, new { payrollTypeCode = "QUINCENAL", onlyOverdue = (bool?)null });
        pending.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await pending.Content.ReadAsStringAsync()))
        {
            var items = doc.RootElement.GetProperty("items").EnumerateArray().ToArray();
            pendingCount = items.Length;
            pendingSum = items.Sum(item => item.GetProperty("amount").GetDecimal());
        }
        Assert.Equal(2, pendingCount);
        Assert.Equal(100m, pendingSum);

        // Payroll input over the same filter cuadra EXACTLY against the pending set.
        var input = await manager.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/one-time-incomes/payroll-input/export?format=json&payrollTypeCode=QUINCENAL&payrollPeriod=Quincena%2013/2026");
        Assert.Equal(HttpStatusCode.OK, input.StatusCode);
        using (var doc = JsonDocument.Parse(await input.Content.ReadAsStringAsync()))
        {
            var rows = doc.RootElement.EnumerateArray().ToArray();
            Assert.Equal(pendingCount, rows.Length);
            Assert.Equal(pendingSum, rows.Sum(row => row.GetProperty("Monto").GetDecimal()));
            Assert.All(rows, row => Assert.Equal("QUINCENAL", row.GetProperty("TipoPlanilla").GetString()));
            Assert.All(rows, row => Assert.Equal("Quincena 13/2026", row.GetProperty("Periodo").GetString()));
        }

        // Apply the period → exactly the pending incomes become APLICADO → both pending and insumo drain.
        var batch = await ApplyOneTimeIncomePeriodAsync(manager, scenario.TenantId, new
        {
            payrollTypeCode = "QUINCENAL",
            payrollPeriodPublicId = (Guid?)null,
            payrollPeriodLabel = "Quincena 13/2026",
            excludedIncomePublicIds = Array.Empty<Guid>()
        });
        batch.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await batch.Content.ReadAsStringAsync()))
        {
            Assert.Equal(pendingCount, doc.RootElement.GetProperty("aplicados").GetInt32());
        }

        var afterInput = await manager.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/one-time-incomes/payroll-input/export?format=json&payrollTypeCode=QUINCENAL&payrollPeriod=Quincena%2013/2026");
        Assert.Equal(HttpStatusCode.OK, afterInput.StatusCode);
        using (var doc = JsonDocument.Parse(await afterInput.Content.ReadAsStringAsync()))
        {
            Assert.Empty(doc.RootElement.EnumerateArray());
        }

        var afterPending = await QueryOneTimeIncomePendingAsync(manager, scenario.TenantId, new { payrollTypeCode = "QUINCENAL", onlyOverdue = (bool?)null });
        afterPending.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await afterPending.Content.ReadAsStringAsync()))
        {
            Assert.Equal(0, doc.RootElement.GetProperty("totalCount").GetInt32());
        }
    }

    [Fact]
    public async Task OneTimeIncomesBandeja_PayrollInput_MissingFilter_Returns400()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario));

        // Missing payroll type + period → 400 (mandatory §5 filter).
        var missing = await manager.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/one-time-incomes/payroll-input/export?format=json");
        await AssertProblemDetailsAsync(missing, HttpStatusCode.BadRequest, "ONE_TIME_INCOME_PAYROLL_INPUT_FILTER_REQUIRED");

        // Payroll type but no period → still 400.
        var noPeriod = await manager.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/one-time-incomes/payroll-input/export?format=json&payrollTypeCode=QUINCENAL");
        await AssertProblemDetailsAsync(noPeriod, HttpStatusCode.BadRequest, "ONE_TIME_INCOME_PAYROLL_INPUT_FILTER_REQUIRED");
    }
}
