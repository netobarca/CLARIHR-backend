using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Reports;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Reports;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.Files;
using CLARIHR.Domain.Reports;
using CLARIHR.Infrastructure.Configuration;
using CLARIHR.Infrastructure.Reports;
using CLARIHR.Infrastructure.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CLARIHR.Application.UnitTests;

public sealed class ReportExportTelemetryTests
{
    [Fact]
    public async Task ProcessDueJobsAsync_WhenJobSucceeds_ShouldEmitStructuredSuccessTelemetry()
    {
        var now = new DateTime(2026, 4, 19, 16, 0, 0, DateTimeKind.Utc);
        var job = ReportExportJob.Create(
            Guid.NewGuid(),
            resourceKey: ReportExportResources.PersonnelFiles,
            format: "csv",
            parametersJson: "{}",
            requestedByUserId: "user-1",
            queuedUtc: now.AddMinutes(-5));

        var repository = new StubReportExportJobRepository
        {
            ClaimableJobs = [job],
            ExpiredSucceededJobs = []
        };

        var storage = new StubReportExportStorage
        {
            UploadHandler = static (_, _, _, _, _, _) => Task.FromResult(
                new FileObjectInfo(4096, "text/csv", DateTime.UtcNow))
        };

        var generator = new StubReportExportJobGenerator
        {
            GenerateHandler = static (_, _, _) =>
                Task.FromResult(new ReportExportGeneratedFile(42, "personnel-files.csv", "text/csv"))
        };

        var logger = new ListLogger<ReportExportJobProcessor>();
        var processor = CreateProcessor(
            repository,
            storage,
            generator,
            now,
            logger,
            maxAttempts: 3);

        var result = await processor.ProcessDueJobsAsync(CancellationToken.None);

        Assert.Equal(1, result.ClaimedCount);
        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(1, result.SucceededCount);
        Assert.Equal(0, result.RetriedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(0, result.ConcurrencySkippedCount);

        var started = Assert.Single(logger.Entries, entry => entry.EventId == ReportExportTelemetryEvents.JobStarted);
        Assert.Equal(job.PublicId, started.GetValue<Guid>("job_id"));
        Assert.Equal(job.TenantId, started.GetValue<Guid>("tenant_id"));
        Assert.Equal("started", started.GetValue<string>("outcome"));
        Assert.True(started.GetValue<long>("queue_latency_ms") >= 0);

        var succeeded = Assert.Single(logger.Entries, entry => entry.EventId == ReportExportTelemetryEvents.JobSucceeded);
        Assert.Equal(job.PublicId, succeeded.GetValue<Guid>("job_id"));
        Assert.Equal(42L, succeeded.GetValue<long>("row_count"));
        Assert.Equal(4096L, succeeded.GetValue<long>("artifact_size_bytes"));
        Assert.Equal("succeeded", succeeded.GetValue<string>("outcome"));
        Assert.True(succeeded.GetValue<long>("processing_duration_ms") >= 0);
    }

    [Fact]
    public async Task ProcessDueJobsAsync_WhenUnexpectedErrorAndAttemptsRemain_ShouldEmitRetryTelemetry()
    {
        var now = new DateTime(2026, 4, 19, 16, 15, 0, DateTimeKind.Utc);
        var job = ReportExportJob.Create(
            Guid.NewGuid(),
            resourceKey: ReportExportResources.CostCenters,
            format: "xlsx",
            parametersJson: "{}",
            requestedByUserId: "user-2",
            queuedUtc: now.AddMinutes(-2));

        var repository = new StubReportExportJobRepository
        {
            ClaimableJobs = [job],
            ExpiredSucceededJobs = []
        };

        var generator = new StubReportExportJobGenerator
        {
            GenerateHandler = static (_, _, _) => throw new InvalidOperationException("boom")
        };

        var logger = new ListLogger<ReportExportJobProcessor>();
        var processor = CreateProcessor(
            repository,
            new StubReportExportStorage(),
            generator,
            now,
            logger,
            maxAttempts: 3);

        var result = await processor.ProcessDueJobsAsync(CancellationToken.None);

        Assert.Equal(1, result.RetriedCount);
        Assert.Equal(0, result.FailedCount);

        var retry = Assert.Single(logger.Entries, entry => entry.EventId == ReportExportTelemetryEvents.JobRetryScheduled);
        Assert.Equal(job.PublicId, retry.GetValue<Guid>("job_id"));
        Assert.Equal("REPORT_EXPORT_FAILED", retry.GetValue<string>("error_code"));
        Assert.Equal("retry_scheduled", retry.GetValue<string>("outcome"));
    }

    [Fact]
    public async Task ProcessDueJobsAsync_WhenArtifactDeleteFails_ShouldTrackCleanupFailureAndContinue()
    {
        var now = new DateTime(2026, 4, 19, 16, 30, 0, DateTimeKind.Utc);
        var expiredJob = ReportExportJob.Create(
            Guid.NewGuid(),
            resourceKey: ReportExportResources.OrgUnits,
            format: "json",
            parametersJson: "{}",
            requestedByUserId: "user-3",
            queuedUtc: now.AddHours(-2));

        expiredJob.MarkRunning("worker-initial", now.AddHours(-2), now.AddHours(-2).AddMinutes(15));
        expiredJob.MarkSucceeded(
            rowCount: 10,
            blobName: "blob-expired",
            fileName: "org-units.json",
            contentType: "application/json",
            sizeBytes: 2560,
            completedUtc: now.AddHours(-1),
            expiresUtc: now.AddMinutes(-5));

        var repository = new StubReportExportJobRepository
        {
            ClaimableJobs = [],
            ExpiredSucceededJobs = [expiredJob]
        };

        var storage = new StubReportExportStorage
        {
            DeleteHandler = static (_, _) => throw new InvalidOperationException("delete failed")
        };

        var logger = new ListLogger<ReportExportJobProcessor>();
        var processor = CreateProcessor(
            repository,
            storage,
            new StubReportExportJobGenerator(),
            now,
            logger,
            maxAttempts: 3);

        var result = await processor.ProcessDueJobsAsync(CancellationToken.None);

        Assert.Equal(1, result.ExpiredCount);
        Assert.Equal(1, result.CleanupDeleteFailureCount);

        var deleteFailed = Assert.Single(logger.Entries, entry => entry.EventId == ReportExportTelemetryEvents.ArtifactDeleteFailed);
        Assert.Equal(expiredJob.PublicId, deleteFailed.GetValue<Guid>("job_id"));
        Assert.Equal("REPORT_EXPORT_ARTIFACT_DELETE_FAILED", deleteFailed.GetValue<string>("error_code"));
        Assert.Equal("cleanup_delete_failed", deleteFailed.GetValue<string>("outcome"));

        var expired = Assert.Single(logger.Entries, entry => entry.EventId == ReportExportTelemetryEvents.ArtifactExpired);
        Assert.Equal(expiredJob.PublicId, expired.GetValue<Guid>("job_id"));
        Assert.Equal("expired", expired.GetValue<string>("outcome"));
    }

    [Fact]
    public async Task ExecuteCycleAsync_WhenProcessorReturnsWork_ShouldEmitCycleCompletedTelemetry()
    {
        var result = new ReportExportWorkerCycleResult(
            ClaimedCount: 2,
            ProcessedCount: 2,
            SucceededCount: 1,
            RetriedCount: 1,
            FailedCount: 0,
            ConcurrencySkippedCount: 0,
            ExpiredCount: 0,
            CleanupDeleteFailureCount: 0);

        var processor = new StubReportExportJobProcessor
        {
            Result = result
        };

        var logger = new ListLogger<ReportExportJobBackgroundService>();
        var service = CreateBackgroundService(processor, logger, workerBatchSize: 7, pollIntervalSeconds: 9);

        await service.ExecuteCycleAsync(CancellationToken.None);

        var cycle = Assert.Single(logger.Entries, entry => entry.EventId == ReportExportTelemetryEvents.WorkerCycleCompleted);
        Assert.Equal(2L, cycle.GetValue<long>("claimed_count"));
        Assert.Equal(2L, cycle.GetValue<long>("processed_count"));
        Assert.Equal(1L, cycle.GetValue<long>("succeeded_count"));
        Assert.Equal(1L, cycle.GetValue<long>("retried_count"));
        Assert.Equal(7L, cycle.GetValue<long>("worker_batch_size"));
        Assert.Equal(9L, cycle.GetValue<long>("poll_interval_seconds"));
    }

    [Fact]
    public async Task ExecuteCycleAsync_WhenProcessorThrows_ShouldEmitCycleFailedTelemetry()
    {
        var processor = new StubReportExportJobProcessor
        {
            ExceptionToThrow = new InvalidOperationException("cycle failed")
        };

        var logger = new ListLogger<ReportExportJobBackgroundService>();
        var service = CreateBackgroundService(processor, logger, workerBatchSize: 3, pollIntervalSeconds: 15);

        await service.ExecuteCycleAsync(CancellationToken.None);

        var failed = Assert.Single(logger.Entries, entry => entry.EventId == ReportExportTelemetryEvents.WorkerCycleFailed);
        Assert.Equal(3L, failed.GetValue<long>("worker_batch_size"));
        Assert.Equal(15L, failed.GetValue<long>("poll_interval_seconds"));
    }

    private static ReportExportJobProcessor CreateProcessor(
        IReportExportJobRepository repository,
        IFileStorageProvider storage,
        IReportExportJobGenerator generator,
        DateTime utcNow,
        ListLogger<ReportExportJobProcessor> logger,
        int maxAttempts)
    {
        var options = Options.Create(new ReportPerformanceOptions
        {
            WorkerBatchSize = 5,
            WorkerPollIntervalSeconds = 10,
            ClaimLeaseMinutes = 15,
            ArtifactRetentionHours = 24,
            MaxAttempts = maxAttempts
        });

        return new ReportExportJobProcessor(
            repository,
            new StubFilePurposeRuleProvider(storage.ProviderType),
            new StubFileStorageProviderResolver(storage),
            generator,
            new StubUnitOfWork(),
            new StubDateTimeProvider(utcNow),
            new AmbientTenantContext(),
            options,
            logger);
    }

    private static ReportExportJobBackgroundService CreateBackgroundService(
        IReportExportJobProcessor processor,
        ListLogger<ReportExportJobBackgroundService> logger,
        int workerBatchSize,
        int pollIntervalSeconds)
    {
        var serviceProvider = new SingleServiceProvider(processor);
        var scopeFactory = new SingleScopeFactory(serviceProvider);
        var options = Options.Create(new ReportPerformanceOptions
        {
            WorkerBatchSize = workerBatchSize,
            WorkerPollIntervalSeconds = pollIntervalSeconds
        });

        return new ReportExportJobBackgroundService(scopeFactory, options, logger);
    }

    private sealed class StubDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class StubReportExportJobRepository : IReportExportJobRepository
    {
        public IReadOnlyCollection<ReportExportJob> ClaimableJobs { get; init; } = Array.Empty<ReportExportJob>();

        public IReadOnlyCollection<ReportExportJob> ExpiredSucceededJobs { get; init; } = Array.Empty<ReportExportJob>();

        public void Add(ReportExportJob job) => throw new NotSupportedException();

        public Task<ReportExportJob?> GetByPublicIdAsync(Guid jobId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PagedResponse<ReportExportJobResponse>> SearchAsync(
            Guid tenantId,
            ReportExportJobStatus? status,
            IReadOnlyCollection<string> allowedResourceKeys,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<ReportExportJob>> GetClaimableAsync(
            DateTime utcNow,
            int maxCount,
            CancellationToken cancellationToken) =>
            Task.FromResult(ClaimableJobs);

        public Task<IReadOnlyCollection<ReportExportJob>> GetExpiredSucceededAsync(
            DateTime utcNow,
            int maxCount,
            CancellationToken cancellationToken) =>
            Task.FromResult(ExpiredSucceededJobs);
    }

    private sealed class StubFilePurposeRuleProvider(StorageProvider provider) : IFilePurposeRuleProvider
    {
        public FilePurposeRule? GetRule(FilePurpose purpose) =>
            purpose == FilePurpose.ReportExport
                ? new FilePurposeRule(
                    MaxSizeBytes: 100 * 1024 * 1024,
                    AllowedContentTypes: ["text/csv", "application/json", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"],
                    AllowedExtensions: [".csv", ".json", ".xlsx"],
                    DefaultProvider: provider,
                    RequiresMalwareScan: false,
                    ContainerOverride: "clarihr-files")
                : null;
    }

    private sealed class StubFileStorageProviderResolver(IFileStorageProvider storage) : IFileStorageProviderResolver
    {
        public IFileStorageProvider Resolve(StorageProvider provider) =>
            provider == storage.ProviderType
                ? storage
                : throw new InvalidOperationException($"No storage provider registered for '{provider}'.");
    }

    private sealed class StubReportExportStorage : IFileStorageProvider
    {
        public Func<Guid, Guid, string, string, Stream, CancellationToken, Task<FileObjectInfo>>? UploadHandler { get; init; }

        public Func<string, CancellationToken, Task<bool>>? DeleteHandler { get; init; }

        public StorageProvider ProviderType => StorageProvider.AzureBlob;

        public Task<CreateUploadSessionResult> CreateUploadSessionAsync(
            CreateUploadSessionProviderCommand command,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CreateReadSessionResult> CreateReadSessionAsync(
            CreateReadSessionCommand command,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> ExistsAsync(string containerName, string objectKey, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<FileObjectInfo?> GetObjectInfoAsync(string containerName, string objectKey, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<FileObjectInfo> UploadStreamAsync(
            string containerName,
            string objectKey,
            string contentType,
            Stream content,
            CancellationToken cancellationToken)
        {
            if (UploadHandler is not null)
            {
                return UploadHandler(Guid.Empty, Guid.Empty, objectKey, contentType, content, cancellationToken);
            }

            return Task.FromResult(new FileObjectInfo(1024, contentType, DateTime.UtcNow));
        }

        public Task<Stream?> OpenReadStreamAsync(string containerName, string objectKey, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(string containerName, string objectKey, CancellationToken cancellationToken)
        {
            if (DeleteHandler is null)
            {
                return Task.FromResult(true);
            }

            return DeleteHandler(objectKey, cancellationToken);
        }
    }

    private sealed class StubReportExportJobGenerator : IReportExportJobGenerator
    {
        public Func<ReportExportJob, Stream, CancellationToken, Task<ReportExportGeneratedFile>>? GenerateHandler { get; init; }

        public Task<ReportExportGeneratedFile> GenerateAsync(
            ReportExportJob job,
            Stream destination,
            CancellationToken cancellationToken)
        {
            if (GenerateHandler is not null)
            {
                return GenerateHandler(job, destination, cancellationToken);
            }

            return Task.FromResult(new ReportExportGeneratedFile(0, "empty.csv", "text/csv"));
        }
    }

    private sealed class StubReportExportJobProcessor : IReportExportJobProcessor
    {
        public ReportExportWorkerCycleResult Result { get; init; }

        public Exception? ExceptionToThrow { get; init; }

        public Task<ReportExportWorkerCycleResult> ProcessDueJobsAsync(CancellationToken cancellationToken)
        {
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(Result);
        }
    }

    private sealed class SingleScopeFactory(IServiceProvider serviceProvider) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new SingleScope(serviceProvider);
    }

    private sealed class SingleScope(IServiceProvider serviceProvider) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = serviceProvider;

        public void Dispose()
        {
        }
    }

    private sealed class SingleServiceProvider(IReportExportJobProcessor processor) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return serviceType == typeof(IReportExportJobProcessor)
                ? processor
                : null;
        }
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

            Entries.Add(new LogEntry(logLevel, eventId, properties, exception, formatter(state, exception)));
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private sealed record LogEntry(
        LogLevel Level,
        EventId EventId,
        IReadOnlyDictionary<string, object?> Properties,
        Exception? Exception,
        string Message)
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
