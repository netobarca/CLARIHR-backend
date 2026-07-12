using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Integration coverage for the one-time-deduction APPLICATION slice (REQ-009 PR-4): the single charge (a second
/// attempt is rejected), the REVERSAL (the deduction returns to AUTORIZADO and can be charged again), the
/// company-wide pending work list, the atomic apply-period batch with exclusions, and the CONCURRENCY RACE — two
/// simultaneous charges on the same deduction: exactly one wins, and the employee is never charged twice.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private async Task<(Guid DeductionId, Guid Token)> CreateAndAuthorizeOneTimeDeductionAsync(
        IntegrationTestScenario scenario,
        HttpClient manager,
        Guid fileId,
        Guid requesterId,
        object? body = null)
    {
        var (deductionId, token) = await CreateOneTimeDeductionAsync(
            manager, fileId, body ?? FixedOneTimeDeductionBody(requesterId));

        using var authorizer = factory.CreateClientFor(OneTimeDeductionAuthorizerContext(scenario, Guid.NewGuid()));
        var response = await PatchOneTimeDeductionAsync(
            authorizer, fileId, deductionId, "resolution", token, new { targetStatusCode = "AUTORIZADO", note = (string?)null });
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (deductionId, doc.RootElement.GetProperty("concurrencyToken").GetGuid());
    }

    private static async Task<HttpResponseMessage> ApplyOneTimeDeductionAsync(
        HttpClient client, Guid fileId, Guid deductionId, Guid token)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/personnel-files/{fileId}/one-time-deductions/{deductionId}/applications")
        {
            Content = JsonContent.Create(new { appliedDate = (DateOnly?)null, payrollPeriodPublicId = (Guid?)null, notes = (string?)null })
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{token}\"");
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task OneTimeDeductionApplications_ChargeOnce_ThenASecondAttemptIsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OneTimeDeductionManagerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Ana", "Aplicada", "EMP-OTDA-A", "ana.otda.a@empresa.test");
        var requesterId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Jefa", "Solicitante", "EMP-OTDA-A2", "jefa.otda.a@empresa.test");

        var (deductionId, token) = await CreateAndAuthorizeOneTimeDeductionAsync(scenario, manager, fileId, requesterId);

        var applied = await ApplyOneTimeDeductionAsync(manager, fileId, deductionId, token);
        var payload = await applied.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == applied.StatusCode, payload);

        using (var doc = JsonDocument.Parse(payload))
        {
            Assert.Equal("APLICADA", doc.RootElement.GetProperty("application").GetProperty("statusCode").GetString());
            Assert.Equal("MANUAL", doc.RootElement.GetProperty("application").GetProperty("originCode").GetString());
            Assert.Equal("APLICADO", doc.RootElement.GetProperty("oneTimeDeductionStatusCode").GetString());
            token = doc.RootElement.GetProperty("oneTimeDeductionConcurrencyToken").GetGuid();
        }

        // A one-off deduction is charged ONCE: the employee must never pay the same fine twice.
        var second = await ApplyOneTimeDeductionAsync(manager, fileId, deductionId, token);
        await AssertProblemDetailsAsync(second, HttpStatusCode.UnprocessableEntity, "ONE_TIME_DEDUCTION_NOT_APPLICABLE");
    }

    [Fact]
    public async Task OneTimeDeductionApplications_TheReversal_ReturnsItToAuthorized_AndItCanBeChargedAgain()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OneTimeDeductionManagerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Rita", "Revertida", "EMP-OTDA-B", "rita.otda.b@empresa.test");
        var requesterId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Jefe", "Solicitante", "EMP-OTDA-B2", "jefe.otda.b@empresa.test");

        var (deductionId, token) = await CreateAndAuthorizeOneTimeDeductionAsync(scenario, manager, fileId, requesterId);

        var applied = await ApplyOneTimeDeductionAsync(manager, fileId, deductionId, token);
        applied.EnsureSuccessStatusCode();
        Guid applicationId;
        using (var doc = JsonDocument.Parse(await applied.Content.ReadAsStringAsync()))
        {
            applicationId = doc.RootElement.GetProperty("application").GetProperty("applicationPublicId").GetGuid();
            token = doc.RootElement.GetProperty("oneTimeDeductionConcurrencyToken").GetGuid();
        }

        // THE REVERSAL: the charge was a mistake, so it is undone.
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/personnel-files/{fileId}/one-time-deductions/{deductionId}/applications/{applicationId}/annulment")
        {
            Content = JsonContent.Create(new { reason = "Se cobró en la planilla equivocada" })
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{token}\"");

        var reverted = await manager.SendAsync(request);
        var revertedPayload = await reverted.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == reverted.StatusCode, revertedPayload);

        using (var doc = JsonDocument.Parse(revertedPayload))
        {
            Assert.Equal("ANULADA", doc.RootElement.GetProperty("application").GetProperty("statusCode").GetString());
            // The deduction goes BACK to AUTORIZADO — the debt still stands, it just was not charged yet.
            Assert.Equal("AUTORIZADO", doc.RootElement.GetProperty("oneTimeDeductionStatusCode").GetString());
            token = doc.RootElement.GetProperty("oneTimeDeductionConcurrencyToken").GetGuid();
        }

        // And it can be charged AGAIN (the filtered-unique index freed the slot).
        var reapplied = await ApplyOneTimeDeductionAsync(manager, fileId, deductionId, token);
        reapplied.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await reapplied.Content.ReadAsStringAsync()))
        {
            Assert.Equal("APLICADO", doc.RootElement.GetProperty("oneTimeDeductionStatusCode").GetString());
        }

        // The history keeps BOTH: the annulled charge and the good one — the audit trail is intact.
        var history = await manager.GetAsync(
            $"/api/v1/personnel-files/{fileId}/one-time-deductions/{deductionId}/applications");
        history.EnsureSuccessStatusCode();
        using var doc2 = JsonDocument.Parse(await history.Content.ReadAsStringAsync());
        Assert.Equal(2, doc2.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task OneTimeDeductionApplications_RevertingAnUnappliedDeduction_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OneTimeDeductionManagerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Nora", "NoAplicada", "EMP-OTDA-C", "nora.otda.c@empresa.test");
        var requesterId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Jefa", "Solicitante", "EMP-OTDA-C2", "jefa.otda.c@empresa.test");

        var (deductionId, token) = await CreateAndAuthorizeOneTimeDeductionAsync(scenario, manager, fileId, requesterId);

        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/personnel-files/{fileId}/one-time-deductions/{deductionId}/applications/{Guid.NewGuid()}/annulment")
        {
            Content = JsonContent.Create(new { reason = "No hay nada que revertir" })
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{token}\"");

        var response = await manager.SendAsync(request);
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "ONE_TIME_DEDUCTION_APPLICATION_NOT_REVERTIBLE");
    }

    [Fact]
    public async Task OneTimeDeductionApplications_PendingWorkList_ListsTheAuthorizedOnesNotYetCharged()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OneTimeDeductionManagerContext(scenario, Guid.NewGuid()));

        var fileA = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Pendiente", "Uno", "EMP-OTDA-D", "pend.otda.d@empresa.test");
        var fileB = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Cobrada", "Dos", "EMP-OTDA-E", "cob.otda.e@empresa.test");
        var requesterId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Jefe", "Solicitante", "EMP-OTDA-D2", "jefe.otda.d@empresa.test");

        // A is authorized and NOT charged; B is authorized and charged.
        await CreateAndAuthorizeOneTimeDeductionAsync(scenario, manager, fileA, requesterId);
        var (deductionB, tokenB) = await CreateAndAuthorizeOneTimeDeductionAsync(scenario, manager, fileB, requesterId);
        (await ApplyOneTimeDeductionAsync(manager, fileB, deductionB, tokenB)).EnsureSuccessStatusCode();

        var response = await manager.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/one-time-deductions/pending/query",
            new { payrollTypeCode = "MENSUAL", payrollPeriodPublicId = (Guid?)null, employeeId = (Guid?)null });
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == response.StatusCode, payload);

        using var doc = JsonDocument.Parse(payload);
        // Only A is still waiting: the charged one is no longer work.
        Assert.Equal(1, doc.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(fileA, doc.RootElement.GetProperty("items")[0].GetProperty("personnelFilePublicId").GetGuid());
        Assert.Equal(75m, doc.RootElement.GetProperty("items")[0].GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task OneTimeDeductionApplications_ApplyPeriodBatch_ChargesAllAndPostponesTheExcluded()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OneTimeDeductionManagerContext(scenario, Guid.NewGuid()));

        var fileA = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Lote", "Uno", "EMP-OTDA-F", "lote.otda.f@empresa.test");
        var fileB = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Lote", "Dos", "EMP-OTDA-G", "lote.otda.g@empresa.test");
        var requesterId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Jefa", "Solicitante", "EMP-OTDA-F2", "jefa.otda.f@empresa.test");

        await CreateAndAuthorizeOneTimeDeductionAsync(scenario, manager, fileA, requesterId);
        var (deductionB, _) = await CreateAndAuthorizeOneTimeDeductionAsync(scenario, manager, fileB, requesterId);

        var response = await manager.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/one-time-deductions/apply-period",
            new
            {
                payrollTypeCode = "MENSUAL",
                payrollPeriodPublicId = (Guid?)null,
                excludedDeductionPublicIds = new[] { deductionB }
            });

        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == response.StatusCode, payload);

        using var doc = JsonDocument.Parse(payload);
        // A was charged; B was excluded, so it stays pending for the next run.
        Assert.Equal(1, doc.RootElement.GetProperty("aplicados").GetInt32());
        Assert.Equal(1, doc.RootElement.GetProperty("pospuestos").GetInt32());

        var pending = await manager.PostAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/one-time-deductions/pending/query",
            new { payrollTypeCode = "MENSUAL", payrollPeriodPublicId = (Guid?)null, employeeId = (Guid?)null });
        pending.EnsureSuccessStatusCode();
        using var pendingDoc = JsonDocument.Parse(await pending.Content.ReadAsStringAsync());
        Assert.Equal(1, pendingDoc.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task OneTimeDeductionApplications_ConcurrentCharges_OnlyOneWins()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OneTimeDeductionManagerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Carrera", "Concurrente", "EMP-OTDA-H", "carrera.otda.h@empresa.test");
        var requesterId = await SeedOneTimeDeductionCandidateAsync(
            scenario.TenantId, "Jefe", "Solicitante", "EMP-OTDA-H2", "jefe.otda.h@empresa.test");

        var (deductionId, token) = await CreateAndAuthorizeOneTimeDeductionAsync(scenario, manager, fileId, requesterId);

        // Two clients race to charge the SAME deduction with the same If-Match. The advisory lock serializes them
        // and the optimistic token rejects the loser: the employee is charged exactly once.
        using var clientA = factory.CreateClientFor(OneTimeDeductionManagerContext(scenario, Guid.NewGuid()));
        using var clientB = factory.CreateClientFor(OneTimeDeductionManagerContext(scenario, Guid.NewGuid()));

        var first = ApplyOneTimeDeductionAsync(clientA, fileId, deductionId, token);
        var second = ApplyOneTimeDeductionAsync(clientB, fileId, deductionId, token);
        var responses = await Task.WhenAll(first, second);

        var succeeded = responses.Count(response => response.StatusCode == HttpStatusCode.OK);
        var conflicted = responses.Count(response => response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity);

        Assert.Equal(1, succeeded);
        Assert.Equal(1, conflicted);

        // Exactly ONE application exists — the fine was never double-charged.
        var history = await manager.GetAsync(
            $"/api/v1/personnel-files/{fileId}/one-time-deductions/{deductionId}/applications");
        history.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await history.Content.ReadAsStringAsync());
        Assert.Equal(1, doc.RootElement.GetArrayLength());

        foreach (var response in responses)
        {
            response.Dispose();
        }
    }
}
