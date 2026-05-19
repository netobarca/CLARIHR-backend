using System.Text.Json;
using CLARIHR.Application.Abstractions.Reports.Documents;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.Reports;
using CLARIHR.Infrastructure.Configuration;
using CLARIHR.Infrastructure.Reports;
using CLARIHR.Infrastructure.Reports.Handlers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Locks in the Document Generation subdomain telemetry (technical-debt
/// doc 01 §6.1): dedicated <c>PdfRenderStarted</c> / <c>PdfRenderSucceeded</c>
/// events emitted around the inner <c>RenderAsync</c> call with the
/// <c>renderer</c>, <c>render_duration_ms</c> and <c>pdf_size_bytes</c>
/// dimensions that the generic Job* events don't carry. Without these,
/// dashboards cannot compute renderer-specific p95 latency or size
/// distributions because <c>row_count</c> is always 1 for PDF and
/// <c>artifact_size_bytes</c> is measured post-upload (not post-render).
/// </summary>
public sealed class JobProfilePdfExportHandlerTelemetryTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProfileId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task GenerateAsync_OnSuccess_ShouldEmitRenderStartedAndRenderSucceededTelemetry()
    {
        var logger = new ListLogger<JobProfilePdfExportHandler>();
        var handler = CreateHandler(renderedBytes: 2048, logger);
        var (job, destination) = CreateJobAndDestination();
        using var parameters = ParseParameters();

        var result = await handler.GenerateAsync(
            job, destination, parameters.RootElement, CancellationToken.None);

        Assert.Equal(1, result.RowCount);

        var started = Assert.Single(logger.Entries, entry => entry.EventId == ReportExportTelemetryEvents.PdfRenderStarted);
        Assert.Equal(job.PublicId, started.GetValue<Guid>("job_id"));
        Assert.Equal(TenantId, started.GetValue<Guid>("tenant_id"));
        Assert.Equal(ProfileId, started.GetValue<Guid>("job_profile_id"));
        Assert.Equal(ReportExportResources.JobProfilePdf, started.GetValue<string>("resource_key"));
        Assert.Equal("JobProfilePdfRenderer", started.GetValue<string>("renderer"));
        Assert.Equal("render_started", started.GetValue<string>("outcome"));

        var succeeded = Assert.Single(logger.Entries, entry => entry.EventId == ReportExportTelemetryEvents.PdfRenderSucceeded);
        Assert.Equal(job.PublicId, succeeded.GetValue<Guid>("job_id"));
        Assert.Equal(ProfileId, succeeded.GetValue<Guid>("job_profile_id"));
        Assert.Equal("JobProfilePdfRenderer", succeeded.GetValue<string>("renderer"));
        Assert.Equal(2048L, succeeded.GetValue<long>("pdf_size_bytes"));
        Assert.True(succeeded.GetValue<long>("render_duration_ms") >= 0);
        Assert.Equal("render_succeeded", succeeded.GetValue<string>("outcome"));
    }

    [Fact]
    public async Task GenerateAsync_WhenRenderFails_ShouldEmitStartedButNotSucceeded()
    {
        var logger = new ListLogger<JobProfilePdfExportHandler>();
        var handler = CreateHandlerWithFailingRenderer(logger);
        var (job, destination) = CreateJobAndDestination();
        using var parameters = ParseParameters();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.GenerateAsync(job, destination, parameters.RootElement, CancellationToken.None));

        Assert.Single(logger.Entries, entry => entry.EventId == ReportExportTelemetryEvents.PdfRenderStarted);
        Assert.DoesNotContain(logger.Entries, entry => entry.EventId == ReportExportTelemetryEvents.PdfRenderSucceeded);
    }

    private static JobProfilePdfExportHandler CreateHandler(int renderedBytes, ILogger<JobProfilePdfExportHandler> logger)
    {
        var repository = new TestJobProfileRepository();
        repository.PrintResponses[ProfileId] = BuildPayload(companyId: TenantId);

        var options = Options.Create(new ReportPerformanceOptions
        {
            MaxDocumentBytes = 50 * 1024 * 1024,
        });

        return new JobProfilePdfExportHandler(
            repository,
            new FixedSizePdfRenderer(renderedBytes),
            options,
            logger);
    }

    private static JobProfilePdfExportHandler CreateHandlerWithFailingRenderer(ILogger<JobProfilePdfExportHandler> logger)
    {
        var repository = new TestJobProfileRepository();
        repository.PrintResponses[ProfileId] = BuildPayload(companyId: TenantId);

        var options = Options.Create(new ReportPerformanceOptions());

        return new JobProfilePdfExportHandler(
            repository,
            new FailingPdfRenderer(),
            options,
            logger);
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

    private sealed class FailingPdfRenderer : IDocumentPdfRenderer<JobProfilePrintResponse>
    {
        public Task RenderAsync(JobProfilePrintResponse payload, Stream destination, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated render failure for §6.1 telemetry test.");
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
            BenefitsSummary: null,
            WorkingConditionSummary: null,
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

    private sealed class ListLogger<TCategoryName> : ILogger<TCategoryName>
    {
        private static readonly IDisposable Scope = new NoopDisposable();

        public List<LogEntry> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => Scope;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (state is IEnumerable<KeyValuePair<string, object?>> statePairs)
            {
                foreach (var pair in statePairs)
                {
                    properties[pair.Key] = pair.Value;
                }
            }

            Entries.Add(new LogEntry(eventId, properties));
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }

    private sealed record LogEntry(EventId EventId, IReadOnlyDictionary<string, object?> Properties)
    {
        public T GetValue<T>(string key)
        {
            Assert.True(Properties.ContainsKey(key), $"Log property '{key}' was not found.");
            var raw = Properties[key];
            Assert.NotNull(raw);

            if (raw is T value)
            {
                return value;
            }

            if (raw is IConvertible convertible)
            {
                return (T)Convert.ChangeType(convertible, typeof(T));
            }

            throw new InvalidCastException($"Unable to cast log property '{key}' from '{raw.GetType().Name}' to '{typeof(T).Name}'.");
        }
    }
}
