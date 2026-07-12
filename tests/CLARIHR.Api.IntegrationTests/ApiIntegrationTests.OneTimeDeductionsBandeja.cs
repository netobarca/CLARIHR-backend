using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Integration coverage for the one-time-deduction bandeja + exports (REQ-009 PR-5): the company bandeja with its
/// StatusCounts over EVERY status and its totals per currency, the filters, and — the load-bearing test — that the
/// payroll input CUADRA: it carries exactly the deductions that were actually CHARGED, so a REVERTED application
/// disappears from it (the payroll must never discount money the company gave back).
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static async Task<JsonElement> QueryOneTimeDeductionBandejaAsync(
        HttpClient client, Guid companyId, object? body = null)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/companies/{companyId}/one-time-deductions/query",
            body ?? new { });
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == response.StatusCode, payload);
        return JsonDocument.Parse(payload).RootElement.Clone();
    }

    private static async Task<JsonElement> ExportOneTimeDeductionsAsync(HttpClient client, Guid companyId, string query = "")
    {
        var response = await client.GetAsync(
            $"/api/v1/companies/{companyId}/one-time-deductions/export?format=json{query}");
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == response.StatusCode, payload);
        return JsonDocument.Parse(payload).RootElement.Clone();
    }

    [Fact]
    public async Task OneTimeDeductionsBandeja_ListsEveryStatus_WithItsCountsAndTotals()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OneTimeDeductionManagerContext(scenario, Guid.NewGuid()));

        var fileA = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Bea", "Bandeja", "EMP-OTDB-A", "bea.otdb.a@empresa.test");
        var fileB = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Bruno", "Bandeja", "EMP-OTDB-B", "bruno.otdb.b@empresa.test");
        var requesterId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Jefa", "Solicitante", "EMP-OTDB-R", "jefa.otdb.r@empresa.test");

        // A: authorized and CHARGED ($75). B: left in EN_REVISION (never resolved).
        var (deductionA, tokenA) = await CreateAndAuthorizeOneTimeDeductionAsync(scenario, manager, fileA, requesterId);
        var applied = await ApplyOneTimeDeductionAsync(manager, fileA, deductionA, tokenA);
        applied.EnsureSuccessStatusCode();

        await CreateOneTimeDeductionAsync(manager, fileB, FixedOneTimeDeductionBody(requesterId, amount: 40m));

        var bandeja = await QueryOneTimeDeductionBandejaAsync(manager, scenario.TenantId);

        Assert.Equal(2, bandeja.GetProperty("totalCount").GetInt32());

        // The counts cover EVERY status of the company, not only the page that was listed.
        var counts = bandeja.GetProperty("statusCounts");
        Assert.Equal(1, counts.GetProperty("APLICADO").GetInt32());
        Assert.Equal(1, counts.GetProperty("EN_REVISION").GetInt32());

        // The totals are the money the company is charging, per currency (75 + 40).
        Assert.Equal(115m, bandeja.GetProperty("amountByCurrency").GetProperty("USD").GetDecimal());

        // Every row carries who asked for the deduction and where it is charged.
        foreach (var item in bandeja.GetProperty("items").EnumerateArray())
        {
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("requesterNameSnapshot").GetString()));
            Assert.Equal("MENSUAL", item.GetProperty("payrollTypeCode").GetString());
            Assert.NotEqual(Guid.Empty, item.GetProperty("assignedPositionPublicId").GetGuid());
        }
    }

    [Fact]
    public async Task OneTimeDeductionsBandeja_FiltersNarrowTheItems_ButNeverTheCounts()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OneTimeDeductionManagerContext(scenario, Guid.NewGuid()));

        var fileA = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Fina", "Filtro", "EMP-OTDF-A", "fina.otdf.a@empresa.test");
        var fileB = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Fito", "Filtro", "EMP-OTDF-B", "fito.otdf.b@empresa.test");
        var requesterId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Jefa", "Solicitante", "EMP-OTDF-R", "jefa.otdf.r@empresa.test");

        var (deductionA, tokenA) = await CreateAndAuthorizeOneTimeDeductionAsync(scenario, manager, fileA, requesterId);
        (await ApplyOneTimeDeductionAsync(manager, fileA, deductionA, tokenA)).EnsureSuccessStatusCode();
        await CreateOneTimeDeductionAsync(manager, fileB, FixedOneTimeDeductionBody(requesterId));

        // By employee.
        var byEmployee = await QueryOneTimeDeductionBandejaAsync(manager, scenario.TenantId, new { employeeId = fileB });
        Assert.Equal(1, byEmployee.GetProperty("totalCount").GetInt32());
        Assert.Equal(fileB, byEmployee.GetProperty("items")[0].GetProperty("personnelFilePublicId").GetGuid());

        // By status: the ITEMS are narrowed to the charged one...
        var byStatus = await QueryOneTimeDeductionBandejaAsync(manager, scenario.TenantId, new { statusCode = "APLICADO" });
        Assert.Equal(1, byStatus.GetProperty("totalCount").GetInt32());
        Assert.Equal(deductionA, byStatus.GetProperty("items")[0].GetProperty("oneTimeDeductionPublicId").GetGuid());

        // ...but the counts still span every status, so the tabs of the bandeja never lie.
        var statusCounts = byStatus.GetProperty("statusCounts");
        Assert.Equal(1, statusCounts.GetProperty("APLICADO").GetInt32());
        Assert.Equal(1, statusCounts.GetProperty("EN_REVISION").GetInt32());

        // By concept type: both were created with DANO_EQUIPO; an unrelated concept returns nothing.
        var byConcept = await QueryOneTimeDeductionBandejaAsync(manager, scenario.TenantId, new { conceptTypeCode = "ANTICIPO" });
        Assert.Equal(0, byConcept.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task OneTimeDeductionsExport_ReturnsTheFilteredRowsInSpanish()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OneTimeDeductionManagerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Elsa", "Exportada", "EMP-OTDX-A", "elsa.otdx.a@empresa.test");
        var requesterId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Jefa", "Solicitante", "EMP-OTDX-R", "jefa.otdx.r@empresa.test");

        await CreateOneTimeDeductionAsync(manager, fileId, FixedOneTimeDeductionBody(requesterId));

        var rows = await ExportOneTimeDeductionsAsync(manager, scenario.TenantId);
        Assert.Equal(1, rows.GetArrayLength());

        // The export ships PascalCase Spanish property names — they ARE the column headers of the xlsx/csv.
        var row = rows[0];
        Assert.Equal("DANO-LAPTOP-001", row.GetProperty("Referencia").GetString());
        Assert.Equal(75m, row.GetProperty("Monto").GetDecimal());
        Assert.Equal("EN_REVISION", row.GetProperty("Estado").GetString());
        Assert.Equal("USD", row.GetProperty("Moneda").GetString());
        Assert.True(row.GetProperty("ValorFijo").GetBoolean());
    }

    [Fact]
    public async Task OneTimeDeductionPayrollInput_RequiresTheRange_AndCarriesOnlyWhatWasActuallyCharged()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OneTimeDeductionManagerContext(scenario, Guid.NewGuid()));

        var charged = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Ines", "Insumo", "EMP-OTDI-A", "ines.otdi.a@empresa.test");
        var reverted = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Raul", "Revertido", "EMP-OTDI-B", "raul.otdi.b@empresa.test");
        var pending = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Pia", "Pendiente", "EMP-OTDI-C", "pia.otdi.c@empresa.test");
        var requesterId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Jefa", "Solicitante", "EMP-OTDI-R", "jefa.otdi.r@empresa.test");

        // [1] Charged and kept.
        var (chargedDeduction, chargedToken) = await CreateAndAuthorizeOneTimeDeductionAsync(scenario, manager, charged, requesterId);
        (await ApplyOneTimeDeductionAsync(manager, charged, chargedDeduction, chargedToken)).EnsureSuccessStatusCode();

        // [2] Charged and then REVERTED — the company gave the money back, the payroll must not discount it.
        var (revertedDeduction, revertedToken) = await CreateAndAuthorizeOneTimeDeductionAsync(scenario, manager, reverted, requesterId);
        var revertedApplication = await ApplyOneTimeDeductionAsync(manager, reverted, revertedDeduction, revertedToken);
        revertedApplication.EnsureSuccessStatusCode();

        Guid applicationId;
        Guid tokenAfterApply;
        using (var doc = JsonDocument.Parse(await revertedApplication.Content.ReadAsStringAsync()))
        {
            applicationId = doc.RootElement.GetProperty("application").GetProperty("applicationPublicId").GetGuid();
            tokenAfterApply = doc.RootElement.GetProperty("oneTimeDeductionConcurrencyToken").GetGuid();
        }

        using var annulRequest = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/personnel-files/{reverted}/one-time-deductions/{revertedDeduction}/applications/{applicationId}/annulment")
        {
            Content = JsonContent.Create(new { reason = "Cobro indebido" })
        };
        annulRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{tokenAfterApply}\"");
        var annulled = await manager.SendAsync(annulRequest);
        var annulledPayload = await annulled.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == annulled.StatusCode, $"Annul failed: {(int)annulled.StatusCode} {annulledPayload}");

        // [3] Authorized but never charged — it is not payroll input yet.
        await CreateAndAuthorizeOneTimeDeductionAsync(scenario, manager, pending, requesterId);

        // The range is MANDATORY: a missing bound is a domain rule (422), not a silent full-table dump.
        var withoutRange = await manager.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/one-time-deductions/payroll-input/export?format=json");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, withoutRange.StatusCode);
        using (var problem = JsonDocument.Parse(await withoutRange.Content.ReadAsStringAsync()))
        {
            // The business code is a ROOT member of the ProblemDetails: ProblemDetailsFactory writes it into
            // ProblemDetails.Extensions, which System.Text.Json flattens ([JsonExtensionData]) — there is no
            // "extensions" object on the wire.
            Assert.Equal(
                "ONE_TIME_DEDUCTION_PAYROLL_INPUT_RANGE_REQUIRED",
                problem.RootElement.GetProperty("code").GetString());
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = await manager.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/one-time-deductions/payroll-input/export?format=json" +
            $"&startDate={today.AddDays(-1):yyyy-MM-dd}&endDate={today.AddDays(1):yyyy-MM-dd}");
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == response.StatusCode, payload);

        using var rows = JsonDocument.Parse(payload);

        // ONLY the deduction that was actually charged and kept: the reverted one and the pending one are out.
        Assert.Equal(1, rows.RootElement.GetArrayLength());
        var row = rows.RootElement[0];
        Assert.Contains("Insumo", row.GetProperty("Empleado").GetString());
        Assert.Equal(75m, row.GetProperty("Monto").GetDecimal());
        Assert.Equal("MENSUAL", row.GetProperty("TipoPlanilla").GetString());
        Assert.Equal(today.ToString("yyyy-MM-dd"), row.GetProperty("FechaAplicada").GetString());
    }
}
