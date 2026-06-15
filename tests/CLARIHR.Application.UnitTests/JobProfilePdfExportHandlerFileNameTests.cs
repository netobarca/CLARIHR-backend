using System.Text.Json;
using CLARIHR.Application.Abstractions.Reports.Documents;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.Reports;
using CLARIHR.Infrastructure.Configuration;
using CLARIHR.Infrastructure.Reports;
using CLARIHR.Infrastructure.Reports.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Locks in the user-friendly filename behavior (technical-debt doc 01 §7.1):
/// the PDF filename is derived from the profile's <c>Code</c> + <c>Title</c>
/// slug instead of the previous GUID-only name, with a deterministic fallback
/// to the job's public id when both fields slugify to empty.
/// </summary>
public sealed class JobProfilePdfExportHandlerFileNameTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProfileId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task GenerateAsync_DerivesFileNameFromCodeAndTitleSlug()
    {
        var result = await RunWith(code: "MGR-001", title: "Gerente de Desarrollo");

        Assert.Equal("job-profile-mgr-001-gerente-de-desarrollo.pdf", result.FileName);
    }

    [Fact]
    public async Task GenerateAsync_StripsDiacriticsFromTitle()
    {
        var result = await RunWith(code: "COORD-1", title: "Coordinación Técnica");

        Assert.Equal("job-profile-coord-1-coordinacion-tecnica.pdf", result.FileName);
    }

    [Fact]
    public async Task GenerateAsync_CollapsesPunctuationAndWhitespaceIntoSingleDash()
    {
        var result = await RunWith(code: "SR/JR", title: "Asistente / Contable, Senior");

        Assert.Equal("job-profile-sr-jr-asistente-contable-senior.pdf", result.FileName);
    }

    [Fact]
    public async Task GenerateAsync_TruncatesLongTitleAtSlugBoundary()
    {
        var longTitle = "Gerente General Internacional de Operaciones Estrategicas Globales para America Latina y el Caribe";
        var result = await RunWith(code: "GG-001", title: longTitle);

        // Code slug: "gg-001" (6). Title slug max 80 chars before joined.
        // After concat: "gg-001-<title-slug>.pdf"; assert no overflow + no trailing dash.
        Assert.StartsWith("job-profile-gg-001-", result.FileName);
        Assert.EndsWith(".pdf", result.FileName, StringComparison.Ordinal);
        Assert.DoesNotContain("--", result.FileName);
        Assert.DoesNotContain("-.pdf", result.FileName, StringComparison.Ordinal);
        // Bounded length: prefix(12) + code-slug(<=40) + dash(1) + title-slug(<=80) + ext(4) = <=137.
        Assert.True(result.FileName.Length <= 137, $"Filename exceeded bounded length: {result.FileName.Length}");
    }

    [Fact]
    public async Task GenerateAsync_FallsBackToJobPublicIdWhenCodeAndTitleSlugifyToEmpty()
    {
        var job = ReportExportJob.Create(
            TenantId,
            ReportExportResources.JobProfilePdf,
            "pdf",
            $"{{\"jobProfilePublicId\":\"{ProfileId}\"}}",
            "user-1",
            new DateTime(2026, 5, 16, 10, 0, 0, DateTimeKind.Utc));

        var result = await RunWithJob(code: "###", title: "...", job: job);

        Assert.Equal($"job-profile-{job.PublicId:N}.pdf", result.FileName);
    }

    [Fact]
    public async Task GenerateAsync_OmitsCodeSegmentWhenCodeIsBlank()
    {
        var result = await RunWith(code: " ", title: "Analista Junior");

        Assert.Equal("job-profile-analista-junior.pdf", result.FileName);
    }

    private static Task<ReportExportGeneratedFile> RunWith(string code, string title)
    {
        var job = ReportExportJob.Create(
            TenantId,
            ReportExportResources.JobProfilePdf,
            "pdf",
            $"{{\"jobProfilePublicId\":\"{ProfileId}\"}}",
            "user-1",
            new DateTime(2026, 5, 16, 10, 0, 0, DateTimeKind.Utc));

        return RunWithJob(code, title, job);
    }

    private static async Task<ReportExportGeneratedFile> RunWithJob(string code, string title, ReportExportJob job)
    {
        var repository = new TestJobProfileRepository();
        repository.PrintResponses[ProfileId] = BuildPayload(TenantId, code, title);

        var handler = new JobProfilePdfExportHandler(
            repository,
            new NoopPdfRenderer(),
            Options.Create(new ReportPerformanceOptions()),
            NullLogger<JobProfilePdfExportHandler>.Instance);

        await using var destination = new MemoryStream();
        using var parameters = JsonDocument.Parse($"{{\"jobProfilePublicId\":\"{ProfileId}\"}}");

        return await handler.GenerateAsync(job, destination, parameters.RootElement, CancellationToken.None);
    }

    private sealed class NoopPdfRenderer : IDocumentPdfRenderer<JobProfilePrintResponse>
    {
        public Task RenderAsync(JobProfilePrintResponse payload, Stream destination, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private static JobProfilePrintResponse BuildPayload(Guid companyId, string code, string title)
    {
        var profile = new JobProfileResponse(
            Id: ProfileId,
            CompanyId: companyId,
            Code: code,
            Title: title,
            Status: JobProfileStatus.Published,
            Version: 1,
            Objective: null,
            OrgUnitId: null,
            OrgUnitName: null,
            ReportsToJobProfileId: null,
            ReportsToJobProfileCode: null,
            ReportsToJobProfileTitle: null,
            PositionCategoryId: null,
            StrategicObjectiveCatalogItemId: null,
            AssignedWorkEquipmentCatalogItemId: null,
            ResponsibilityCatalogItemId: null,
            DecisionScope: null,
            AssignedResources: null,
            Responsibilities: null,
            MarketSalaryReference: null,
            ValuationNotes: null,
            EffectiveFromUtc: null,
            EffectiveToUtc: null,
            IsActive: true,
            Requirements: Array.Empty<JobProfileRequirementResponse>(),
            Functions: Array.Empty<JobProfileFunctionResponse>(),
            Relations: Array.Empty<JobProfileRelationResponse>(),
            Competencies: Array.Empty<JobProfileCompetencyResponse>(),
            Trainings: Array.Empty<JobProfileTrainingResponse>(),
            Compensation: null,
            Benefits: Array.Empty<JobProfileBenefitResponse>(),
            WorkingConditions: Array.Empty<JobProfileWorkingConditionResponse>(),
            DependentPositions: Array.Empty<JobProfileDependentPositionResponse>(),
            ConcurrencyToken: Guid.NewGuid(),
            CreatedAtUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ModifiedAtUtc: null);

        return new JobProfilePrintResponse(profile, new DateTime(2026, 5, 9, 12, 0, 0, DateTimeKind.Utc));
    }
}
