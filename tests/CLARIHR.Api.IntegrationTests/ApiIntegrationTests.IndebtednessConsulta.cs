using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// The indebtedness query and simulation (REQ-010 PR-3). The load-bearing test here is the NO-WRITE one: the
/// levantamiento says the simulation "solo simulación y no debe afectar la planilla", and a simulation that quietly
/// persisted something would be a silent corruption of payroll data.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static async Task<JsonElement> GetIndebtednessAsync(HttpClient client, Guid employeeId)
    {
        var response = await client.GetAsync($"/api/v1/personnel-files/{employeeId}/indebtedness");
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == response.StatusCode, $"Query failed: {(int)response.StatusCode} {payload}");
        return JsonDocument.Parse(payload).RootElement.Clone();
    }

    private static async Task<JsonElement> SimulateIndebtednessAsync(HttpClient client, Guid employeeId, object body)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/personnel-files/{employeeId}/indebtedness/simulation", body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == response.StatusCode, $"Simulation failed: {(int)response.StatusCode} {payload}");
        return JsonDocument.Parse(payload).RootElement.Clone();
    }

    [Fact]
    public async Task Indebtedness_WithNoParameters_ReportsSinControl()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(IndebtednessFlowContext(scenario));

        var (employeeId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Nadia", "SinNada", "EMP-INQ-A", "nadia.inq.a@empresa.test", monthlySalary: 1000m);

        var indebtedness = await GetIndebtednessAsync(client, employeeId);

        // No ceiling configured ⇒ SIN_CONTROL. It is a legitimate state (the FE paints it grey, not red): the
        // figures are still reported, there is just nothing to compare them against.
        Assert.Equal("SIN_CONTROL", indebtedness.GetProperty("status").GetString());
        Assert.Equal(1000m, indebtedness.GetProperty("baseIncome").GetDecimal());
        Assert.Equal(0m, indebtedness.GetProperty("currentLoad").GetDecimal());
        Assert.Equal(JsonValueKind.Null, indebtedness.GetProperty("globalLimitPercent").ValueKind);
        Assert.Empty(indebtedness.GetProperty("overrides").EnumerateArray());
    }

    [Fact]
    public async Task Indebtedness_BreaksDownTheBaseAndTheLoad_AndExcludesTheSuspendedCredits()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(IndebtednessFlowContext(scenario));

        var (employeeId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Cora", "Consulta", "EMP-INQ-B", "cora.inq.b@empresa.test", monthlySalary: 1000m);

        await SetIndebtednessCeilingAsync(client, scenario.TenantId, 50m);

        using var authorizer = factory.CreateClientFor(RecurringDeductionAuthorizerContext(scenario, Guid.NewGuid()));

        // Credit A: authorized → VIGENTE → real load ($200).
        var (creditA, tokenA) = await CreateRecurringDeductionAsync(client, employeeId, IndebtednessCreditBody(200m));
        var authorizedA = await PatchRecurringDeductionAsync(
            authorizer, employeeId, creditA, "resolution", tokenA,
            new { targetStatusCode = "VIGENTE", note = (string?)null });
        authorizedA.EnsureSuccessStatusCode();
        var tokenAfterAuth = await ReadTokenAsync(authorizedA);

        // Credit B: authorized and then SUSPENDED — it must be VISIBLE but must NOT consume capacity (P-12).
        var (creditB, tokenB) = await CreateRecurringDeductionAsync(client, employeeId, IndebtednessCreditBody(100m));
        var authorizedB = await PatchRecurringDeductionAsync(
            authorizer, employeeId, creditB, "resolution", tokenB,
            new { targetStatusCode = "VIGENTE", note = (string?)null });
        authorizedB.EnsureSuccessStatusCode();

        var suspended = await PatchRecurringDeductionAsync(
            client, employeeId, creditB, "suspension", await ReadTokenAsync(authorizedB),
            new { suspend = true, note = "Convenio en revisión" });
        var suspendedPayload = await suspended.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == suspended.StatusCode, $"Suspension failed: {(int)suspended.StatusCode} {suspendedPayload}");

        var indebtedness = await GetIndebtednessAsync(client, employeeId);

        // The load counts ONLY the live credit: 200 of 1,000 = 20%. The suspended $100 is shown and ignored.
        Assert.Equal(200m, indebtedness.GetProperty("currentLoad").GetDecimal());
        Assert.Equal(20m, indebtedness.GetProperty("currentPercent").GetDecimal());
        Assert.Equal("DENTRO", indebtedness.GetProperty("status").GetString());

        var loadLines = indebtedness.GetProperty("loadBreakdown").EnumerateArray().ToArray();
        Assert.Equal(2, loadLines.Length);

        var suspendedLine = loadLines.Single(line => line.GetProperty("statusCode").GetString() == "SUSPENDIDO");
        Assert.False(suspendedLine.GetProperty("isIncludedInLoad").GetBoolean());
        Assert.Equal(100m, suspendedLine.GetProperty("monthlyAmount").GetDecimal());

        var liveLine = loadLines.Single(line => line.GetProperty("statusCode").GetString() == "VIGENTE");
        Assert.True(liveLine.GetProperty("isIncludedInLoad").GetBoolean());
        Assert.Equal("Banco Agrícola", liveLine.GetProperty("financialInstitution").GetString());
        Assert.Equal(50m, liveLine.GetProperty("limitPercent").GetDecimal());   // each row carries ITS ceiling

        // The base is broken down per plaza, each monthly-ized on its own pay period.
        var baseLines = indebtedness.GetProperty("baseBreakdown").EnumerateArray().ToArray();
        Assert.Single(baseLines);
        Assert.Equal("MENSUAL", baseLines[0].GetProperty("payPeriodCode").GetString());
        Assert.Equal(1000m, baseLines[0].GetProperty("monthlyValue").GetDecimal());

        _ = tokenAfterAuth;
    }

    [Fact]
    public async Task IndebtednessSimulation_ProjectsTheAdditionalDeduction_AndWritesNOTHING()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(IndebtednessFlowContext(scenario));

        var (employeeId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Simona", "Simulada", "EMP-INQ-C", "simona.inq.c@empresa.test", monthlySalary: 1000m);

        await SetIndebtednessCeilingAsync(client, scenario.TenantId, 30m);

        // The state of the world BEFORE the simulation.
        var (before, tokensBefore) = await SnapshotWorldAsync(scenario.TenantId);

        var simulation = await SimulateIndebtednessAsync(client, employeeId, new
        {
            baseIncomeOverride = (decimal?)null,
            additionalDeduction = new { amount = 400m, payPeriodCode = "MENSUAL", typeCode = "PRESTAMO_BANCARIO" }
        });

        Assert.Equal(1000m, simulation.GetProperty("baseIncome").GetDecimal());
        Assert.Equal(0m, simulation.GetProperty("currentPercent").GetDecimal());
        Assert.Equal(400m, simulation.GetProperty("additionalMonthlyDeduction").GetDecimal());
        Assert.Equal(40m, simulation.GetProperty("simulatedPercent").GetDecimal());
        Assert.True(simulation.GetProperty("wouldExceed").GetBoolean());
        Assert.Equal("EXCEDIDO", simulation.GetProperty("status").GetString());

        // 🚨 THE assertion of this endpoint: "solo simulación y no debe afectar la planilla". Nothing was created,
        // and no aggregate's concurrency token moved — the handler is a query with no unit of work.
        var (after, tokensAfter) = await SnapshotWorldAsync(scenario.TenantId);
        Assert.Equal(before, after);
        Assert.Equal(tokensBefore, tokensAfter);
    }

    [Fact]
    public async Task IndebtednessSimulation_HonoursTheTypedIncome_AndTheWeeklyCadence()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(IndebtednessFlowContext(scenario));

        var (employeeId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Wanda", "Semanal", "EMP-INQ-D", "wanda.inq.d@empresa.test", monthlySalary: 1000m);

        await SetIndebtednessCeilingAsync(client, scenario.TenantId, 30m);

        // The "ingreso digitado": the operator tries the numbers against a hypothetical $2,000 salary, and a
        // WEEKLY $100 deduction — which is $433.33 of monthly debt, not $100. Comparing the raw installment would
        // let a weekly credit slip four times under the ceiling.
        var simulation = await SimulateIndebtednessAsync(client, employeeId, new
        {
            baseIncomeOverride = (decimal?)2000m,
            additionalDeduction = new { amount = 100m, payPeriodCode = "SEMANAL", typeCode = (string?)null }
        });

        Assert.Equal(2000m, simulation.GetProperty("baseIncome").GetDecimal());
        Assert.Equal(433.33m, simulation.GetProperty("additionalMonthlyDeduction").GetDecimal());
        Assert.Equal(21.67m, simulation.GetProperty("simulatedPercent").GetDecimal());
        Assert.False(simulation.GetProperty("wouldExceed").GetBoolean());
    }

    [Fact]
    public async Task Indebtedness_WithoutTheGrant_IsForbidden()
    {
        var scenario = await factory.ResetDatabaseAsync();

        var (employeeId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Prohibida", "SinPermiso", "EMP-INQ-E", "prohibida.inq.e@empresa.test");

        // An authenticated user with NO ViewIndebtedness grant: the figure is aggregated and sensitive, so it does
        // not ride on the generic file-read permission.
        using var stranger = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        var response = await stranger.GetAsync($"/api/v1/personnel-files/{employeeId}/indebtedness");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Row counts + every concurrency token of the aggregates a rogue write could touch.</summary>
    private async Task<(string Counts, string Tokens)> SnapshotWorldAsync(Guid tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var deductions = await dbContext.Set<PersonnelFileRecurringDeduction>()
            .AsNoTracking().Where(item => item.TenantId == tenantId)
            .OrderBy(item => item.PublicId)
            .Select(item => new { item.PublicId, item.ConcurrencyToken })
            .ToArrayAsync();

        var overrides = await dbContext.Set<PersonnelFileRecurringDeductionIndebtednessOverride>()
            .AsNoTracking().CountAsync(item => item.TenantId == tenantId);

        var preferenceToken = await dbContext.CompanyPreferences
            .AsNoTracking().Where(item => item.TenantId == tenantId)
            .Select(item => item.ConcurrencyToken)
            .SingleOrDefaultAsync();

        var counts = $"deductions={deductions.Length};overrides={overrides}";
        var tokens = string.Join('|', deductions.Select(item => $"{item.PublicId}:{item.ConcurrencyToken}"))
            + $"||preference:{preferenceToken}";

        return (counts, tokens);
    }
}
