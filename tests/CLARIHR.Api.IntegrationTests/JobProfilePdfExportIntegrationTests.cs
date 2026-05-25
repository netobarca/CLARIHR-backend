using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Application.Features.Reports;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.OrgStructureCatalogs;
using CLARIHR.Domain.OrgUnits;
using CLARIHR.Domain.Reports;
using CLARIHR.Infrastructure.Persistence;
using CLARIHR.Infrastructure.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

public sealed class JobProfilePdfExportIntegrationTests(ReportExportIntegrationTestWebApplicationFactory factory)
    : IClassFixture<ReportExportIntegrationTestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = IntegrationTestJson.CreateOptions();

    [Fact]
    public async Task PostJob_WhenJobProfilePdfWithCsvFormat_ShouldReturnBadRequest()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId,
                scenario.TenantId,
                JobProfilePermissionCodes.Read));

        var response = await client.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/report-export-jobs",
            new
            {
                resourceKey = "JOB_PROFILE_PDF",
                format = "csv",
                parameters = new { jobProfileId = Guid.NewGuid() }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostJob_WhenTabularResourceWithPdfFormat_ShouldReturnBadRequest()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId,
                scenario.TenantId,
                JobProfilePermissionCodes.Read));

        var response = await client.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/report-export-jobs",
            new
            {
                resourceKey = "PERSONNEL_FILES",
                format = "pdf",
                parameters = new { isActive = true }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostJob_WhenJobProfilePdfWithPdfFormat_ShouldQueueJob()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId,
                scenario.TenantId,
                JobProfilePermissionCodes.Read));

        var response = await client.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/report-export-jobs",
            new
            {
                resourceKey = "JOB_PROFILE_PDF",
                format = "pdf",
                parameters = new { jobProfileId = Guid.NewGuid() }
            });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var queued = await response.Content.ReadFromJsonAsync<ReportExportJobResponse>(JsonOptions);
        Assert.NotNull(queued);
        Assert.Equal(ReportExportJobStatus.Queued, queued.Status);
        Assert.Equal("JOB_PROFILE_PDF", queued.ResourceKey);
        Assert.Equal("pdf", queued.Format);
    }

    [Fact]
    public async Task PostShortcut_WhenJobProfilePdf_ShouldQueueSameJobAsGenericEndpoint()
    {
        // §7.2: the shortcut POST .../job-profiles/{id}/exports/pdf (no body) must
        // produce the same queued JOB_PROFILE_PDF/pdf job as the generic endpoint.
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId,
                scenario.TenantId,
                JobProfilePermissionCodes.Read));

        var response = await client.PostAsync(
            $"/api/v1/companies/{scenario.TenantId}/job-profiles/{Guid.NewGuid()}/exports/pdf",
            content: null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var queued = await response.Content.ReadFromJsonAsync<ReportExportJobResponse>(JsonOptions);
        Assert.NotNull(queued);
        Assert.Equal(ReportExportJobStatus.Queued, queued.Status);
        Assert.Equal("JOB_PROFILE_PDF", queued.ResourceKey);
        Assert.Equal("pdf", queued.Format);
    }

    [Fact]
    public async Task ExportJobProfilePdf_EndToEnd_SeedsProcessesAndDownloadsValidPdf()
    {
        // §8.1: the gap this closes — seed a REAL Job Profile, run the export through
        // the whole pipeline (create → process → render → upload → download) and assert
        // the downloaded artifact is a real PDF. This is the coverage that would have
        // caught §N4 (the jobProfileId/jobProfilePublicId key mismatch), invisible to
        // tests that never process a seeded profile.
        var scenario = await factory.ResetDatabaseAsync();
        var jobProfileId = await SeedJobProfileAsync(scenario.TenantId);

        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId,
                scenario.TenantId,
                JobProfilePermissionCodes.Read,
                JobProfilePermissionCodes.Admin));

        // Create via the §7.2 shortcut (also exercises the real client contract).
        var createResponse = await client.PostAsync(
            $"/api/v1/companies/{scenario.TenantId}/job-profiles/{jobProfileId}/exports/pdf",
            content: null);
        createResponse.EnsureSuccessStatusCode();
        var queued = await createResponse.Content.ReadFromJsonAsync<ReportExportJobResponse>(JsonOptions);
        Assert.NotNull(queued);

        await ProcessAllPendingJobsAsync();

        // The job must complete (not fail) for a seeded profile.
        var detailResponse = await client.GetAsync($"/api/v1/report-export-jobs/{queued.Id}");
        detailResponse.EnsureSuccessStatusCode();
        var detail = await detailResponse.Content.ReadFromJsonAsync<ReportExportJobResponse>(JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(ReportExportJobStatus.Succeeded, detail.Status);
        Assert.Equal("pdf", detail.Format);
        Assert.False(string.IsNullOrWhiteSpace(detail.FileName));
        Assert.EndsWith(".pdf", detail.FileName, StringComparison.Ordinal);

        // Download the artifact and assert it is a real PDF (header + EOF marker).
        var downloadResponse = await client.GetAsync($"/api/v1/report-export-jobs/{queued.Id}/download");
        downloadResponse.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", downloadResponse.Content.Headers.ContentType?.MediaType);

        var bytes = await downloadResponse.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0, "Downloaded PDF should not be empty.");
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes, 0, Math.Min(5, bytes.Length)), StringComparison.Ordinal);
        var tail = Encoding.ASCII.GetString(bytes, Math.Max(0, bytes.Length - 1024), Math.Min(1024, bytes.Length));
        Assert.Contains("%%EOF", tail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessJob_WhenJobProfileDoesNotExist_ShouldFailWithoutCrashingPipeline()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId,
                scenario.TenantId,
                JobProfilePermissionCodes.Read));

        var createResponse = await client.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/report-export-jobs",
            new
            {
                resourceKey = "JOB_PROFILE_PDF",
                format = "pdf",
                parameters = new { jobProfileId = Guid.NewGuid() }
            });
        createResponse.EnsureSuccessStatusCode();
        var queued = await createResponse.Content.ReadFromJsonAsync<ReportExportJobResponse>(JsonOptions);
        Assert.NotNull(queued);

        await ProcessAllPendingJobsAsync();

        var detailResponse = await client.GetAsync($"/api/v1/report-export-jobs/{queued.Id}");
        detailResponse.EnsureSuccessStatusCode();
        var detail = await detailResponse.Content.ReadFromJsonAsync<ReportExportJobResponse>(JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(ReportExportJobStatus.Failed, detail.Status);
        Assert.False(string.IsNullOrWhiteSpace(detail.LastErrorMessage));
    }

    [Fact]
    public async Task PostJob_WhenRequesterCannotManageProfiles_ForcesIncludeCompensationFalse()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId,
                scenario.TenantId,
                JobProfilePermissionCodes.Read));

        // The client tries to self-grant salary visibility.
        var response = await client.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/report-export-jobs",
            new
            {
                resourceKey = "JOB_PROFILE_PDF",
                format = "pdf",
                parameters = new { jobProfileId = Guid.NewGuid(), includeCompensation = true }
            });

        response.EnsureSuccessStatusCode();
        var queued = await response.Content.ReadFromJsonAsync<ReportExportJobResponse>(JsonOptions);
        Assert.NotNull(queued);

        Assert.False(await ReadIncludeCompensationAsync(queued.Id),
            "A Read-only requester must not be able to embed salary data, even by sending includeCompensation=true.");
    }

    [Fact]
    public async Task PostJob_WhenRequesterCanManageProfiles_AllowsIncludeCompensationTrue()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId,
                scenario.TenantId,
                JobProfilePermissionCodes.Read,
                JobProfilePermissionCodes.Admin));

        var response = await client.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/report-export-jobs",
            new
            {
                resourceKey = "JOB_PROFILE_PDF",
                format = "pdf",
                parameters = new { jobProfileId = Guid.NewGuid() }
            });

        response.EnsureSuccessStatusCode();
        var queued = await response.Content.ReadFromJsonAsync<ReportExportJobResponse>(JsonOptions);
        Assert.NotNull(queued);

        Assert.True(await ReadIncludeCompensationAsync(queued.Id),
            "A profile manager must keep salary data in the exported document.");
    }

    [Fact]
    public async Task PostJob_WhenRequesterCannotManageProfiles_ForcesIncludeCompensationFalseEvenWithCasedKey()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId,
                scenario.TenantId,
                JobProfilePermissionCodes.Read));

        // §N3: the client tries to bypass the server-controlled stamp by sending
        // a differently-cased key ("IncludeCompensation") that a case-sensitive
        // JsonObject would not overwrite.
        var response = await client.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/report-export-jobs",
            new
            {
                resourceKey = "JOB_PROFILE_PDF",
                format = "pdf",
                parameters = new { jobProfileId = Guid.NewGuid(), IncludeCompensation = true }
            });

        response.EnsureSuccessStatusCode();
        var queued = await response.Content.ReadFromJsonAsync<ReportExportJobResponse>(JsonOptions);
        Assert.NotNull(queued);

        Assert.Equal(1, await ReadIncludeCompensationKeyCountAsync(queued.Id));
        Assert.False(await ReadIncludeCompensationAsync(queued.Id),
            "§N3: a cased-key variant must not survive the server stamp nor satisfy the worker gate.");
    }

    private async Task<bool> ReadIncludeCompensationAsync(Guid jobPublicId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var job = await dbContext.ReportExportJobs
            .IgnoreQueryFilters()
            .SingleAsync(item => item.PublicId == jobPublicId);

        using var document = JsonDocument.Parse(job.ParametersJson);
        return document.RootElement.TryGetProperty("includeCompensation", out var flag)
            && flag.ValueKind == JsonValueKind.True;
    }

    // §N3: after the request-side stamp there must be exactly one property whose
    // name is "includeCompensation" ignoring case — no surviving cased variant.
    private async Task<int> ReadIncludeCompensationKeyCountAsync(Guid jobPublicId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var job = await dbContext.ReportExportJobs
            .IgnoreQueryFilters()
            .SingleAsync(item => item.PublicId == jobPublicId);

        using var document = JsonDocument.Parse(job.ParametersJson);
        var count = 0;
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (string.Equals(property.Name, "includeCompensation", StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        return count;
    }

    // §8.1: seed a real Job Profile (with its required OrgUnit chain) for the tenant
    // so the export pipeline has something to render — vs. the random GUIDs the other
    // tests use. A Job Profile requires a valid OrgUnit (FK + UpdateCore EnsurePositiveId),
    // which in turn requires an OrgUnitType, so all three are seeded.
    private async Task<Guid> SeedJobProfileAsync(Guid tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var orgUnitType = OrgUnitTypeCatalogItem.Create("E2E-GER", "Gerencia E2E", description: null, sortOrder: 10);
        orgUnitType.SetTenantId(tenantId);
        dbContext.OrgUnitTypeCatalogItems.Add(orgUnitType);
        await dbContext.SaveChangesAsync();

        var orgUnit = OrgUnit.Create(
            "E2E-OU", "Unidad E2E", orgUnitType.Id,
            functionalAreaCatalogItemId: null, parentId: null, sortOrder: 1,
            description: null, costCenterCode: null, managerEmployeeId: null);
        orgUnit.SetTenantId(tenantId);
        dbContext.OrgUnits.Add(orgUnit);
        await dbContext.SaveChangesAsync();

        var profile = JobProfile.Create("E2E-001", "Perfil E2E de Exportación");
        profile.SetTenantId(tenantId);
        profile.UpdateCore(
            "E2E-001", "Perfil E2E de Exportación",
            objective: null, orgUnitId: orgUnit.Id,
            reportsToJobProfileId: null, positionCategoryId: null,
            strategicObjectiveCatalogItemId: null, assignedWorkEquipmentCatalogItemId: null,
            responsibilityCatalogItemId: null, decisionScope: null, assignedResources: null,
            responsibilities: null, benefitsSummary: null, workingConditionSummary: null,
            marketSalaryReference: null, valuationNotes: null,
            effectiveFromUtc: null, effectiveToUtc: null, bumpVersion: false);
        dbContext.JobProfiles.Add(profile);
        await dbContext.SaveChangesAsync();

        return profile.PublicId;
    }

    private async Task ProcessAllPendingJobsAsync()
    {
        for (var iteration = 0; iteration < 10; iteration++)
        {
            using var scope = factory.Services.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IReportExportJobProcessor>();
            var result = await processor.ProcessDueJobsAsync(CancellationToken.None);
            if (result.ClaimedCount == 0)
            {
                return;
            }
        }
    }
}
