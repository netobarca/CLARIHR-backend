using System.Net;
using System.Net.Http.Json;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Integration coverage for PR-6 (closes Ola 1): the lactation END-TO-END slice (HR-only register-with-schedules
/// → LACTANCIA journal → out-of-range/overlap schedule guards → PUT that replaces the schedule set → annulment)
/// and the company-wide incapacities bandeja + export (StatusCounts over every status, items defaulting to
/// REGISTRADA so the EN_REVISION self-registrations are excluded from the payroll input, and the tabular export).
/// Reuses the helpers of <see cref="ApiIntegrationTests"/> defined in the incapacities/settlements partials.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private async Task<Guid> GetLactationTypeIdAsync(Guid tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await dbContext.IncapacityTypes
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.Code == "LACTANCIA")
            .Select(item => item.PublicId)
            .FirstAsync();
    }

    private static object BuildLactationBody(Guid typeId, string start, string end, object[] schedules) =>
        new
        {
            incapacityTypePublicId = typeId,
            startDate = start,
            endDate = end,
            notes = (string?)null,
            schedules
        };

    [Fact]
    public async Task Lactation_HrRoundTrip_CreatesSchedulesJournalsReplacesAndAnnuls()
    {
        var scenario = await factory.ResetDatabaseAsync();
        await ApplyLeaveTemplateAsync(scenario);
        using var client = factory.CreateClientFor(CreateIncapacityManagerContext(scenario));

        var (fileId, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Lucía", "Lactancia", "EMP-LAC-A", "lucia.lac.a@empresa.test");
        var typeId = await GetLactationTypeIdAsync(scenario.TenantId);

        // [1] Register with one schedule contained in the period → REGISTRADA + LACTANCIA journal.
        var create = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/lactation-periods",
            BuildLactationBody(typeId, "2026-03-01", "2026-08-31",
                [new { startDate = "2026-03-01", endDate = "2026-05-31", dailyPermitsCount = 2, minutesPerPermit = 30 }]));
        var payload = await create.Content.ReadAsStringAsync();
        Assert.True(create.StatusCode == HttpStatusCode.Created, $"Create lactation failed: {(int)create.StatusCode} {payload}");
        var created = await create.Content.ReadFromJsonAsync<PersonnelFileLactationPeriodResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(IncapacityStatuses.Registrada, created!.StatusCode);
        Assert.Single(created.Schedules);
        Assert.Equal(1, await CountPersonnelActionsAsync(scenario.TenantId, fileId, "LACTANCIA"));

        // [2] A schedule reaching outside the period range → 422.
        var outOfRange = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/lactation-periods",
            BuildLactationBody(typeId, "2026-03-01", "2026-08-31",
                [new { startDate = "2026-03-01", endDate = "2026-09-30", dailyPermitsCount = 2, minutesPerPermit = 30 }]));
        await AssertProblemDetailsAsync(outOfRange, HttpStatusCode.UnprocessableEntity, "LACTATION_SCHEDULE_OUT_OF_RANGE");

        // [3] Overlapping schedules → 422.
        var overlap = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/lactation-periods",
            BuildLactationBody(typeId, "2026-03-01", "2026-08-31",
                [
                    new { startDate = "2026-03-01", endDate = "2026-05-31", dailyPermitsCount = 2, minutesPerPermit = 30 },
                    new { startDate = "2026-05-15", endDate = "2026-06-30", dailyPermitsCount = 1, minutesPerPermit = 45 }
                ]));
        await AssertProblemDetailsAsync(overlap, HttpStatusCode.UnprocessableEntity, "LACTATION_SCHEDULE_OVERLAP");

        // [4] PUT replaces the full schedule set (two non-overlapping schedules).
        var update = await SendSettlementAsync(
            client, HttpMethod.Put, $"/api/v1/personnel-files/{fileId}/lactation-periods/{created.Id}",
            created.ConcurrencyToken,
            BuildLactationBody(typeId, "2026-03-01", "2026-08-31",
                [
                    new { startDate = "2026-03-01", endDate = "2026-04-30", dailyPermitsCount = 2, minutesPerPermit = 30 },
                    new { startDate = "2026-05-01", endDate = "2026-06-30", dailyPermitsCount = 1, minutesPerPermit = 45 }
                ]));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<PersonnelFileLactationPeriodResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(2, updated!.Schedules.Count);

        // [5] Annulment → ANULADA (terminal).
        var annul = await SendSettlementAsync(
            client, HttpMethod.Patch, $"/api/v1/personnel-files/{fileId}/lactation-periods/{created.Id}/annulment",
            updated.ConcurrencyToken, new { reason = "Periodo anulado" });
        Assert.Equal(HttpStatusCode.OK, annul.StatusCode);
        var annulled = await annul.Content.ReadFromJsonAsync<PersonnelFileLactationPeriodResponse>(JsonOptions);
        Assert.NotNull(annulled);
        Assert.Equal(IncapacityStatuses.Anulada, annulled!.StatusCode);
    }

    [Fact]
    public async Task Incapacities_Bandeja_StatusCountsDefaultRegistradaAndExports()
    {
        var scenario = await factory.ResetDatabaseAsync();
        await ApplyLeaveTemplateAsync(scenario);

        var (fileA, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Ana", "Registrada", "EMP-BJ-A", "ana.bj.a@empresa.test");
        var (fileB, _) = await SeedSettlementCandidateAsync(
            scenario.TenantId, "Beto", "Revision", "EMP-BJ-B", "beto.bj.b@empresa.test",
            linkedUserPublicId: scenario.ActorUserId);
        var (riskId, typeId) = await GetIncapacityMasterIdsAsync(scenario.TenantId);
        var documentFileId = await SeedIncapacityDocumentFileAsync(scenario);

        using var hrClient = factory.CreateClientFor(CreateIncapacityManagerContext(scenario));
        using var selfClient = factory.CreateClientFor(TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId));

        // One REGISTRADA (HR) on fileA + one EN_REVISION (self-service) on fileB.
        _ = await CreateIncapacityAsync(hrClient, fileA, riskId, typeId, "2026-03-04", "2026-03-06", documentFileId);
        var selfCreate = await selfClient.PostJsonAsync(
            $"/api/v1/personnel-files/{fileB}/incapacities",
            BuildIncapacityBody(riskId, typeId, "2026-03-10", "2026-03-12", documentFileId));
        Assert.Equal(HttpStatusCode.Created, selfCreate.StatusCode);

        // Default query: StatusCounts cover both statuses; items default to REGISTRADA (EN_REVISION excluded).
        var bandeja = await hrClient.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/incapacities/query", new { });
        bandeja.EnsureSuccessStatusCode();
        using (var doc = await ReadJsonAsync(bandeja))
        {
            Assert.Equal(1, doc.RootElement.GetProperty("totalCount").GetInt32());
            var statusCounts = doc.RootElement.GetProperty("statusCounts");
            Assert.Equal(1, statusCounts.GetProperty("REGISTRADA").GetInt32());
            Assert.Equal(1, statusCounts.GetProperty("EN_REVISION").GetInt32());
            foreach (var item in doc.RootElement.GetProperty("items").EnumerateArray())
            {
                Assert.Equal("REGISTRADA", item.GetProperty("statusCode").GetString());
            }
        }

        // Explicit EN_REVISION filter returns the self-registration on fileB.
        var enRevision = await hrClient.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/incapacities/query", new { statusCode = "EN_REVISION" });
        enRevision.EnsureSuccessStatusCode();
        using (var doc = await ReadJsonAsync(enRevision))
        {
            Assert.Equal(1, doc.RootElement.GetProperty("totalCount").GetInt32());
            var row = doc.RootElement.GetProperty("items").EnumerateArray().Single();
            Assert.Equal(fileB, row.GetProperty("personnelFilePublicId").GetGuid());
        }

        // Export xlsx: 200 + spreadsheet content-type + non-empty body (defaults to REGISTRADA).
        var export = await hrClient.GetAsync($"/api/v1/companies/{scenario.TenantId}/incapacities/export?format=xlsx");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            export.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await export.Content.ReadAsByteArrayAsync());

        var badFormat = await hrClient.GetAsync($"/api/v1/companies/{scenario.TenantId}/incapacities/export?format=doc");
        Assert.Equal(HttpStatusCode.BadRequest, badFormat.StatusCode);
    }
}
