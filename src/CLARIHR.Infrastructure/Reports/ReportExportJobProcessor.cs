using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Domain.Files;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Reports;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.Reports;
using CLARIHR.Infrastructure.Configuration;
using CLARIHR.Infrastructure.Tenancy;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CLARIHR.Infrastructure.Reports;

internal sealed class ReportExportJobProcessor(
    IReportExportJobRepository repository,
    IFilePurposeRuleProvider ruleProvider,
    IFileStorageProviderResolver providerResolver,
    IReportExportJobGenerator generator,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    AmbientTenantContext ambientTenantContext,
    IOptions<ReportPerformanceOptions> options,
    ILogger<ReportExportJobProcessor> logger) : IReportExportJobProcessor
{
    private readonly ReportPerformanceOptions _options = options.Value;
    private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    public async Task<ReportExportWorkerCycleResult> ProcessDueJobsAsync(CancellationToken cancellationToken)
    {
        var expirationResult = await ExpireArtifactsAsync(cancellationToken);

        var utcNow = dateTimeProvider.UtcNow;
        var jobs = await repository.GetClaimableAsync(
            utcNow,
            _options.NormalizedWorkerBatchSize,
            cancellationToken);

        if (jobs.Count == 0)
        {
            return new ReportExportWorkerCycleResult(
                ClaimedCount: 0,
                ProcessedCount: 0,
                SucceededCount: 0,
                RetriedCount: 0,
                FailedCount: 0,
                ConcurrencySkippedCount: 0,
                ExpiredCount: expirationResult.ExpiredCount,
                CleanupDeleteFailureCount: expirationResult.CleanupDeleteFailureCount);
        }

        foreach (var job in jobs.Where(job => job.CanBeClaimed(utcNow)))
        {
            job.MarkRunning(_workerId, utcNow, utcNow.Add(_options.NormalizedClaimLease));
        }

        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogInformation(
                ReportExportTelemetryEvents.JobClaimConflict,
                exception,
                "Report export job claim conflict detected. claimed_count {claimed_count} worker_id {worker_id} outcome {outcome}",
                jobs.Count,
                _workerId,
                "concurrency_skipped");

            return new ReportExportWorkerCycleResult(
                ClaimedCount: jobs.Count,
                ProcessedCount: 0,
                SucceededCount: 0,
                RetriedCount: 0,
                FailedCount: 0,
                ConcurrencySkippedCount: jobs.Count,
                ExpiredCount: expirationResult.ExpiredCount,
                CleanupDeleteFailureCount: expirationResult.CleanupDeleteFailureCount);
        }

        var processedCount = 0;
        var succeededCount = 0;
        var retriedCount = 0;
        var failedCount = 0;
        var concurrencySkippedCount = 0;

        foreach (var job in jobs.Where(static job => job.Status == ReportExportJobStatus.Running))
        {
            var outcome = await ProcessJobAsync(job, cancellationToken);
            processedCount++;

            switch (outcome)
            {
                case JobProcessingOutcome.Succeeded:
                    succeededCount++;
                    break;
                case JobProcessingOutcome.RetryScheduled:
                    retriedCount++;
                    break;
                case JobProcessingOutcome.FailedTerminal:
                    failedCount++;
                    break;
                case JobProcessingOutcome.ConcurrencySkipped:
                    concurrencySkippedCount++;
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported job processing outcome '{outcome}'.");
            }
        }

        return new ReportExportWorkerCycleResult(
            ClaimedCount: jobs.Count,
            ProcessedCount: processedCount,
            SucceededCount: succeededCount,
            RetriedCount: retriedCount,
            FailedCount: failedCount,
            ConcurrencySkippedCount: concurrencySkippedCount,
            ExpiredCount: expirationResult.ExpiredCount,
            CleanupDeleteFailureCount: expirationResult.CleanupDeleteFailureCount);
    }

    private async Task<JobProcessingOutcome> ProcessJobAsync(ReportExportJob job, CancellationToken cancellationToken)
    {
        var queueLatencyMs = ComputeQueueLatencyMs(job);
        var maxAttempts = _options.NormalizedMaxAttempts;
        logger.LogInformation(
            ReportExportTelemetryEvents.JobStarted,
            "Report export job started. job_id {job_id} tenant_id {tenant_id} resource_key {resource_key} format {format} worker_id {worker_id} attempt {attempt} max_attempts {max_attempts} queue_latency_ms {queue_latency_ms} outcome {outcome}",
            job.PublicId,
            job.TenantId,
            job.ResourceKey,
            job.Format,
            _workerId,
            job.Attempts,
            maxAttempts,
            queueLatencyMs,
            "started");

        var stopwatch = Stopwatch.StartNew();
        using var tenantScope = ambientTenantContext.Push(job.TenantId);
        var tempPath = Path.Combine(Path.GetTempPath(), $"clarihr-report-export-{job.PublicId:N}.tmp");

        try
        {
            await using var tempStream = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 128 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var generated = await generator.GenerateAsync(job, tempStream, cancellationToken);
            if (tempStream.CanSeek)
            {
                tempStream.Position = 0;
            }

            var rule = ruleProvider.GetRule(FilePurpose.ReportExport);
            if (rule is null)
            {
                throw new InvalidOperationException("Report export storage purpose is not configured.");
            }

            var provider = providerResolver.Resolve(rule.DefaultProvider);
            var objectKey = $"tenants/{job.TenantId:D}/report-exports/{job.PublicId:D}/{generated.FileName}";
            var containerName = rule.ContainerOverride ?? "clarihr-files";
            var artifact = await provider.UploadStreamAsync(
                containerName,
                objectKey,
                generated.ContentType,
                tempStream,
                cancellationToken);

            var completedUtc = dateTimeProvider.UtcNow;
            job.MarkSucceeded(
                generated.RowCount,
                objectKey,
                generated.FileName,
                generated.ContentType,
                artifact.SizeBytes,
                completedUtc,
                completedUtc.Add(_options.NormalizedArtifactRetention));

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            stopwatch.Stop();
            logger.LogInformation(
                ReportExportTelemetryEvents.JobSucceeded,
                "Report export job succeeded. job_id {job_id} tenant_id {tenant_id} resource_key {resource_key} format {format} worker_id {worker_id} attempt {attempt} max_attempts {max_attempts} queue_latency_ms {queue_latency_ms} processing_duration_ms {processing_duration_ms} row_count {row_count} artifact_size_bytes {artifact_size_bytes} outcome {outcome}",
                job.PublicId,
                job.TenantId,
                job.ResourceKey,
                job.Format,
                _workerId,
                job.Attempts,
                maxAttempts,
                queueLatencyMs,
                stopwatch.ElapsedMilliseconds,
                generated.RowCount,
                artifact.SizeBytes,
                "succeeded");

            return JobProcessingOutcome.Succeeded;
        }
        catch (ReportExportLimitExceededException exception)
        {
            const string errorCode = "REPORT_EXPORT_LIMIT_EXCEEDED";
            const string errorMessage = "Report export exceeded an enforced limit (row count or document size).";

            job.MarkProcessingFailed(
                ReportPolicyErrors.ExportLimitExceeded.Code,
                ReportPolicyErrors.ExportLimitExceeded.Message,
                dateTimeProvider.UtcNow,
                job.Attempts);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            stopwatch.Stop();
            logger.LogInformation(
                ReportExportTelemetryEvents.JobFailedTerminal,
                exception,
                "Report export job failed terminally. job_id {job_id} tenant_id {tenant_id} resource_key {resource_key} format {format} worker_id {worker_id} attempt {attempt} max_attempts {max_attempts} queue_latency_ms {queue_latency_ms} processing_duration_ms {processing_duration_ms} error_code {error_code} error_message {error_message} outcome {outcome}",
                job.PublicId,
                job.TenantId,
                job.ResourceKey,
                job.Format,
                _workerId,
                job.Attempts,
                job.Attempts,
                queueLatencyMs,
                stopwatch.ElapsedMilliseconds,
                errorCode,
                errorMessage,
                "failed_terminal");

            return JobProcessingOutcome.FailedTerminal;
        }
        catch (ReportExportInvalidParametersException exception)
        {
            const string errorCode = "REPORT_EXPORT_PARAMETERS_INVALID";
            const string errorMessage = "Report export parameters are invalid.";

            job.MarkProcessingFailed(
                errorCode,
                errorMessage,
                dateTimeProvider.UtcNow,
                job.Attempts);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            stopwatch.Stop();
            logger.LogInformation(
                ReportExportTelemetryEvents.JobFailedTerminal,
                exception,
                "Report export job failed terminally. job_id {job_id} tenant_id {tenant_id} resource_key {resource_key} format {format} worker_id {worker_id} attempt {attempt} max_attempts {max_attempts} queue_latency_ms {queue_latency_ms} processing_duration_ms {processing_duration_ms} error_code {error_code} error_message {error_message} outcome {outcome}",
                job.PublicId,
                job.TenantId,
                job.ResourceKey,
                job.Format,
                _workerId,
                job.Attempts,
                job.Attempts,
                queueLatencyMs,
                stopwatch.ElapsedMilliseconds,
                errorCode,
                errorMessage,
                "failed_terminal");

            return JobProcessingOutcome.FailedTerminal;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            stopwatch.Stop();
            logger.LogInformation(
                ReportExportTelemetryEvents.JobClaimConflict,
                exception,
                "Report export job changed concurrently while processing. job_id {job_id} tenant_id {tenant_id} resource_key {resource_key} format {format} worker_id {worker_id} attempt {attempt} max_attempts {max_attempts} queue_latency_ms {queue_latency_ms} processing_duration_ms {processing_duration_ms} error_code {error_code} outcome {outcome}",
                job.PublicId,
                job.TenantId,
                job.ResourceKey,
                job.Format,
                _workerId,
                job.Attempts,
                maxAttempts,
                queueLatencyMs,
                stopwatch.ElapsedMilliseconds,
                "REPORT_EXPORT_CONCURRENCY_CONFLICT",
                "concurrency_skipped");

            return JobProcessingOutcome.ConcurrencySkipped;
        }
        catch (Exception exception)
        {
            job.MarkProcessingFailed(
                "REPORT_EXPORT_FAILED",
                "Report export processing failed.",
                dateTimeProvider.UtcNow,
                _options.NormalizedMaxAttempts);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            stopwatch.Stop();

            if (job.Status == ReportExportJobStatus.Queued)
            {
                logger.LogInformation(
                    ReportExportTelemetryEvents.JobRetryScheduled,
                    exception,
                    "Report export job scheduled for retry. job_id {job_id} tenant_id {tenant_id} resource_key {resource_key} format {format} worker_id {worker_id} attempt {attempt} max_attempts {max_attempts} queue_latency_ms {queue_latency_ms} processing_duration_ms {processing_duration_ms} error_code {error_code} error_message {error_message} outcome {outcome}",
                    job.PublicId,
                    job.TenantId,
                    job.ResourceKey,
                    job.Format,
                    _workerId,
                    job.Attempts,
                    maxAttempts,
                    queueLatencyMs,
                    stopwatch.ElapsedMilliseconds,
                    "REPORT_EXPORT_FAILED",
                    "Report export processing failed.",
                    "retry_scheduled");

                return JobProcessingOutcome.RetryScheduled;
            }

            logger.LogError(
                ReportExportTelemetryEvents.JobFailedTerminal,
                exception,
                "Report export job failed terminally. job_id {job_id} tenant_id {tenant_id} resource_key {resource_key} format {format} worker_id {worker_id} attempt {attempt} max_attempts {max_attempts} queue_latency_ms {queue_latency_ms} processing_duration_ms {processing_duration_ms} error_code {error_code} error_message {error_message} outcome {outcome}",
                job.PublicId,
                job.TenantId,
                job.ResourceKey,
                job.Format,
                _workerId,
                job.Attempts,
                maxAttempts,
                queueLatencyMs,
                stopwatch.ElapsedMilliseconds,
                "REPORT_EXPORT_FAILED",
                "Report export processing failed.",
                "failed_terminal");

            return JobProcessingOutcome.FailedTerminal;
        }
        finally
        {
            TryDeleteTempFile(tempPath);
        }
    }

    private async Task<ExpirationResult> ExpireArtifactsAsync(CancellationToken cancellationToken)
    {
        var expiredCount = 0;
        var cleanupDeleteFailureCount = 0;
        var utcNow = dateTimeProvider.UtcNow;
        var expiredJobs = await repository.GetExpiredSucceededAsync(
            utcNow,
            Math.Max(10, _options.NormalizedWorkerBatchSize * 10),
            cancellationToken);

        if (expiredJobs.Count == 0)
        {
            return new ExpirationResult(ExpiredCount: 0, CleanupDeleteFailureCount: 0);
        }

        foreach (var job in expiredJobs)
        {
            if (!string.IsNullOrWhiteSpace(job.ArtifactBlobName))
            {
                try
                {
                    var rule = ruleProvider.GetRule(FilePurpose.ReportExport);
                    if (rule is not null)
                    {
                        var provider = providerResolver.Resolve(rule.DefaultProvider);
                        var containerName = rule.ContainerOverride ?? "clarihr-files";
                        await provider.DeleteAsync(containerName, job.ArtifactBlobName, cancellationToken);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    cleanupDeleteFailureCount++;
                    logger.LogError(
                        ReportExportTelemetryEvents.ArtifactDeleteFailed,
                        exception,
                        "Report export artifact cleanup failed. job_id {job_id} tenant_id {tenant_id} resource_key {resource_key} format {format} worker_id {worker_id} row_count {row_count} artifact_size_bytes {artifact_size_bytes} error_code {error_code} outcome {outcome}",
                        job.PublicId,
                        job.TenantId,
                        job.ResourceKey,
                        job.Format,
                        _workerId,
                        job.RowCount,
                        job.ArtifactSizeBytes,
                        "REPORT_EXPORT_ARTIFACT_DELETE_FAILED",
                        "cleanup_delete_failed");
                }
            }

            job.MarkExpired(utcNow);
            expiredCount++;
            logger.LogInformation(
                ReportExportTelemetryEvents.ArtifactExpired,
                "Report export artifact expired. job_id {job_id} tenant_id {tenant_id} resource_key {resource_key} format {format} worker_id {worker_id} row_count {row_count} artifact_size_bytes {artifact_size_bytes} outcome {outcome}",
                job.PublicId,
                job.TenantId,
                job.ResourceKey,
                job.Format,
                _workerId,
                job.RowCount,
                job.ArtifactSizeBytes,
                "expired");
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ExpirationResult(expiredCount, cleanupDeleteFailureCount);
    }

    private static long ComputeQueueLatencyMs(ReportExportJob job)
    {
        var startedUtc = job.StartedUtc ?? job.QueuedUtc;
        var latency = startedUtc - job.QueuedUtc;
        return latency <= TimeSpan.Zero ? 0 : (long)latency.TotalMilliseconds;
    }

    private static void TryDeleteTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch
        {
            // Best-effort cleanup; the worker must not fail a job after upload because temp deletion failed.
        }
    }

    private readonly record struct ExpirationResult(
        int ExpiredCount,
        int CleanupDeleteFailureCount);

    private enum JobProcessingOutcome
    {
        Succeeded = 1,
        RetryScheduled = 2,
        FailedTerminal = 3,
        ConcurrencySkipped = 4
    }
}
