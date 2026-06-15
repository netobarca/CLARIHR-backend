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
/// Regression coverage for the generator-level PDF size guard
/// (technical-debt doc 01 §3.3). Locks in that a pathological document is
/// rejected with the typed <see cref="ReportExportLimitExceededException"/>
/// *before* the downstream upload (so the processor maps it to the terminal
/// <c>REPORT_EXPORT_LIMIT_EXCEEDED</c> failure), and that a normally-sized
/// document passes the guard unaffected.
/// </summary>
public sealed class JobProfilePdfExportHandlerLimitTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProfileId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task GenerateAsync_RenderedDocumentExceedsMaxDocumentBytes_ThrowsTypedLimitException()
    {
        var handler = CreateHandler(renderedBytes: 4096, maxDocumentBytes: 1024);
        var (job, destination) = CreateJobAndDestination();
        using var parameters = ParseParameters();

        var exception = await Assert.ThrowsAsync<ReportExportLimitExceededException>(
            () => handler.GenerateAsync(job, destination, parameters.RootElement, CancellationToken.None));

        Assert.Contains("document size", exception.Message, StringComparison.Ordinal);
        Assert.Contains("4096", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsync_RenderedDocumentWithinMaxDocumentBytes_ReturnsGeneratedFile()
    {
        var handler = CreateHandler(renderedBytes: 512, maxDocumentBytes: 1024);
        var (job, destination) = CreateJobAndDestination();
        using var parameters = ParseParameters();

        var result = await handler.GenerateAsync(
            job, destination, parameters.RootElement, CancellationToken.None);

        Assert.Equal(1, result.RowCount);
        // §7.1: filename is derived from the profile's code + title slug,
        // not the job's GUID. Default payload uses code "MGR-001" and title
        // "Gerente de Desarrollo".
        Assert.Equal("job-profile-mgr-001-gerente-de-desarrollo.pdf", result.FileName);
    }

    private static JobProfilePdfExportHandler CreateHandler(int renderedBytes, long maxDocumentBytes)
    {
        var repository = new TestJobProfileRepository();
        repository.PrintResponses[ProfileId] = BuildPayload(companyId: TenantId);

        var options = Options.Create(new ReportPerformanceOptions
        {
            MaxDocumentBytes = maxDocumentBytes,
        });

        return new JobProfilePdfExportHandler(
            repository,
            new FixedSizePdfRenderer(renderedBytes),
            options,
            NullLogger<JobProfilePdfExportHandler>.Instance);
    }

    private static (ReportExportJob Job, MemoryStream Destination) CreateJobAndDestination()
    {
        var job = ReportExportJob.Create(
            TenantId,
            ReportExportResources.JobProfilePdf,
            "pdf",
            $"{{\"jobProfilePublicId\":\"{ProfileId}\"}}",
            "user-1",
            new DateTime(2026, 5, 16, 10, 0, 0, DateTimeKind.Utc));

        return (job, new MemoryStream());
    }

    private static JsonDocument ParseParameters() =>
        JsonDocument.Parse($"{{\"jobProfilePublicId\":\"{ProfileId}\"}}");

    private sealed class FixedSizePdfRenderer(int byteCount)
        : IDocumentPdfRenderer<JobProfilePrintResponse>
    {
        public async Task RenderAsync(
            JobProfilePrintResponse payload, Stream destination, CancellationToken cancellationToken)
        {
            await destination.WriteAsync(new byte[byteCount], cancellationToken);
        }
    }

    private static JobProfilePrintResponse BuildPayload(Guid companyId)
    {
        var profile = new JobProfileResponse(
            Id: ProfileId,
            CompanyId: companyId,
            Code: "MGR-001",
            Title: "Gerente de Desarrollo",
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
