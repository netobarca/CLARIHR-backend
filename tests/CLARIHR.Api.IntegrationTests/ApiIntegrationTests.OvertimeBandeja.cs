using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Domain.Overtime;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Integration coverage for the overtime bandeja + exports slice (REQ-007 PR-5): the advanced search with
/// per-status counts (span every status) + the totals EN HORAS (RF-011 / §0.16) — the global <c>TotalHours</c> and
/// the <c>TotalsByType[]</c> buckets CUADRAN by construction (Σ totalHours == TotalHours) — the <c>originChannel</c>
/// filter (RRHH/PORTAL), the tabular exports (xlsx 200 + json headers) and the PAYROLL INPUT (§0.16): it demands
/// the mandatory payroll type + period (400 when missing), derives the cost center from the plaza (D-12) and cuadra
/// against the pending tray of the same filter — EXCLUDING the compensated + future records. Seeds overtime records
/// directly through the domain (full control over status / channel / type / hours / compensation) pointing the
/// plaza at the candidate's real primary assignment. Reuses the PR-3/PR-4 overtime helpers.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private async Task<Guid> ResolvePrimaryAssignmentPublicIdAsync(Guid tenantId, Guid filePublicId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(assignment => assignment.TenantId == tenantId
                && assignment.PersonnelFile.PublicId == filePublicId
                && assignment.IsPrimary)
            .Select(assignment => assignment.PublicId)
            .FirstAsync();
    }

    /// <summary>Seeds an overtime record straight through the domain so the bandeja tests can pin the status,
    /// channel, type snapshot, hours, work date and compensation without the create/authorize HTTP dance.</summary>
    private async Task<Guid> SeedOvertimeRecordDirectAsync(
        Guid tenantId,
        Guid filePublicId,
        Guid requesterFilePublicId,
        string status = OvertimeRecordStatuses.Autorizada,
        string typeCode = "HED",
        decimal typeFactor = 2.00m,
        int durationHours = 2,
        int durationMinutes = 30,
        string originChannel = OvertimeRecordChannels.Rrhh,
        int workDateOffsetDays = -1,
        string payrollTypeCode = "QUINCENAL",
        string payrollPeriodLabel = "Quincena 13/2026",
        bool compensated = false)
    {
        var assignmentPublicId = await ResolvePrimaryAssignmentPublicIdAsync(tenantId, filePublicId);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var fileInternalId = await dbContext.Set<PersonnelFile>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(file => file.PublicId == filePublicId)
            .Select(file => file.Id)
            .FirstAsync();

        var workDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(workDateOffsetDays);
        var actor = Guid.NewGuid();

        var record = PersonnelFileOvertimeRecord.Create(
            workDate,
            Guid.NewGuid(),
            typeCode,
            $"Tipo {typeCode}",
            typeFactor,
            typeFactor,
            null,
            durationHours,
            durationMinutes,
            null,
            null,
            Guid.NewGuid(),
            "PROD",
            "Producción",
            null,
            originChannel,
            assignmentPublicId,
            requesterFilePublicId,
            "Solicitante",
            payrollTypeCode,
            null,
            null,
            payrollPeriodLabel,
            null,
            actor);
        record.BindToPersonnelFile(fileInternalId);
        record.SetTenantId(tenantId);

        var now = DateTime.UtcNow;
        switch (status)
        {
            case OvertimeRecordStatuses.Autorizada:
                record.Approve(actor, now);
                break;
            case OvertimeRecordStatuses.Rechazada:
                record.Reject(actor, now, "No procede");
                break;
            case OvertimeRecordStatuses.Anulada:
                record.Annul("Registro erróneo", actor, now);
                break;
            case OvertimeRecordStatuses.EnRevision:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported seed status.");
        }

        if (compensated)
        {
            record.MarkCompensated(Guid.NewGuid());
        }

        dbContext.Set<PersonnelFileOvertimeRecord>().Add(record);
        await dbContext.SaveChangesAsync();
        return record.PublicId;
    }

    private async Task<JsonDocument> QueryOvertimeBandejaAsync(HttpClient client, Guid companyId, object body)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/overtime-records/query", body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == response.StatusCode, $"Bandeja query failed: {(int)response.StatusCode} {payload}");
        return JsonDocument.Parse(payload);
    }

    private static decimal TotalHours(JsonElement root) => root.GetProperty("totalHours").GetDecimal();

    private static (int Count, decimal Hours) TypeBucket(JsonElement root, string typeCode)
    {
        foreach (var bucket in root.GetProperty("totalsByType").EnumerateArray())
        {
            if (bucket.GetProperty("overtimeTypeCode").GetString() == typeCode)
            {
                return (bucket.GetProperty("count").GetInt32(), bucket.GetProperty("totalHours").GetDecimal());
            }
        }

        return (0, 0m);
    }

    [Fact]
    public async Task OvertimeBandeja_StatusCountsAndTotalsByType_Cuadran()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OvertimeManagerContext(scenario));

        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Sol", "Solicitante", "EMP-OTB-REQ", "sol.otb.req@empresa.test");

        // 2 AUTORIZADA of type HED (2 h 30 m → 2.50 each → 5.00) + 1 AUTORIZADA of type HEN (2 h 00 m → 2.00) +
        // 1 decoy EN_REVISION of type HED (2.50). Distinct employees so the plaza resolves cleanly.
        var f1 = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Uno", "Hed", "EMP-OTB-1", "uno.otb.1@empresa.test");
        var f2 = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Dos", "Hed", "EMP-OTB-2", "dos.otb.2@empresa.test");
        var f3 = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Tri", "Hen", "EMP-OTB-3", "tri.otb.3@empresa.test");
        var f4 = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Dec", "Rev", "EMP-OTB-4", "dec.otb.4@empresa.test");

        await SeedOvertimeRecordDirectAsync(scenario.TenantId, f1, requesterId, typeCode: "HED", typeFactor: 2.00m, durationHours: 2, durationMinutes: 30);
        await SeedOvertimeRecordDirectAsync(scenario.TenantId, f2, requesterId, typeCode: "HED", typeFactor: 2.00m, durationHours: 2, durationMinutes: 30);
        await SeedOvertimeRecordDirectAsync(scenario.TenantId, f3, requesterId, typeCode: "HEN", typeFactor: 2.50m, durationHours: 2, durationMinutes: 0);
        await SeedOvertimeRecordDirectAsync(scenario.TenantId, f4, requesterId, status: OvertimeRecordStatuses.EnRevision, typeCode: "HED", typeFactor: 2.00m, durationHours: 2, durationMinutes: 30);

        // No status filter → every status listed; StatusCounts span all; totals over all 4 records.
        using (var doc = await QueryOvertimeBandejaAsync(manager, scenario.TenantId, new { }))
        {
            var root = doc.RootElement;
            Assert.Equal(4, root.GetProperty("totalCount").GetInt32());
            var counts = root.GetProperty("statusCounts");
            Assert.Equal(3, counts.GetProperty("AUTORIZADA").GetInt32());
            Assert.Equal(1, counts.GetProperty("EN_REVISION").GetInt32());

            // TotalHours over all four = 2.50 + 2.50 + 2.00 + 2.50 = 9.50; Σ TotalsByType.totalHours == TotalHours.
            Assert.Equal(9.50m, TotalHours(root));
            Assert.Equal((3, 7.50m), TypeBucket(root, "HED"));
            Assert.Equal((1, 2.00m), TypeBucket(root, "HEN"));
            var sumBuckets = root.GetProperty("totalsByType").EnumerateArray().Sum(bucket => bucket.GetProperty("totalHours").GetDecimal());
            Assert.Equal(TotalHours(root), sumBuckets);
        }

        // Status filter narrows the items + totals, but the StatusCounts still span every status.
        using (var doc = await QueryOvertimeBandejaAsync(manager, scenario.TenantId, new { statusCodes = new[] { "AUTORIZADA" } }))
        {
            var root = doc.RootElement;
            Assert.Equal(3, root.GetProperty("totalCount").GetInt32());
            Assert.All(
                root.GetProperty("items").EnumerateArray(),
                item => Assert.Equal("AUTORIZADA", item.GetProperty("statusCode").GetString()));

            // 5.00 (HED) + 2.00 (HEN) = 7.00; the decoy EN_REVISION drops out.
            Assert.Equal(7.00m, TotalHours(root));
            Assert.Equal((2, 5.00m), TypeBucket(root, "HED"));
            Assert.Equal((1, 2.00m), TypeBucket(root, "HEN"));
            var sumBuckets = root.GetProperty("totalsByType").EnumerateArray().Sum(bucket => bucket.GetProperty("totalHours").GetDecimal());
            Assert.Equal(TotalHours(root), sumBuckets);

            // StatusCounts still span every status (3 AUTORIZADA + 1 EN_REVISION = 4).
            var counts = root.GetProperty("statusCounts");
            Assert.Equal(4, counts.GetProperty("AUTORIZADA").GetInt32() + counts.GetProperty("EN_REVISION").GetInt32());
        }
    }

    [Fact]
    public async Task OvertimeBandeja_OriginChannelFilter_NarrowsToChannel()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OvertimeManagerContext(scenario));

        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Sol", "Solicitante", "EMP-OTB-CH-REQ", "sol.otbch.req@empresa.test");
        var fileRrhh = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Ren", "Rrhh", "EMP-OTB-CH-R", "ren.otbch.r@empresa.test");
        var filePortal = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Por", "Portal", "EMP-OTB-CH-P", "por.otbch.p@empresa.test");

        var rrhhId = await SeedOvertimeRecordDirectAsync(scenario.TenantId, fileRrhh, requesterId, originChannel: OvertimeRecordChannels.Rrhh);
        var portalId = await SeedOvertimeRecordDirectAsync(scenario.TenantId, filePortal, requesterId, originChannel: OvertimeRecordChannels.Portal);

        using (var doc = await QueryOvertimeBandejaAsync(manager, scenario.TenantId, new { originChannel = "PORTAL" }))
        {
            var items = doc.RootElement.GetProperty("items").EnumerateArray().ToArray();
            Assert.Single(items);
            Assert.Equal(portalId, items[0].GetProperty("overtimeRecordPublicId").GetGuid());
            Assert.Equal("PORTAL", items[0].GetProperty("originChannel").GetString());
        }

        // Combined filter (channel + type) still narrows to the RRHH record.
        using (var doc = await QueryOvertimeBandejaAsync(manager, scenario.TenantId, new { originChannel = "RRHH", statusCodes = new[] { "AUTORIZADA" } }))
        {
            var items = doc.RootElement.GetProperty("items").EnumerateArray().ToArray();
            Assert.Single(items);
            Assert.Equal(rrhhId, items[0].GetProperty("overtimeRecordPublicId").GetGuid());
        }
    }

    [Fact]
    public async Task OvertimeBandeja_Export_ReturnsSpreadsheetAndJsonHeaders()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OvertimeManagerContext(scenario));

        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Exp", "Solicitante", "EMP-OTB-EXP-REQ", "exp.otbexp.req@empresa.test");
        var fileId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Xio", "Export", "EMP-OTB-EXP", "xio.otbexp@empresa.test");
        await SeedOvertimeRecordDirectAsync(scenario.TenantId, fileId, requesterId, status: OvertimeRecordStatuses.EnRevision, typeCode: "HED", typeFactor: 2.00m);

        var xlsx = await manager.GetAsync($"/api/v1/companies/{scenario.TenantId}/overtime-records/export?format=xlsx");
        Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            xlsx.Content.Headers.ContentType?.MediaType);

        var json = await manager.GetAsync($"/api/v1/companies/{scenario.TenantId}/overtime-records/export?format=json");
        Assert.Equal(HttpStatusCode.OK, json.StatusCode);
        using var doc = JsonDocument.Parse(await json.Content.ReadAsStringAsync());
        var rows = doc.RootElement.EnumerateArray().ToArray();
        Assert.Single(rows);
        Assert.Equal("EN_REVISION", rows[0].GetProperty("Estado").GetString());
        Assert.Equal("HED", rows[0].GetProperty("TipoHoraExtra").GetString());
        Assert.Equal("RRHH", rows[0].GetProperty("Canal").GetString());
        Assert.Equal(2.50m, rows[0].GetProperty("DuracionHoras").GetDecimal());
        Assert.Equal(2.00m, rows[0].GetProperty("Factor").GetDecimal());
    }

    [Fact]
    public async Task OvertimeBandeja_PayrollInput_CuadraAgainstPending_ExcludesCompensatedAndFuture()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OvertimeManagerContext(scenario));

        var requesterId = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Ins", "Solicitante", "EMP-OTB-INS-REQ", "ins.otbins.req@empresa.test");

        var f1 = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Uno", "Insumo", "EMP-OTB-INS-1", "uno.otbins.1@empresa.test");
        var f2 = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Dos", "Insumo", "EMP-OTB-INS-2", "dos.otbins.2@empresa.test");
        var fComp = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Com", "Insumo", "EMP-OTB-INS-C", "com.otbins.c@empresa.test");
        var fFut = await SeedOneTimeIncomeCandidateAsync(scenario.TenantId, "Fut", "Insumo", "EMP-OTB-INS-F", "fut.otbins.f@empresa.test");

        // Two plain AUTORIZADA (past, not compensated) → in the insumo. Compensated + future → out of the insumo.
        var r1 = await SeedOvertimeRecordDirectAsync(scenario.TenantId, f1, requesterId, durationHours: 2, durationMinutes: 30);
        var r2 = await SeedOvertimeRecordDirectAsync(scenario.TenantId, f2, requesterId, durationHours: 2, durationMinutes: 0);
        var rComp = await SeedOvertimeRecordDirectAsync(scenario.TenantId, fComp, requesterId, durationHours: 2, durationMinutes: 30, compensated: true);
        var rFut = await SeedOvertimeRecordDirectAsync(scenario.TenantId, fFut, requesterId, durationHours: 2, durationMinutes: 30, workDateOffsetDays: 10);

        // The pending tray (RF-012) lists all four AUTORIZADA records (it does not exclude compensated / future).
        var pending = await QueryOvertimePendingAsync(manager, scenario.TenantId, new { payrollTypeCode = "QUINCENAL", onlyOverdue = (bool?)null });
        pending.EnsureSuccessStatusCode();
        Guid[] pendingIds;
        using (var doc = JsonDocument.Parse(await pending.Content.ReadAsStringAsync()))
        {
            pendingIds = doc.RootElement.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("overtimeRecordPublicId").GetGuid())
                .ToArray();
        }
        Assert.Equal(4, pendingIds.Length);
        Assert.Contains(rComp, pendingIds);
        Assert.Contains(rFut, pendingIds);

        // The insumo of the same filter cuadra against the pending MINUS the compensated + future records.
        var input = await manager.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/overtime-records/payroll-input/export?format=json&payrollTypeCode=QUINCENAL&payrollPeriod=Quincena%2013/2026");
        Assert.Equal(HttpStatusCode.OK, input.StatusCode);
        using (var doc = JsonDocument.Parse(await input.Content.ReadAsStringAsync()))
        {
            var rows = doc.RootElement.EnumerateArray().ToArray();
            Assert.Equal(2, rows.Length);
            // 2.50 + 2.00 = 4.50 factored hours worth of shifts (compensated + future excluded).
            Assert.Equal(4.50m, rows.Sum(row => row.GetProperty("DuracionHoras").GetDecimal()));
            Assert.All(rows, row => Assert.Equal("QUINCENAL", row.GetProperty("TipoPlanilla").GetString()));
            Assert.All(rows, row => Assert.Equal("Quincena 13/2026", row.GetProperty("Periodo").GetString()));
            // The cost center is derived from the plaza (D-12) — every candidate has a seeded cost center.
            Assert.All(rows, row => Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("CentroCosto").GetString())));
        }

        _ = (r1, r2);
    }

    [Fact]
    public async Task OvertimeBandeja_PayrollInput_MissingFilter_Returns400()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var manager = factory.CreateClientFor(OvertimeManagerContext(scenario));

        // Missing payroll type + period → 400 (mandatory §0.16 filter).
        var missing = await manager.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/overtime-records/payroll-input/export?format=json");
        await AssertProblemDetailsAsync(missing, HttpStatusCode.BadRequest, "OVERTIME_PAYROLL_INPUT_FILTER_REQUIRED");

        // Payroll type but no period → still 400.
        var noPeriod = await manager.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/overtime-records/payroll-input/export?format=json&payrollTypeCode=QUINCENAL");
        await AssertProblemDetailsAsync(noPeriod, HttpStatusCode.BadRequest, "OVERTIME_PAYROLL_INPUT_FILTER_REQUIRED");
    }
}
