using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Integration coverage for the overtime-record application slice (REQ-007 PR-4): the UNITARY application (RF-011,
/// AUTORIZADA → APLICADA, hours from the record, payroll-period FK snapshot), the elapsed-work-date guard (№13 — a
/// FUTURE organized shift → 422 <c>OVERTIME_WORK_DATE_NOT_ELAPSED</c>), the annulment-with-reopening (RF-013:
/// APLICADA → AUTORIZADA → re-apply — the partial-unique index allows the re-application), the application history,
/// the company-wide apply-period batch (RF-012: postponement via exclusion, catch-up of "atrasados", atomicity), the
/// CARRERA (two concurrent apply-period runs → no duplicate applications — the advisory lock + partial-unique index
/// net), the pending/overdue tray, and the If-Match concurrency guard. Reuses the PR-3 overtime helpers (masters,
/// body, create, resolution PATCH) and <c>SeedOneTimeIncomeCandidateAsync</c> (completed employee + primary assignment).
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private async Task<(Guid RecordId, Guid Token)> CreateAndAuthorizeOvertimeAsync(
        HttpClient manager, HttpClient authorizer, Guid fileId, object body)
    {
        var (recordId, createToken) = await CreateOvertimeAsync(manager, fileId, body);
        var authorizeResponse = await PatchOvertimeAsync(
            authorizer, fileId, recordId, "resolution", createToken, new { targetStatusCode = "AUTORIZADA", note = (string?)null });
        authorizeResponse.EnsureSuccessStatusCode();
        return (recordId, await ReadTokenAsync(authorizeResponse));
    }

    private static object OverdueOvertimeBody(
        Guid typeId, Guid justId, Guid requesterId, DateOnly endDate, int workDateOffsetDays = -1)
    {
        var workDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(workDateOffsetDays).ToString("yyyy-MM-dd");
        return new
        {
            workDate,
            overtimeTypePublicId = typeId,
            factorApplied = (decimal?)null,
            factorOverrideNote = (string?)null,
            durationHours = 2,
            durationMinutes = 30,
            startTime = (string?)null,
            endTime = (string?)null,
            justificationTypePublicId = justId,
            observations = (string?)null,
            assignedPositionPublicId = (Guid?)null,
            requesterFilePublicId = requesterId,
            payrollTypeCode = "QUINCENAL",
            payrollPeriodPublicId = (Guid?)null,
            payrollPeriodLabel = "Quincena vencida",
            payrollPeriodEndDate = endDate.ToString("yyyy-MM-dd")
        };
    }

    private static async Task<HttpResponseMessage> PostOvertimeApplicationAsync(
        HttpClient client, Guid fileId, Guid recordId, Guid? recordToken, object? body = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/personnel-files/{fileId}/overtime-records/{recordId}/applications")
        {
            Content = JsonContent.Create(body ?? new { })
        };
        if (recordToken is { } value)
        {
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{value}\"");
        }

        return await client.SendAsync(request);
    }

    private static async Task<(Guid ApplicationId, string ApplicationStatus, string RecordStatus, Guid RecordToken)>
        ApplyOvertimeRecordAsync(HttpClient client, Guid fileId, Guid recordId, Guid recordToken, object? body = null)
    {
        var response = await PostOvertimeApplicationAsync(client, fileId, recordId, recordToken, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, $"Apply failed: {(int)response.StatusCode} {payload}");
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var application = root.GetProperty("application");
        return (
            application.GetProperty("applicationPublicId").GetGuid(),
            application.GetProperty("statusCode").GetString()!,
            root.GetProperty("overtimeRecordStatusCode").GetString()!,
            root.GetProperty("overtimeRecordConcurrencyToken").GetGuid());
    }

    private Task<HttpResponseMessage> ApplyOvertimePeriodAsync(HttpClient client, Guid companyId, object body) =>
        client.PostAsJsonAsync($"/api/v1/companies/{companyId}/overtime-records/apply-period", body);

    private Task<HttpResponseMessage> QueryOvertimePendingAsync(HttpClient client, Guid companyId, object body) =>
        client.PostAsJsonAsync($"/api/v1/companies/{companyId}/overtime-records/pending/query", body);

    private static async Task<JsonDocument> GetOvertimeApplicationsAsync(HttpClient client, Guid fileId, Guid recordId)
    {
        var response = await client.GetAsync($"/api/v1/personnel-files/{fileId}/overtime-records/{recordId}/applications");
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OvertimeApplications_ApplyThenHistory_MovesToAplicada()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OvertimeManagerContext(scenario));
        using var authorizer = factory.CreateClientFor(OvertimeAuthorizerContext(scenario, Guid.NewGuid()));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Ada", "Aplicada", "EMP-OTA-A", "ada.ota.a@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Rene", "Solicitante", "EMP-OTA-A2", "rene.ota.a@empresa.test");

        var (recordId, token) = await CreateAndAuthorizeOvertimeAsync(manager, authorizer, fileId, OvertimeBody(typeId, justId, requesterId));

        var applied = await ApplyOvertimeRecordAsync(manager, fileId, recordId, token);
        Assert.Equal("APLICADA", applied.ApplicationStatus);
        Assert.Equal("APLICADA", applied.RecordStatus);

        // The record now reflects APLICADA.
        var detail = await manager.GetAsync($"/api/v1/personnel-files/{fileId}/overtime-records/{recordId}");
        detail.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await detail.Content.ReadAsStringAsync()))
        {
            Assert.Equal("APLICADA", doc.RootElement.GetProperty("statusCode").GetString());
        }

        using var history = await GetOvertimeApplicationsAsync(manager, fileId, recordId);
        var items = history.RootElement.EnumerateArray().ToArray();
        Assert.Single(items);
        Assert.Equal("APLICADA", items[0].GetProperty("statusCode").GetString());
        Assert.Equal("MANUAL", items[0].GetProperty("originCode").GetString());
        // The application defaults to the record's declared destination label.
        Assert.Equal("Quincena 13/2026", items[0].GetProperty("payrollPeriodLabel").GetString());
    }

    [Fact]
    public async Task OvertimeApplications_ApplyWithPayrollPeriod_SnapshotsFkAndLabel()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OvertimeManagerContext(scenario));
        using var authorizer = factory.CreateClientFor(OvertimeAuthorizerContext(scenario, Guid.NewGuid()));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Petra", "Periodo", "EMP-OTA-B", "petra.ota.b@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Raúl", "Solicitante", "EMP-OTA-B2", "raul.ota.b@empresa.test");

        var (recordId, token) = await CreateAndAuthorizeOvertimeAsync(manager, authorizer, fileId, OvertimeBody(typeId, justId, requesterId));

        var periodId = await SeedPayrollPeriodAsync(
            scenario.TenantId, "QUINCENAL", 2026, 14, new DateOnly(2026, 7, 16), new DateOnly(2026, 7, 31));

        var applied = await ApplyOvertimeRecordAsync(manager, fileId, recordId, token, new { payrollPeriodPublicId = periodId });
        Assert.Equal("APLICADA", applied.RecordStatus);

        using var history = await GetOvertimeApplicationsAsync(manager, fileId, recordId);
        var item = history.RootElement.EnumerateArray().Single();
        Assert.Equal(periodId, item.GetProperty("payrollPeriodPublicId").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("payrollPeriodLabel").GetString()));
    }

    [Fact]
    public async Task OvertimeApplications_ApplyFutureWorkDate_Returns422_WorkDateNotElapsed()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OvertimeManagerContext(scenario));
        using var authorizer = factory.CreateClientFor(OvertimeAuthorizerContext(scenario, Guid.NewGuid()));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Futura", "Jornada", "EMP-OTA-C", "futura.ota.c@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Regina", "Solicitante", "EMP-OTA-C2", "regina.ota.c@empresa.test");

        // Organized shift 10 days ahead → create + authorize OK, but its work date has NOT elapsed → not applicable.
        var (recordId, token) = await CreateAndAuthorizeOvertimeAsync(
            manager, authorizer, fileId, OvertimeBody(typeId, justId, requesterId, workDateOffsetDays: 10));

        var response = await PostOvertimeApplicationAsync(manager, fileId, recordId, token);
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "OVERTIME_WORK_DATE_NOT_ELAPSED");

        // It also stays out of the apply-period batch (future shifts are excluded from the candidates).
        var batch = await ApplyOvertimePeriodAsync(manager, scenario.TenantId, new
        {
            payrollTypeCode = "QUINCENAL",
            payrollPeriodPublicId = (Guid?)null,
            payrollPeriodLabel = "Quincena 13/2026",
            excludedRecordPublicIds = Array.Empty<Guid>()
        });
        batch.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await batch.Content.ReadAsStringAsync()))
        {
            Assert.Equal(0, doc.RootElement.GetProperty("aplicados").GetInt32());
        }
    }

    [Fact]
    public async Task OvertimeApplications_AnnulApplication_ReopensThenReapply()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OvertimeManagerContext(scenario));
        using var authorizer = factory.CreateClientFor(OvertimeAuthorizerContext(scenario, Guid.NewGuid()));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Rita", "Reapertura", "EMP-OTA-D", "rita.ota.d@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Rodi", "Solicitante", "EMP-OTA-D2", "rodi.ota.d@empresa.test");

        var (recordId, token) = await CreateAndAuthorizeOvertimeAsync(manager, authorizer, fileId, OvertimeBody(typeId, justId, requesterId));

        var applied = await ApplyOvertimeRecordAsync(manager, fileId, recordId, token);

        // Annul (revert) the active application → the record reopens to AUTORIZADA.
        using var annulRequest = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/personnel-files/{fileId}/overtime-records/{recordId}/applications/{applied.ApplicationId}/annulment")
        {
            Content = JsonContent.Create(new { reason = "Aplicación equivocada" })
        };
        annulRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{applied.RecordToken}\"");
        var annulResponse = await manager.SendAsync(annulRequest);
        var annulPayload = await annulResponse.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == annulResponse.StatusCode, annulPayload);

        Guid reopenedToken;
        using (var doc = JsonDocument.Parse(annulPayload))
        {
            Assert.Equal("ANULADA", doc.RootElement.GetProperty("application").GetProperty("statusCode").GetString());
            Assert.Equal("AUTORIZADA", doc.RootElement.GetProperty("overtimeRecordStatusCode").GetString());
            reopenedToken = doc.RootElement.GetProperty("overtimeRecordConcurrencyToken").GetGuid();
        }

        // Re-apply → APLICADA again (the partial unique index allows a new active application).
        var reapplied = await ApplyOvertimeRecordAsync(manager, fileId, recordId, reopenedToken);
        Assert.Equal("APLICADA", reapplied.RecordStatus);

        // History carries both the ANULADA and the new APLICADA.
        using var history = await GetOvertimeApplicationsAsync(manager, fileId, recordId);
        var items = history.RootElement.EnumerateArray().ToArray();
        Assert.Equal(2, items.Length);
        Assert.Single(items, item => item.GetProperty("statusCode").GetString() == "APLICADA");
        Assert.Single(items, item => item.GetProperty("statusCode").GetString() == "ANULADA");
    }

    [Fact]
    public async Task OvertimeApplications_ApplyOnNonAutorizada_Returns422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OvertimeManagerContext(scenario));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Nora", "NoAutorizada", "EMP-OTA-E", "nora.ota.e@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Rux", "Solicitante", "EMP-OTA-E2", "rux.ota.e@empresa.test");

        // EN_REVISION (never authorized) → apply → 422 NOT_APPLICABLE.
        var (recordId, token) = await CreateOvertimeAsync(manager, fileId, OvertimeBody(typeId, justId, requesterId));

        var response = await PostOvertimeApplicationAsync(manager, fileId, recordId, token);
        await AssertProblemDetailsAsync(response, HttpStatusCode.UnprocessableEntity, "OVERTIME_NOT_APPLICABLE");
    }

    [Fact]
    public async Task OvertimeApplications_ApplyPeriodBatch_AppliesAndPostpones()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OvertimeManagerContext(scenario));
        using var authorizer = factory.CreateClientFor(OvertimeAuthorizerContext(scenario, Guid.NewGuid()));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var file1 = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Uno", "Puntual", "EMP-OTA-F1", "uno.ota.f@empresa.test");
        var file2 = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Dos", "Pospuesto", "EMP-OTA-F2", "dos.ota.f@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Rea", "Solicitante", "EMP-OTA-F3", "rea.ota.f@empresa.test");

        var (record1, _) = await CreateAndAuthorizeOvertimeAsync(manager, authorizer, file1, OvertimeBody(typeId, justId, requesterId));
        var (record2, _) = await CreateAndAuthorizeOvertimeAsync(manager, authorizer, file2, OvertimeBody(typeId, justId, requesterId));

        // Batch 1: exclude record2 → only record1 applies; record2 is postponed.
        var batch1 = await ApplyOvertimePeriodAsync(manager, scenario.TenantId, new
        {
            payrollTypeCode = "QUINCENAL",
            payrollPeriodPublicId = (Guid?)null,
            payrollPeriodLabel = "Quincena 13/2026",
            excludedRecordPublicIds = new[] { record2 }
        });
        var batch1Payload = await batch1.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == batch1.StatusCode, batch1Payload);
        using (var doc = JsonDocument.Parse(batch1Payload))
        {
            Assert.Equal(1, doc.RootElement.GetProperty("aplicados").GetInt32());
            Assert.Equal(1, doc.RootElement.GetProperty("pospuestos").GetInt32());
        }

        using (var history2 = await GetOvertimeApplicationsAsync(manager, file2, record2))
        {
            Assert.Empty(history2.RootElement.EnumerateArray());
        }

        // Batch 2: no exclusions → record2 (still AUTORIZADA) catches up; record1 is already APLICADA (skipped).
        var batch2 = await ApplyOvertimePeriodAsync(manager, scenario.TenantId, new
        {
            payrollTypeCode = "QUINCENAL",
            payrollPeriodPublicId = (Guid?)null,
            payrollPeriodLabel = "Quincena 13/2026",
            excludedRecordPublicIds = Array.Empty<Guid>()
        });
        var batch2Payload = await batch2.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == batch2.StatusCode, batch2Payload);
        using (var doc = JsonDocument.Parse(batch2Payload))
        {
            Assert.Equal(1, doc.RootElement.GetProperty("aplicados").GetInt32());
            Assert.Equal(0, doc.RootElement.GetProperty("pospuestos").GetInt32());
        }

        using var history2b = await GetOvertimeApplicationsAsync(manager, file2, record2);
        Assert.Single(history2b.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task OvertimeApplications_ConcurrentApplyPeriod_NoDuplicateApplications()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OvertimeManagerContext(scenario));
        using var authorizer = factory.CreateClientFor(OvertimeAuthorizerContext(scenario, Guid.NewGuid()));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Kara", "Carrera", "EMP-OTA-G", "kara.ota.g@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Rob", "Solicitante", "EMP-OTA-G2", "rob.ota.g@empresa.test");

        var (recordId, _) = await CreateAndAuthorizeOvertimeAsync(manager, authorizer, fileId, OvertimeBody(typeId, justId, requesterId));

        object BatchBody() => new
        {
            payrollTypeCode = "QUINCENAL",
            payrollPeriodPublicId = (Guid?)null,
            payrollPeriodLabel = "Quincena 13/2026",
            excludedRecordPublicIds = Array.Empty<Guid>()
        };

        // Two concurrent apply-period runs of the SAME filter: the advisory lock serializes them; the partial
        // unique index is the final net → the record is applied exactly once (no 500).
        var runA = ApplyOvertimePeriodAsync(manager, scenario.TenantId, BatchBody());
        var runB = ApplyOvertimePeriodAsync(manager, scenario.TenantId, BatchBody());
        var responses = await Task.WhenAll(runA, runB);

        foreach (var response in responses)
        {
            Assert.True(
                response.StatusCode is HttpStatusCode.OK or HttpStatusCode.UnprocessableEntity,
                $"Unexpected status {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        // Exactly one active APLICADA application across both runs.
        using var history = await GetOvertimeApplicationsAsync(manager, fileId, recordId);
        var applied = history.RootElement.EnumerateArray()
            .Where(item => item.GetProperty("statusCode").GetString() == "APLICADA")
            .ToArray();
        Assert.Single(applied);

        var conflicts = responses.Where(response => response.StatusCode == HttpStatusCode.UnprocessableEntity).ToArray();
        if (conflicts.Length > 0)
        {
            await AssertProblemDetailsAsync(conflicts[0], HttpStatusCode.UnprocessableEntity, "OVERTIME_APPLY_PERIOD_CONFLICT");
        }
    }

    [Fact]
    public async Task OvertimeApplications_PendingTray_MarksOverdue_AndDropsAfterApply()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OvertimeManagerContext(scenario));
        using var authorizer = factory.CreateClientFor(OvertimeAuthorizerContext(scenario, Guid.NewGuid()));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Olga", "Overdue", "EMP-OTA-H", "olga.ota.h@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Rin", "Solicitante", "EMP-OTA-H2", "rin.ota.h@empresa.test");

        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var (recordId, token) = await CreateAndAuthorizeOvertimeAsync(
            manager, authorizer, fileId, OverdueOvertimeBody(typeId, justId, requesterId, yesterday));

        // The pending tray lists the AUTORIZADA record with the overdue mark.
        var pending = await QueryOvertimePendingAsync(manager, scenario.TenantId, new { payrollTypeCode = "QUINCENAL", onlyOverdue = (bool?)null });
        pending.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await pending.Content.ReadAsStringAsync()))
        {
            var row = doc.RootElement.GetProperty("items").EnumerateArray()
                .Single(item => item.GetProperty("overtimeRecordPublicId").GetGuid() == recordId);
            Assert.True(row.GetProperty("isOverdue").GetBoolean());
            Assert.Equal(1, doc.RootElement.GetProperty("overdueCount").GetInt32());
        }

        // Apply it → it drops out of the pending tray.
        _ = await ApplyOvertimeRecordAsync(manager, fileId, recordId, token);

        var afterApply = await QueryOvertimePendingAsync(manager, scenario.TenantId, new { payrollTypeCode = "QUINCENAL", onlyOverdue = (bool?)null });
        afterApply.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await afterApply.Content.ReadAsStringAsync()))
        {
            Assert.DoesNotContain(
                doc.RootElement.GetProperty("items").EnumerateArray(),
                item => item.GetProperty("overtimeRecordPublicId").GetGuid() == recordId);
        }
    }

    [Fact]
    public async Task OvertimeApplications_ApplyWithoutIfMatch_Returns400_StaleReturns409()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OvertimeManagerContext(scenario));
        using var authorizer = factory.CreateClientFor(OvertimeAuthorizerContext(scenario, Guid.NewGuid()));

        var (typeId, justId) = await SeedOvertimeMastersAsync(scenario.TenantId);
        var fileId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Ivo", "IfMatch", "EMP-OTA-I", "ivo.ota.i@empresa.test");
        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Ras", "Solicitante", "EMP-OTA-I2", "ras.ota.i@empresa.test");

        var (recordId, _) = await CreateAndAuthorizeOvertimeAsync(manager, authorizer, fileId, OvertimeBody(typeId, justId, requesterId));

        // Missing If-Match → 400.
        var missing = await PostOvertimeApplicationAsync(manager, fileId, recordId, recordToken: null);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        // Stale If-Match → 409.
        var stale = await PostOvertimeApplicationAsync(manager, fileId, recordId, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }
}
