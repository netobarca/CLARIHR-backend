using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Integration coverage for the one-time-income application slice (REQ-006 PR-4): the UNITARY application (RF-011,
/// AUTORIZADO → APLICADO, amount from the record, payroll-period FK snapshot), the annulment-with-reopening (RF-013:
/// APLICADO → AUTORIZADO → re-apply — the partial-unique index allows the re-application), the application history,
/// the company-wide apply-period batch (RF-012: postponement via exclusion, catch-up of "atrasados", atomicity),
/// the CARRERA (two concurrent apply-period runs → no duplicate applications — the advisory lock + partial-unique
/// index net), the pending/overdue tray, and the If-Match concurrency guard.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private async Task<(Guid IncomeId, Guid Token)> CreateAndAuthorizeOneTimeIncomeAsync(
        HttpClient manager, HttpClient authorizer, Guid fileId, object body)
    {
        var (incomeId, createToken) = await CreateOneTimeIncomeAsync(manager, fileId, body);
        var authorizeResponse = await PatchOneTimeIncomeAsync(
            authorizer, fileId, incomeId, "resolution", createToken, new { targetStatusCode = "AUTORIZADO", note = (string?)null });
        authorizeResponse.EnsureSuccessStatusCode();
        return (incomeId, await ReadTokenAsync(authorizeResponse));
    }

    private static async Task<HttpResponseMessage> PostOneTimeIncomeApplicationAsync(
        HttpClient client, Guid fileId, Guid incomeId, Guid? incomeToken, object? body = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/personnel-files/{fileId}/one-time-incomes/{incomeId}/applications")
        {
            Content = JsonContent.Create(body ?? new { })
        };
        if (incomeToken is { } value)
        {
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{value}\"");
        }

        return await client.SendAsync(request);
    }

    private static async Task<(Guid ApplicationId, string ApplicationStatus, string IncomeStatus, Guid IncomeToken)>
        ApplyOneTimeIncomeAsync(HttpClient client, Guid fileId, Guid incomeId, Guid incomeToken, object? body = null)
    {
        var response = await PostOneTimeIncomeApplicationAsync(client, fileId, incomeId, incomeToken, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, $"Apply failed: {(int)response.StatusCode} {payload}");
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var application = root.GetProperty("application");
        return (
            application.GetProperty("applicationPublicId").GetGuid(),
            application.GetProperty("statusCode").GetString()!,
            root.GetProperty("oneTimeIncomeStatusCode").GetString()!,
            root.GetProperty("oneTimeIncomeConcurrencyToken").GetGuid());
    }

    private Task<HttpResponseMessage> ApplyOneTimeIncomePeriodAsync(HttpClient client, Guid companyId, object body) =>
        client.PostAsJsonAsync($"/api/v1/companies/{companyId}/one-time-incomes/apply-period", body);

    private Task<HttpResponseMessage> QueryOneTimeIncomePendingAsync(HttpClient client, Guid companyId, object body) =>
        client.PostAsJsonAsync($"/api/v1/companies/{companyId}/one-time-incomes/pending/query", body);

    private static async Task<JsonDocument> GetOneTimeIncomeApplicationsAsync(HttpClient client, Guid fileId, Guid incomeId)
    {
        var response = await client.GetAsync($"/api/v1/personnel-files/{fileId}/one-time-incomes/{incomeId}/applications");
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static object OverdueOneTimeIncomeBody(Guid requesterFilePublicId, DateOnly endDate, string payrollTypeCode = "QUINCENAL")
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        return new
        {
            incomeDate = today,
            reference = "ATRASADO",
            conceptTypeCode = "BONO",
            observations = (string?)null,
            isFixedValue = true,
            calculationMethod = (string?)null,
            quantity = (decimal?)null,
            unitValue = (decimal?)null,
            multiplier = (decimal?)null,
            percentage = (decimal?)null,
            baseAmount = (decimal?)null,
            amount = (decimal?)120m,
            currencyCode = "USD",
            assignedPositionPublicId = (Guid?)null,
            requesterFilePublicId,
            payrollTypeCode,
            payrollPeriodPublicId = (Guid?)null,
            payrollPeriodLabel = "Quincena vencida",
            payrollPeriodEndDate = endDate.ToString("yyyy-MM-dd")
        };
    }

    [Fact]
    public async Task OneTimeIncomeApplications_ApplyThenHistory_MovesToAplicado()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario));
        using var authorizer = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Ada", "Aplicada", "EMP-OTI-APP-A", "ada.otiapp.a@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Rene", "Solicitante", "EMP-OTI-APP-A2", "rene.otiapp.a@empresa.test");

        var (incomeId, token) = await CreateAndAuthorizeOneTimeIncomeAsync(manager, authorizer, fileId, FixedOneTimeIncomeBody(requesterId));

        var applied = await ApplyOneTimeIncomeAsync(manager, fileId, incomeId, token);
        Assert.Equal("APLICADA", applied.ApplicationStatus);
        Assert.Equal("APLICADO", applied.IncomeStatus);

        // The income now reflects APLICADO.
        var detail = await manager.GetAsync($"/api/v1/personnel-files/{fileId}/one-time-incomes/{incomeId}");
        detail.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await detail.Content.ReadAsStringAsync()))
        {
            Assert.Equal("APLICADO", doc.RootElement.GetProperty("statusCode").GetString());
        }

        using var history = await GetOneTimeIncomeApplicationsAsync(manager, fileId, incomeId);
        var items = history.RootElement.EnumerateArray().ToArray();
        Assert.Single(items);
        Assert.Equal("APLICADA", items[0].GetProperty("statusCode").GetString());
        Assert.Equal("MANUAL", items[0].GetProperty("originCode").GetString());
        // The application defaults to the income's declared destination label.
        Assert.Equal("Quincena 13/2026", items[0].GetProperty("payrollPeriodLabel").GetString());
    }

    [Fact]
    public async Task OneTimeIncomeApplications_ApplyWithPayrollPeriod_SnapshotsFkAndLabel()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario));
        using var authorizer = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Petra", "Periodo", "EMP-OTI-APP-B", "petra.otiapp.b@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Raúl", "Solicitante", "EMP-OTI-APP-B2", "raul.otiapp.b@empresa.test");

        var (incomeId, token) = await CreateAndAuthorizeOneTimeIncomeAsync(manager, authorizer, fileId, FixedOneTimeIncomeBody(requesterId));

        var periodId = await SeedPayrollPeriodAsync(
            scenario.TenantId, "QUINCENAL", 2026, 14, new DateOnly(2026, 7, 16), new DateOnly(2026, 7, 31));

        var applied = await ApplyOneTimeIncomeAsync(manager, fileId, incomeId, token, new { payrollPeriodPublicId = periodId });
        Assert.Equal("APLICADO", applied.IncomeStatus);

        using var history = await GetOneTimeIncomeApplicationsAsync(manager, fileId, incomeId);
        var item = history.RootElement.EnumerateArray().Single();
        Assert.Equal(periodId, item.GetProperty("payrollPeriodPublicId").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("payrollPeriodLabel").GetString()));
    }

    [Fact]
    public async Task OneTimeIncomeApplications_AnnulApplication_ReopensThenReapply()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario));
        using var authorizer = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Rita", "Reapertura", "EMP-OTI-APP-C", "rita.otiapp.c@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Rodi", "Solicitante", "EMP-OTI-APP-C2", "rodi.otiapp.c@empresa.test");

        var (incomeId, token) = await CreateAndAuthorizeOneTimeIncomeAsync(manager, authorizer, fileId, FixedOneTimeIncomeBody(requesterId));

        var applied = await ApplyOneTimeIncomeAsync(manager, fileId, incomeId, token);

        // Annul (revert) the active application → the income reopens to AUTORIZADO.
        using var annulRequest = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/personnel-files/{fileId}/one-time-incomes/{incomeId}/applications/{applied.ApplicationId}/annulment")
        {
            Content = JsonContent.Create(new { reason = "Aplicación equivocada" })
        };
        annulRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{applied.IncomeToken}\"");
        var annulResponse = await manager.SendAsync(annulRequest);
        var annulPayload = await annulResponse.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == annulResponse.StatusCode, annulPayload);

        Guid reopenedToken;
        using (var doc = JsonDocument.Parse(annulPayload))
        {
            Assert.Equal("ANULADA", doc.RootElement.GetProperty("application").GetProperty("statusCode").GetString());
            Assert.Equal("AUTORIZADO", doc.RootElement.GetProperty("oneTimeIncomeStatusCode").GetString());
            reopenedToken = doc.RootElement.GetProperty("oneTimeIncomeConcurrencyToken").GetGuid();
        }

        // Re-apply → APLICADO again (the partial unique index allows a new active application).
        var reapplied = await ApplyOneTimeIncomeAsync(manager, fileId, incomeId, reopenedToken);
        Assert.Equal("APLICADO", reapplied.IncomeStatus);

        // History carries both the ANULADA and the new APLICADA.
        using var history = await GetOneTimeIncomeApplicationsAsync(manager, fileId, incomeId);
        var items = history.RootElement.EnumerateArray().ToArray();
        Assert.Equal(2, items.Length);
        Assert.Single(items, item => item.GetProperty("statusCode").GetString() == "APLICADA");
        Assert.Single(items, item => item.GetProperty("statusCode").GetString() == "ANULADA");
    }

    [Fact]
    public async Task OneTimeIncomeApplications_ApplyOnNonAutorizado_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario));

        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Nora", "NoAutorizada", "EMP-OTI-APP-D", "nora.otiapp.d@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Rux", "Solicitante", "EMP-OTI-APP-D2", "rux.otiapp.d@empresa.test");

        // EN_REVISION (never authorized) → apply → 422 NOT_APPLICABLE.
        var (incomeId, token) = await CreateOneTimeIncomeAsync(manager, fileId, FixedOneTimeIncomeBody(requesterId));

        var response = await PostOneTimeIncomeApplicationAsync(manager, fileId, incomeId, token);
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "ONE_TIME_INCOME_NOT_APPLICABLE");
    }

    [Fact]
    public async Task OneTimeIncomeApplications_ApplyPeriodBatch_AppliesAndPostpones()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario));
        using var authorizer = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var file1 = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Uno", "Puntual", "EMP-OTI-APP-E1", "uno.otiapp.e@empresa.test");
        var file2 = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Dos", "Pospuesto", "EMP-OTI-APP-E2", "dos.otiapp.e@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Rea", "Solicitante", "EMP-OTI-APP-E3", "rea.otiapp.e@empresa.test");

        var (income1, _) = await CreateAndAuthorizeOneTimeIncomeAsync(manager, authorizer, file1, FixedOneTimeIncomeBody(requesterId));
        var (income2, _) = await CreateAndAuthorizeOneTimeIncomeAsync(manager, authorizer, file2, FixedOneTimeIncomeBody(requesterId));

        // Batch 1: exclude income2 → only income1 applies; income2 is postponed.
        var batch1 = await ApplyOneTimeIncomePeriodAsync(manager, scenario.TenantId, new
        {
            payrollTypeCode = "QUINCENAL",
            payrollPeriodPublicId = (Guid?)null,
            payrollPeriodLabel = "Quincena 13/2026",
            excludedIncomePublicIds = new[] { income2 }
        });
        var batch1Payload = await batch1.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == batch1.StatusCode, batch1Payload);
        using (var doc = JsonDocument.Parse(batch1Payload))
        {
            Assert.Equal(1, doc.RootElement.GetProperty("aplicados").GetInt32());
            Assert.Equal(1, doc.RootElement.GetProperty("pospuestos").GetInt32());
        }

        using (var history2 = await GetOneTimeIncomeApplicationsAsync(manager, file2, income2))
        {
            Assert.Empty(history2.RootElement.EnumerateArray());
        }

        // Batch 2: no exclusions → income2 (still AUTORIZADO) catches up; income1 is already APLICADO (skipped).
        var batch2 = await ApplyOneTimeIncomePeriodAsync(manager, scenario.TenantId, new
        {
            payrollTypeCode = "QUINCENAL",
            payrollPeriodPublicId = (Guid?)null,
            payrollPeriodLabel = "Quincena 13/2026",
            excludedIncomePublicIds = Array.Empty<Guid>()
        });
        var batch2Payload = await batch2.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == batch2.StatusCode, batch2Payload);
        using (var doc = JsonDocument.Parse(batch2Payload))
        {
            Assert.Equal(1, doc.RootElement.GetProperty("aplicados").GetInt32());
            Assert.Equal(0, doc.RootElement.GetProperty("pospuestos").GetInt32());
        }

        using var history2b = await GetOneTimeIncomeApplicationsAsync(manager, file2, income2);
        Assert.Single(history2b.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task OneTimeIncomeApplications_ConcurrentApplyPeriod_NoDuplicateApplications()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario));
        using var authorizer = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Kara", "Carrera", "EMP-OTI-APP-F", "kara.otiapp.f@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Rob", "Solicitante", "EMP-OTI-APP-F2", "rob.otiapp.f@empresa.test");

        var (incomeId, _) = await CreateAndAuthorizeOneTimeIncomeAsync(manager, authorizer, fileId, FixedOneTimeIncomeBody(requesterId));

        object BatchBody() => new
        {
            payrollTypeCode = "QUINCENAL",
            payrollPeriodPublicId = (Guid?)null,
            payrollPeriodLabel = "Quincena 13/2026",
            excludedIncomePublicIds = Array.Empty<Guid>()
        };

        // Two concurrent apply-period runs of the SAME filter: the advisory lock serializes them; the partial
        // unique index is the final net → the income is applied exactly once (no 500).
        var runA = ApplyOneTimeIncomePeriodAsync(manager, scenario.TenantId, BatchBody());
        var runB = ApplyOneTimeIncomePeriodAsync(manager, scenario.TenantId, BatchBody());
        var responses = await Task.WhenAll(runA, runB);

        foreach (var response in responses)
        {
            Assert.True(
                response.StatusCode is HttpStatusCode.OK or HttpStatusCode.UnprocessableEntity,
                $"Unexpected status {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        // Exactly one active APLICADA application across both runs.
        using var history = await GetOneTimeIncomeApplicationsAsync(manager, fileId, incomeId);
        var applied = history.RootElement.EnumerateArray()
            .Where(item => item.GetProperty("statusCode").GetString() == "APLICADA")
            .ToArray();
        Assert.Single(applied);

        var conflicts = responses.Where(response => response.StatusCode == HttpStatusCode.UnprocessableEntity).ToArray();
        if (conflicts.Length > 0)
        {
            await AssertProblemDetailsAsync(conflicts[0], HttpStatusCode.UnprocessableEntity, "ONE_TIME_INCOME_APPLY_PERIOD_CONFLICT");
        }
    }

    [Fact]
    public async Task OneTimeIncomeApplications_PendingTray_MarksOverdue_AndDropsAfterApply()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario));
        using var authorizer = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Olga", "Overdue", "EMP-OTI-APP-G", "olga.otiapp.g@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Rin", "Solicitante", "EMP-OTI-APP-G2", "rin.otiapp.g@empresa.test");

        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var (incomeId, token) = await CreateAndAuthorizeOneTimeIncomeAsync(
            manager, authorizer, fileId, OverdueOneTimeIncomeBody(requesterId, yesterday));

        // The pending tray lists the AUTORIZADO income with the overdue mark.
        var pending = await QueryOneTimeIncomePendingAsync(manager, scenario.TenantId, new { payrollTypeCode = "QUINCENAL", onlyOverdue = (bool?)null });
        pending.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await pending.Content.ReadAsStringAsync()))
        {
            var row = doc.RootElement.GetProperty("items").EnumerateArray()
                .Single(item => item.GetProperty("oneTimeIncomePublicId").GetGuid() == incomeId);
            Assert.True(row.GetProperty("isOverdue").GetBoolean());
            Assert.Equal(1, doc.RootElement.GetProperty("overdueCount").GetInt32());
        }

        // Apply it → it drops out of the pending tray.
        _ = await ApplyOneTimeIncomeAsync(manager, fileId, incomeId, token);

        var afterApply = await QueryOneTimeIncomePendingAsync(manager, scenario.TenantId, new { payrollTypeCode = "QUINCENAL", onlyOverdue = (bool?)null });
        afterApply.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await afterApply.Content.ReadAsStringAsync()))
        {
            Assert.DoesNotContain(
                doc.RootElement.GetProperty("items").EnumerateArray(),
                item => item.GetProperty("oneTimeIncomePublicId").GetGuid() == incomeId);
        }
    }

    [Fact]
    public async Task OneTimeIncomeApplications_ApplyWithoutIfMatch_Returns400_StaleReturns409()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OneTimeIncomeManagerContext(scenario));
        using var authorizer = factory.CreateClientFor(OneTimeIncomeAuthorizerContext(scenario, Guid.NewGuid()));

        var fileId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Ivo", "IfMatch", "EMP-OTI-APP-H", "ivo.otiapp.h@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(
            scenario.TenantId, "Ras", "Solicitante", "EMP-OTI-APP-H2", "ras.otiapp.h@empresa.test");

        var (incomeId, _) = await CreateAndAuthorizeOneTimeIncomeAsync(manager, authorizer, fileId, FixedOneTimeIncomeBody(requesterId));

        // Missing If-Match → 400.
        var missing = await PostOneTimeIncomeApplicationAsync(manager, fileId, incomeId, incomeToken: null);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        // Stale If-Match → 409.
        var stale = await PostOneTimeIncomeApplicationAsync(manager, fileId, incomeId, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }
}
