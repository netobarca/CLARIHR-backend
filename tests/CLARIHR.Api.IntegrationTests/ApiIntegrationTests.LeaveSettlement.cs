using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// End-to-end coverage of the vacations ↔ settlement integration (REQ-001 RF-019, plan §3.11): the settlement
/// engine seeds the VACACION_PROPORCIONAL suggestion with the employee's pending vacation-fund days when a fund
/// exists, and keeps the legacy <c>DaysSinceAnniversary</c> default (fully retrocompatible) when it does not —
/// while the liquidator's audited unit override survives a subsequent recalculation. Reuses the settlement and
/// vacation-fund helpers of the sibling partials (<see cref="SeedSettlementCandidateAsync"/>,
/// <see cref="ExecuteRetirementAsync"/>, <see cref="CreateManualVacationPeriodAsync"/>).
/// </summary>
public sealed partial class ApiIntegrationTests
{
    /// <summary>Local mirror of <c>SettlementCalculationRules.DaysSinceAnniversary</c> (internal to Application) for the expectation.</summary>
    private static int ExpectedDaysSinceAnniversary(DateTime plazaStartDate, DateTime retirementDate)
    {
        var start = plazaStartDate.Date;
        var end = retirementDate.Date;
        if (end <= start)
        {
            return 0;
        }

        var anniversary = SafeAnniversary(end.Year, start.Month, start.Day);
        if (anniversary > end)
        {
            anniversary = SafeAnniversary(end.Year - 1, start.Month, start.Day);
        }

        return Math.Max(0, (end - anniversary).Days);

        static DateTime SafeAnniversary(int year, int month, int day) =>
            new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));
    }

    private static JsonElement VacationLine(JsonElement settlementRoot) =>
        settlementRoot.GetProperty("lines").EnumerateArray()
            .Single(line => line.GetProperty("conceptCode").GetString() == "VACACION_PROPORCIONAL");

    private static JsonElement LineByConcept(JsonElement settlementRoot, string conceptCode) =>
        settlementRoot.GetProperty("lines").EnumerateArray()
            .Single(line => line.GetProperty("conceptCode").GetString() == conceptCode);

    [Fact]
    public async Task Settlement_VacationProportional_UsesFundPendingDays_AndLiquidatorOverrideSurvivesRecalc()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        var (employeeId, plazaId) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Fabiola", "Fondo", "EMP-LQV-A", "fabiola.lqv.a@empresa.test");
        var (requesterId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Ramiro", "Solicitante", "EMP-LQV-A3", "ramiro.lqv.a@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "DeRrhh", "EMP-LQV-A2", "gestora.lqv.a@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        // A live 2026 fund with 12 pending enjoyment days — created BEFORE the retirement (a retired profile is
        // locked for all writes). No consumption ⇒ available = granted = 12.
        _ = await CreateManualVacationPeriodAsync(client, employeeId, 2026, legal: 12);

        await ExecuteRetirementAsync(client, employeeId, requesterId);

        // [ASSERT 1 — WITH FUND] The initial VACACION_PROPORCIONAL suggestion uses the 12 pending fund days,
        // NOT DaysSinceAnniversary.
        var created = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = plazaId, requestDate = DateTime.UtcNow.Date });
        var createdPayload = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.OK, $"Create failed: {(int)created.StatusCode} {createdPayload}");

        Guid settlementId, token, vacationLineId, salarioLineId;
        using (var doc = JsonDocument.Parse(createdPayload))
        {
            var root = doc.RootElement;
            settlementId = root.GetProperty("publicId").GetGuid();
            token = root.GetProperty("concurrencyToken").GetGuid();

            var vacation = VacationLine(root);
            Assert.Equal(12m, vacation.GetProperty("unitsOrDays").GetDecimal());
            vacationLineId = vacation.GetProperty("publicId").GetGuid();
            salarioLineId = LineByConcept(root, "SALARIO").GetProperty("publicId").GetGuid();

            // Sanity: 12 pending fund days ≠ the anniversary the legacy path would have produced.
            var anniversary = ExpectedDaysSinceAnniversary(
                root.GetProperty("plazaStartDate").GetDateTime(), root.GetProperty("retirementDate").GetDateTime());
            Assert.NotEqual(12m, (decimal)anniversary);
        }

        // [ASSERT 3 — OVERRIDE] The liquidator overrides the suggested units to 5 (audited manual edit).
        var overridden = await SendSettlementAsync(client, HttpMethod.Put,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/lines/{vacationLineId}",
            token, new { unitsOrDays = 5m });
        Assert.Equal(HttpStatusCode.OK, overridden.StatusCode);
        using (var doc = await ReadJsonAsync(overridden))
        {
            token = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
            Assert.Equal(5m, VacationLine(doc.RootElement).GetProperty("unitsOrDays").GetDecimal());
        }

        // A subsequent recalculation (toggling the SALARIO line, which reruns the engine over the persisted
        // lines) must PRESERVE the liquidator's unit override — my change only touched the initial suggestion.
        var recalculated = await SendSettlementAsync(client, HttpMethod.Put,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/lines/{salarioLineId}",
            token, new { isIncluded = true });
        Assert.Equal(HttpStatusCode.OK, recalculated.StatusCode);
        using (var doc = await ReadJsonAsync(recalculated))
        {
            token = doc.RootElement.GetProperty("concurrencyToken").GetGuid();
            Assert.Equal(5m, VacationLine(doc.RootElement).GetProperty("unitsOrDays").GetDecimal());
        }

        // Regenerating from scratch (the conscious "throw away adjustments" path) discards the override and
        // re-reads the fund → the suggestion is the 12 pending days again.
        var regenerated = await SendSettlementAsync(client, HttpMethod.Post,
            $"/api/v1/personnel-files/{employeeId}/settlements/{settlementId}/lines/regenerate",
            token, body: null);
        Assert.Equal(HttpStatusCode.OK, regenerated.StatusCode);
        using (var doc = await ReadJsonAsync(regenerated))
        {
            Assert.Equal(12m, VacationLine(doc.RootElement).GetProperty("unitsOrDays").GetDecimal());
        }
    }

    [Fact]
    public async Task Settlement_VacationProportional_FallsBackToAnniversary_WhenNoFund()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateRetirementContext(scenario));

        var (employeeId, plazaId) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Nelson", "SinFondo", "EMP-LQV-B", "nelson.lqv.b@empresa.test");
        var (requesterId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Rosa", "Solicitante", "EMP-LQV-B3", "rosa.lqv.b@empresa.test");
        _ = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Gestora", "DeRrhh", "EMP-LQV-B2", "gestora.lqv.b@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);

        // NO vacation fund for this employee.
        await ExecuteRetirementAsync(client, employeeId, requesterId);

        // [ASSERT 2 — WITHOUT FUND] The suggestion falls back to DaysSinceAnniversary (retrocompatible).
        var created = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/settlements",
            new { assignedPositionPublicId = plazaId, requestDate = DateTime.UtcNow.Date });
        var createdPayload = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.OK, $"Create failed: {(int)created.StatusCode} {createdPayload}");

        using var doc = JsonDocument.Parse(createdPayload);
        var root = doc.RootElement;
        var expected = ExpectedDaysSinceAnniversary(
            root.GetProperty("plazaStartDate").GetDateTime(), root.GetProperty("retirementDate").GetDateTime());
        Assert.True(expected > 0, "The anniversary baseline should be a positive number of days.");
        Assert.Equal((decimal)expected, VacationLine(root).GetProperty("unitsOrDays").GetDecimal());
    }
}
