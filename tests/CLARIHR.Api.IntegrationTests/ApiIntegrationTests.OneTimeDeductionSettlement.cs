using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// End-to-end coverage of the one-time-deduction ↔ settlement integration (REQ-009 PR-5). The load-bearing
/// assertion: the suggested <c>DESCUENTO_EVENTUAL_PENDIENTE</c> line is classified as a <b>Descuento</b> and
/// therefore <b>REDUCES the net pay</b>. The engine's ResolveClass switch defaults to <c>Ingreso</c> and REQ-008's
/// fix only covered the CYCLIC concept — without this REQ's own entry in the Descuento arm, the settlement would
/// PAY the employee the fine it is supposed to charge him. Also covered: the line is manual (editable/excludable),
/// an EXCLUDED line leaves the debt owed, issuing charges it, and annulling the settlement reopens it.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static JsonElement? OneTimeDeductionLineOrNull(JsonElement settlementRoot) =>
        settlementRoot.GetProperty("lines").EnumerateArray()
            .Where(line => line.GetProperty("conceptCode").GetString() == "DESCUENTO_EVENTUAL_PENDIENTE")
            .Select(line => (JsonElement?)line)
            .FirstOrDefault();

    private static async Task<string> GetOneTimeDeductionStatusAsync(HttpClient client, Guid fileId, Guid deductionId)
    {
        var response = await client.GetAsync($"/api/v1/personnel-files/{fileId}/one-time-deductions/{deductionId}");
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == response.StatusCode, $"Get failed: {(int)response.StatusCode} {payload}");
        using var doc = JsonDocument.Parse(payload);
        return doc.RootElement.GetProperty("statusCode").GetString()!;
    }

    /// <summary>Seeds a settlement candidate carrying an AUTORIZADO (never charged) $75 deduction, and retires him.
    /// The deduction has to be created and authorized BEFORE the retirement — a retired profile is locked.</summary>
    private async Task<(Guid EmployeeId, Guid PlazaId, Guid DeductionId)> SeedRetiredEmployeeOwingAOneTimeDeductionAsync(
        IntegrationTestScenario scenario,
        HttpClient client,
        string tag)
    {
        var lower = tag.ToLowerInvariant();
        var (employeeId, plazaId) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Deudor", tag, $"EMP-OTDL-{tag}", $"deudor.{lower}@empresa.test");
        var (requesterId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Jefa", $"Solicitante{tag}", $"EMP-OTDLR-{tag}", $"jefa.{lower}@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", $"DeRrhh{tag}", $"EMP-OTDLG-{tag}", $"gestora.{lower}@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        var (deductionId, token) = await CreateOneTimeDeductionAsync(client, employeeId, FixedOneTimeDeductionBody(requesterId));

        using var authorizer = factory.CreateClientFor(OneTimeDeductionAuthorizerContext(scenario, Guid.NewGuid()));
        var authorized = await PatchOneTimeDeductionAsync(
            authorizer, employeeId, deductionId, "resolution", token,
            new { targetStatusCode = "AUTORIZADO", note = (string?)null });
        var authorizedPayload = await authorized.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == authorized.StatusCode, $"Authorize failed: {(int)authorized.StatusCode} {authorizedPayload}");

        await ExecuteRetirementAsync(client, employeeId, requesterId);
        return (employeeId, plazaId, deductionId);
    }

    private async Task<(Guid SettlementId, Guid Token, JsonDocument Document)> CreateSettlementForAsync(
        HttpClient client, Guid employeeId, Guid plazaId)
    {
        var created = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = plazaId, requestDate = DateTime.UtcNow.Date });
        var payload = await created.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == created.StatusCode, $"Create settlement failed: {(int)created.StatusCode} {payload}");

        var document = JsonDocument.Parse(payload);
        return (
            document.RootElement.GetProperty("publicId").GetGuid(),
            document.RootElement.GetProperty("concurrencyToken").GetGuid(),
            document);
    }

    [Fact]
    public async Task OneTimeDeductionSettlement_TheSuggestedLineIsADeduction_AndReducesTheNet()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        var (employeeId, plazaId, deductionId) =
            await SeedRetiredEmployeeOwingAOneTimeDeductionAsync(scenario, client, "A");

        var (settlementId, token, document) = await CreateSettlementForAsync(client, employeeId, plazaId);

        Guid lineId;
        decimal netWithLine;
        using (document)
        {
            var line = LineByConcept(document.RootElement, "DESCUENTO_EVENTUAL_PENDIENTE");

            // [1] THE assertion of this PR. ResolveClass defaults to Ingreso: without the concept in its Descuento
            // arm the $75 fine would have been PAID to the employee instead of charged to him.
            Assert.Equal("Descuento", line.GetProperty("conceptClass").GetString());

            // [2] Suggested at the deduction's amount, INCLUDED, and MANUAL (seed -9945 IsSystemCalculated=false),
            // so the settler can still edit or exclude it.
            Assert.Equal(75m, line.GetProperty("calculatedAmount").GetDecimal());
            Assert.True(line.GetProperty("isIncluded").GetBoolean());
            Assert.False(line.GetProperty("isSystemCalculated").GetBoolean());

            // [3] It counts among the DEDUCTIONS of the settlement.
            Assert.True(document.RootElement.GetProperty("totalDeductions").GetDecimal() >= 75m);

            lineId = line.GetProperty("publicId").GetGuid();
            netWithLine = document.RootElement.GetProperty("netPay").GetDecimal();
        }

        // [4] Excluding it gives the employee back exactly the $75 — which only holds if it had been SUBTRACTED.
        var excluded = await SendSettlementAsync(client, HttpMethod.Put,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/lines/{lineId}",
            token, new { isIncluded = false });
        var excludedPayload = await excluded.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == excluded.StatusCode, $"Exclude failed: {(int)excluded.StatusCode} {excludedPayload}");

        using (var doc = JsonDocument.Parse(excludedPayload))
        {
            token = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
            Assert.Equal(netWithLine + 75m, doc.RootElement.GetProperty("netPay").GetDecimal());
        }

        // [5] Issuing with the line EXCLUDED must NOT charge the debt: it stays AUTORIZADO (still owed).
        var issued = await SendSettlementAsync(client, HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/issuance",
            token, new { confirmNegativeNet = false });
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        token = await ReadTokenAsync(issued);
        Assert.Equal("AUTORIZADO", await GetOneTimeDeductionStatusAsync(client, employeeId, deductionId));

        // [6] And annulling it reopens nothing, because nothing was closed.
        var annulled = await SendSettlementAsync(client, HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/annulment",
            token, new { reason = "Corrección" });
        Assert.Equal(HttpStatusCode.OK, annulled.StatusCode);
        Assert.Equal("AUTORIZADO", await GetOneTimeDeductionStatusAsync(client, employeeId, deductionId));
    }

    [Fact]
    public async Task OneTimeDeductionSettlement_AnIncludedLine_ChargesTheDeductionOnIssue_AndAnnulReopensIt()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        var (employeeId, plazaId, deductionId) =
            await SeedRetiredEmployeeOwingAOneTimeDeductionAsync(scenario, client, "B");

        var (settlementId, token, document) = await CreateSettlementForAsync(client, employeeId, plazaId);
        using (document)
        {
            Assert.NotNull(OneTimeDeductionLineOrNull(document.RootElement));
        }

        // The line stays INCLUDED (the default) → issuing the settlement charges the debt through it.
        var issued = await SendSettlementAsync(client, HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/issuance",
            token, new { confirmNegativeNet = false });
        var issuedPayload = await issued.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == issued.StatusCode, $"Issue failed: {(int)issued.StatusCode} {issuedPayload}");
        token = await ReadTokenAsync(issued);

        Assert.Equal("APLICADO", await GetOneTimeDeductionStatusAsync(client, employeeId, deductionId));

        // Annulling the settlement reopens exactly what it charged → the debt is owed again.
        var annulled = await SendSettlementAsync(client, HttpMethod.Patch,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/annulment",
            token, new { reason = "Liquidación mal calculada" });
        var annulledPayload = await annulled.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == annulled.StatusCode, $"Annul failed: {(int)annulled.StatusCode} {annulledPayload}");

        Assert.Equal("AUTORIZADO", await GetOneTimeDeductionStatusAsync(client, employeeId, deductionId));
    }

    [Fact]
    public async Task OneTimeDeductionSettlement_WithoutAnAuthorizedDeduction_AddsNoLine()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        var (employeeId, plazaId) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Limpio", "SinDeuda", "EMP-OTDL-C", "limpio.otdl.c@empresa.test");
        var (requesterId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Jefa", "SolicitanteC", "EMP-OTDLR-C", "jefa.otdl.c@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "DeRrhhC", "EMP-OTDLG-C", "gestora.otdl.c@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        await ExecuteRetirementAsync(client, employeeId, requesterId);

        // Retrocompatibility: no debt ⇒ no line. The settlement is exactly what it was before REQ-009.
        var (_, _, document) = await CreateSettlementForAsync(client, employeeId, plazaId);
        using (document)
        {
            Assert.Null(OneTimeDeductionLineOrNull(document.RootElement));
        }
    }
}
