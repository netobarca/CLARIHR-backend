using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// The company-wide not-worked-time bandeja, its payroll input and its availability source (REQ-011 PR-4). The
/// load-bearing test: an ANNULLED record disappears from the payroll input AND from the availability view — an
/// absence that was annulled never happened, so the payroll must not discount it and the planner must not see the
/// employee as unavailable.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private async Task<(Guid EmployeeId, Guid RecordId, Guid Token)> SeedNotWorkedTimeRecordAsync(
        IntegrationTestScenario scenario,
        HttpClient client,
        string tag)
    {
        var (employeeId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Bandeja", tag, $"EMP-NWTB-{tag}", $"bandeja.{tag.ToLowerInvariant()}@empresa.test",
            monthlySalary: 900m);

        var created = await CreateNotWorkedTimeAsync(client, employeeId, new
        {
            typeCode = "AUSENCIA_SIN_GOCE",
            assignedPositionPublicId = (Guid?)null,
            startDate = NotWorkedMonday.ToString("yyyy-MM-dd"),
            endDate = NotWorkedMonday.AddDays(4).ToString("yyyy-MM-dd"),
            hours = (decimal?)null,
            reason = (string?)null,
        });

        return (
            employeeId,
            created.GetProperty("notWorkedTimePublicId").GetGuid(),
            created.GetProperty("concurrencyToken").GetGuid());
    }

    [Fact]
    public async Task NotWorkedTimesBandeja_CountsAndTotalsSpanEveryStatus()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(NotWorkedTimeContext(scenario));
        await LoadNotWorkedTimeTemplateAsync(client, scenario.TenantId);

        var (_, _, _) = await SeedNotWorkedTimeRecordAsync(scenario, client, "A");
        var (employeeB, recordB, tokenB) = await SeedNotWorkedTimeRecordAsync(scenario, client, "B");

        // Annul B.
        using var annul = new HttpRequestMessage(
            HttpMethod.Patch, $"/api/v1/personnel-files/{employeeB}/not-worked-times/{recordB}/annulment")
        {
            Content = JsonContent.Create(new { reason = "Error de captura" })
        };
        annul.Headers.TryAddWithoutValidation("If-Match", $"\"{tokenB}\"");
        (await client.SendAsync(annul)).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/not-worked-times/query",
            new { });
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal(2, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, root.GetProperty("statusCounts").GetProperty("REGISTRADO").GetInt32());
        Assert.Equal(1, root.GetProperty("statusCounts").GetProperty("ANULADO").GetInt32());

        // The money the company is actually discounting: only the live record ($180). The annulled one is counted
        // in the tabs but NOT in the total — it is not money any more.
        Assert.Equal(180m, root.GetProperty("amountByCurrency").GetProperty("USD").GetDecimal());

        // Filtering by status narrows the ITEMS but never the counts (they are the numbers of the tabs).
        var filtered = await client.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/not-worked-times/query",
            new { statusCode = "ANULADO" });
        using var filteredDoc = JsonDocument.Parse(await filtered.Content.ReadAsStringAsync());
        Assert.Equal(1, filteredDoc.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, filteredDoc.RootElement.GetProperty("statusCounts").GetProperty("REGISTRADO").GetInt32());
    }

    [Fact]
    public async Task NotWorkedTimePayrollInput_RequiresTheRange_AndExcludesTheAnnulledAndThePaidOnes()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(NotWorkedTimeContext(scenario));
        await LoadNotWorkedTimeTemplateAsync(client, scenario.TenantId);

        // [1] A live unpaid absence → payroll input.
        var (_, _, _) = await SeedNotWorkedTimeRecordAsync(scenario, client, "C");

        // [2] An annulled one → out (the company gave the money back).
        var (employeeD, recordD, tokenD) = await SeedNotWorkedTimeRecordAsync(scenario, client, "D");
        using var annul = new HttpRequestMessage(
            HttpMethod.Patch, $"/api/v1/personnel-files/{employeeD}/not-worked-times/{recordD}/annulment")
        {
            Content = JsonContent.Create(new { reason = "Anulado" })
        };
        annul.Headers.TryAddWithoutValidation("If-Match", $"\"{tokenD}\"");
        (await client.SendAsync(annul)).EnsureSuccessStatusCode();

        // [3] A PAID absence → out too: it has no discount, so it is documentation, not payroll input.
        var (employeeE, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Con", "Goce", "EMP-NWTB-E", "con.goce.e@empresa.test", monthlySalary: 900m);
        await CreateNotWorkedTimeAsync(client, employeeE, new
        {
            typeCode = "AUSENCIA_CON_GOCE",
            assignedPositionPublicId = (Guid?)null,
            startDate = NotWorkedMonday.ToString("yyyy-MM-dd"),
            endDate = NotWorkedMonday.AddDays(4).ToString("yyyy-MM-dd"),
            hours = (decimal?)null,
            reason = (string?)null,
        });

        // The range is MANDATORY — no silent full-history dump into a payroll run.
        var withoutRange = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/not-worked-times/payroll-input/export?format=json");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, withoutRange.StatusCode);
        using (var problem = JsonDocument.Parse(await withoutRange.Content.ReadAsStringAsync()))
        {
            Assert.Equal(
                "NOT_WORKED_TIME_PAYROLL_INPUT_RANGE_REQUIRED",
                problem.RootElement.GetProperty("code").GetString());
        }

        var export = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/not-worked-times/payroll-input/export?format=json" +
            $"&startDate={NotWorkedMonday:yyyy-MM-dd}&endDate={NotWorkedMonday.AddDays(6):yyyy-MM-dd}");
        var payload = await export.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == export.StatusCode, payload);

        using var rows = JsonDocument.Parse(payload);
        Assert.Equal(1, rows.RootElement.GetArrayLength());

        var row = rows.RootElement[0];
        Assert.Equal(180m, row.GetProperty("Monto").GetDecimal());
        Assert.Equal(6m, row.GetProperty("DiasDescontados").GetDecimal());
        Assert.Equal("AUSENCIA_SIN_GOCE", row.GetProperty("ConceptoEgreso").GetString());
    }

    [Fact]
    public async Task NotWorkedTime_BecomesAThirdSourceOfTheAvailabilityView()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(NotWorkedTimeContext(scenario));
        await LoadNotWorkedTimeTemplateAsync(client, scenario.TenantId);

        var (employeeId, recordId, token) = await SeedNotWorkedTimeRecordAsync(scenario, client, "F");

        var query = new
        {
            startDate = NotWorkedMonday.ToString("yyyy-MM-dd"),
            endDate = NotWorkedMonday.AddDays(6).ToString("yyyy-MM-dd"),
        };

        var available = await client.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/time-availability/query", query);
        var payload = await available.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == available.StatusCode, payload);

        using (var doc = JsonDocument.Parse(payload))
        {
            // The chassis said connecting a family is additive: a repository method and a category. It was.
            Assert.Contains(
                doc.RootElement.GetProperty("activeSources").EnumerateArray(),
                item => item.GetString() == "TIEMPO_NO_TRABAJADO");

            var row = doc.RootElement.GetProperty("rows").EnumerateArray()
                .Single(item => item.GetProperty("categoryCode").GetString() == "TIEMPO_NO_TRABAJADO");
            Assert.Equal(employeeId, row.GetProperty("personnelFilePublicId").GetGuid());
            Assert.Equal(recordId, row.GetProperty("referencePublicId").GetGuid());
        }

        // Annulling it makes the employee AVAILABLE again: an annulled absence never happened.
        using var annul = new HttpRequestMessage(
            HttpMethod.Patch, $"/api/v1/personnel-files/{employeeId}/not-worked-times/{recordId}/annulment")
        {
            Content = JsonContent.Create(new { reason = "Anulado" })
        };
        annul.Headers.TryAddWithoutValidation("If-Match", $"\"{token}\"");
        (await client.SendAsync(annul)).EnsureSuccessStatusCode();

        var afterAnnulment = await client.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/time-availability/query", query);
        afterAnnulment.EnsureSuccessStatusCode();

        using var afterDoc = JsonDocument.Parse(await afterAnnulment.Content.ReadAsStringAsync());
        Assert.DoesNotContain(
            afterDoc.RootElement.GetProperty("rows").EnumerateArray(),
            item => item.GetProperty("categoryCode").GetString() == "TIEMPO_NO_TRABAJADO");
    }
}
