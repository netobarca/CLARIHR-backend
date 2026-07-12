using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Integration coverage for the recurring-deduction bandejas + exports (REQ-008 PR-5): the company bandeja with its
/// StatusCounts over EVERY status and its derived charged / outstanding totals, the pending-charges bandeja (which
/// reuses the very projection the apply-period batch consumes), the payroll input with its MANDATORY range, and —
/// the load-bearing test — that the payroll input CUADRA exactly against the pending-charges bandeja: what the
/// bandeja said was due is, once applied, exactly what the payroll is told to discount.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static async Task<JsonElement> QueryDeductionBandejaAsync(HttpClient client, Guid companyId, object? body = null)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/companies/{companyId}/recurring-deductions/query",
            body ?? new { });
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == response.StatusCode, payload);
        return JsonDocument.Parse(payload).RootElement.Clone();
    }

    private static async Task<JsonElement> QueryPendingChargesAsync(HttpClient client, Guid companyId, object? body = null)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/companies/{companyId}/recurring-deductions/pending-installments/query",
            body ?? new { });
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == response.StatusCode, payload);
        return JsonDocument.Parse(payload).RootElement.Clone();
    }

    [Fact]
    public async Task RecurringDeductionsBandeja_ListsEveryStatus_AndDerivesTheChargedAndOutstandingTotals()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringDeductionManagerContext(scenario, Guid.NewGuid()));

        var fileA = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Bea", "Bandeja", "EMP-RDB-A", "bea.rdb.a@empresa.test");
        var fileB = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Bruno", "Bandeja", "EMP-RDB-B", "bruno.rdb.b@empresa.test");

        // Credit A: authorized, one charge applied ($50 of $750).
        var (deductionA, tokenA) = await CreateAndAuthorizeDeductionAsync(scenario, manager, fileA, SegmentedRecurringDeductionBody());
        var applied = await ApplyChargeAsync(manager, fileA, deductionA, tokenA);
        applied.EnsureSuccessStatusCode();

        // Credit B: left in EN_REVISION (never authorized).
        await CreateRecurringDeductionAsync(manager, fileB, SegmentedRecurringDeductionBody());

        var bandeja = await QueryDeductionBandejaAsync(manager, scenario.TenantId);

        Assert.Equal(2, bandeja.GetProperty("totalCount").GetInt32());

        // The StatusCounts cover every status, not just the listed page.
        var counts = bandeja.GetProperty("statusCounts");
        Assert.Equal(1, counts.GetProperty("VIGENTE").GetInt32());
        Assert.Equal(1, counts.GetProperty("EN_REVISION").GetInt32());

        // The derived totals: $50 charged on A; $700 outstanding on A + $750 on B = $1,450.
        Assert.Equal(50m, bandeja.GetProperty("chargedByCurrency").GetProperty("USD").GetDecimal());
        Assert.Equal(1450m, bandeja.GetProperty("outstandingByCurrency").GetProperty("USD").GetDecimal());

        var rowA = bandeja.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("recurringDeductionPublicId").GetGuid() == deductionA);
        Assert.Equal(50m, rowA.GetProperty("totalCharged").GetDecimal());
        Assert.Equal(700m, rowA.GetProperty("totalOutstanding").GetDecimal());
        Assert.Equal("Banco Agrícola", rowA.GetProperty("financialInstitution").GetString());
    }

    [Fact]
    public async Task RecurringDeductionsBandeja_StatusFilter_NarrowsTheItemsButNotTheCounts()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringDeductionManagerContext(scenario, Guid.NewGuid()));

        var fileA = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Filtra", "Uno", "EMP-RDB-C", "filtra.rdb.c@empresa.test");
        var fileB = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Filtra", "Dos", "EMP-RDB-D", "filtra.rdb.d@empresa.test");

        await CreateAndAuthorizeDeductionAsync(scenario, manager, fileA, SegmentedRecurringDeductionBody());
        await CreateRecurringDeductionAsync(manager, fileB, SegmentedRecurringDeductionBody());

        var bandeja = await QueryDeductionBandejaAsync(manager, scenario.TenantId, new { statusCode = "VIGENTE" });

        // Only the VIGENTE credit is listed...
        Assert.Equal(1, bandeja.GetProperty("totalCount").GetInt32());
        // ...but the counts still see both statuses.
        Assert.Equal(1, bandeja.GetProperty("statusCounts").GetProperty("VIGENTE").GetInt32());
        Assert.Equal(1, bandeja.GetProperty("statusCounts").GetProperty("EN_REVISION").GetInt32());
    }

    [Fact]
    public async Task RecurringDeductionsPendingCharges_ProjectTheDueChargesAndFlagTheOverdueOnes()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringDeductionManagerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Pía", "Pendiente", "EMP-RDB-E", "pia.rdb.e@empresa.test");

        await CreateAndAuthorizeDeductionAsync(scenario, manager, fileId, SegmentedRecurringDeductionBody());

        // A cutoff 3 months out catches charges 1..4 of the monthly plan (today + 3 monthly steps).
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(3).ToString("yyyy-MM-dd");
        var pending = await QueryPendingChargesAsync(manager, scenario.TenantId, new { cutoffDate = cutoff });

        Assert.Equal(4, pending.GetProperty("totalCount").GetInt32());

        var first = pending.GetProperty("items")[0];
        Assert.Equal(1, first.GetProperty("installmentNumber").GetInt32());
        Assert.Equal(50m, first.GetProperty("amount").GetDecimal());
        Assert.Equal("PREST-BCO-2026-001", first.GetProperty("reference").GetString());
        Assert.Equal("Banco Agrícola", first.GetProperty("financialInstitution").GetString());
        // The first charge falls due today (the plan starts today), so it is not overdue yet.
        Assert.False(first.GetProperty("isOverdue").GetBoolean());
    }

    [Fact]
    public async Task RecurringDeductionsPendingCharges_AFutureDatedCreditContributesNothing()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringDeductionManagerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Futuro", "Pendiente", "EMP-RDB-F", "futuro.rdb.f@empresa.test");

        var future = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(2).ToString("yyyy-MM-dd");
        var body = new
        {
            effectiveDate = future,
            reference = "PREST-FUT-BANDEJA",
            recurringDeductionTypeCode = "PRESTAMO_BANCARIO",
            conceptTypeCode = "PRESTAMO_BANCARIO",
            financialInstitution = "Banco",
            observations = (string?)null,
            assignedPositionPublicId = (Guid?)null,
            installmentStartDate = future,
            exceptionMonths = (int[]?)null,
            currencyCode = "USD",
            payrollTypeCode = "MENSUAL",
            installmentFrequencyCode = "MENSUAL",
            applicationFrequencyCode = "MENSUAL",
            isIndefinite = false,
            usesCompoundInterest = false,
            principalAmount = (decimal?)null,
            interestRatePercent = (decimal?)null,
            plannedInstallments = (int?)null,
            segments = new object[] { new { fromInstallment = 1, toInstallment = (int?)6, installmentValue = 50m } },
            settlementActionCode = "DESCONTAR_SALDO"
        };

        await CreateAndAuthorizeDeductionAsync(scenario, manager, fileId, body);

        // The bandeja must never show work the batch would refuse (D-04): the credit is not enforceable yet.
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(6).ToString("yyyy-MM-dd");
        var pending = await QueryPendingChargesAsync(manager, scenario.TenantId, new { cutoffDate = cutoff });

        Assert.Equal(0, pending.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task RecurringDeductionsPayrollInput_WithoutARange_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringDeductionManagerContext(scenario, Guid.NewGuid()));

        var response = await manager.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/recurring-deductions/payroll-input/export?format=json");

        await AssertProblemDetailsAsync(
            response, HttpStatusCode.UnprocessableEntity, "RECURRING_DEDUCTION_PAYROLL_INPUT_RANGE_REQUIRED");
    }

    [Fact]
    public async Task RecurringDeductionsPayrollInput_CuadraExactlyAgainstThePendingBandeja()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringDeductionManagerContext(scenario, Guid.NewGuid()));

        var fileA = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Cuadra", "Uno", "EMP-RDB-G", "cuadra.rdb.g@empresa.test");
        var fileB = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Cuadra", "Dos", "EMP-RDB-H", "cuadra.rdb.h@empresa.test");

        await CreateAndAuthorizeDeductionAsync(scenario, manager, fileA, SegmentedRecurringDeductionBody());
        await CreateAndAuthorizeDeductionAsync(scenario, manager, fileB, InterestRecurringDeductionBody());

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cutoff = today.ToString("yyyy-MM-dd");

        // 1) What the pending bandeja says is due up to today.
        var pending = await QueryPendingChargesAsync(manager, scenario.TenantId, new { cutoffDate = cutoff });
        var expected = pending.GetProperty("items").EnumerateArray()
            .Select(item => (
                Reference: item.GetProperty("reference").GetString()!,
                Number: item.GetProperty("installmentNumber").GetInt32(),
                Amount: item.GetProperty("amount").GetDecimal()))
            .OrderBy(row => row.Reference)
            .ThenBy(row => row.Number)
            .ToArray();

        Assert.Equal(2, expected.Length);

        // 2) The batch applies exactly that.
        var batch = await manager.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/recurring-deductions/apply-period",
            new
            {
                payrollTypeCode = "MENSUAL",
                payrollPeriodPublicId = (Guid?)null,
                cutoffDate = cutoff,
                excludedDeductionPublicIds = Array.Empty<Guid>()
            });
        batch.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await batch.Content.ReadAsStringAsync()))
        {
            Assert.Equal(expected.Length, doc.RootElement.GetProperty("aplicadas").GetInt32());
        }

        // 3) The payroll input for the same window must carry EXACTLY those rows, with the same amounts.
        var response = await manager.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/recurring-deductions/payroll-input/export"
            + $"?format=json&startDate={today:yyyy-MM-dd}&endDate={today:yyyy-MM-dd}");
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == response.StatusCode, payload);

        using var input = JsonDocument.Parse(payload);
        var actual = input.RootElement.EnumerateArray()
            .Select(row => (
                Reference: row.GetProperty("Referencia").GetString()!,
                Number: row.GetProperty("NumeroCuota").GetInt32(),
                Amount: row.GetProperty("Monto").GetDecimal()))
            .OrderBy(row => row.Reference)
            .ThenBy(row => row.Number)
            .ToArray();

        Assert.Equal(expected, actual);

        // The creditor travels with the row — the payroll operator must know whom to pay.
        var first = input.RootElement[0];
        Assert.False(string.IsNullOrWhiteSpace(first.GetProperty("InstitucionFinanciera").GetString()));
        Assert.Equal("REGULAR", first.GetProperty("TipoCuota").GetString());
    }

    [Fact]
    public async Task RecurringDeductionsPayrollInput_IncludesTheExtraordinaryPayments()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(RecurringDeductionManagerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedRecurringDeductionCandidateAsync(
            scenario.TenantId, "Extra", "Insumo", "EMP-RDB-I", "extra.rdb.i@empresa.test");

        var (deductionId, token) = await CreateAndAuthorizeDeductionAsync(scenario, manager, fileId, SegmentedRecurringDeductionBody());

        var abono = await ApplyExtraordinaryAsync(manager, fileId, deductionId, token, 100m);
        abono.EnsureSuccessStatusCode();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = await manager.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/recurring-deductions/payroll-input/export"
            + $"?format=json&startDate={today:yyyy-MM-dd}&endDate={today:yyyy-MM-dd}");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = doc.RootElement.EnumerateArray().Single();

        // The payroll must discount the abono too — it is money the employee owes this period.
        Assert.Equal("EXTRAORDINARIA", row.GetProperty("TipoCuota").GetString());
        Assert.Equal(100m, row.GetProperty("Monto").GetDecimal());
        Assert.Equal(100m, row.GetProperty("Capital").GetDecimal());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("NumeroCuota").ValueKind);
    }
}
