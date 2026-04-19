using CLARIHR.Infrastructure.Configuration;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CLARIHR.Infrastructure.Reports;

internal sealed class ReportExportJobBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<ReportPerformanceOptions> options,
    ILogger<ReportExportJobBackgroundService> logger) : BackgroundService
{
    private readonly ReportPerformanceOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.NormalizedWorkerPollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await ExecuteCycleAsync(stoppingToken);

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }
    }

    internal async Task ExecuteCycleAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var scope = scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IReportExportJobProcessor>();
            var result = await processor.ProcessDueJobsAsync(cancellationToken);
            stopwatch.Stop();

            if (result.HadWork)
            {
                logger.LogInformation(
                    ReportExportTelemetryEvents.WorkerCycleCompleted,
                    "Report export worker cycle completed. cycle_duration_ms {cycle_duration_ms} claimed_count {claimed_count} processed_count {processed_count} succeeded_count {succeeded_count} retried_count {retried_count} failed_count {failed_count} concurrency_skipped_count {concurrency_skipped_count} expired_count {expired_count} cleanup_delete_failure_count {cleanup_delete_failure_count} had_work {had_work} worker_batch_size {worker_batch_size} poll_interval_seconds {poll_interval_seconds}",
                    stopwatch.ElapsedMilliseconds,
                    result.ClaimedCount,
                    result.ProcessedCount,
                    result.SucceededCount,
                    result.RetriedCount,
                    result.FailedCount,
                    result.ConcurrencySkippedCount,
                    result.ExpiredCount,
                    result.CleanupDeleteFailureCount,
                    result.HadWork,
                    _options.NormalizedWorkerBatchSize,
                    (int)_options.NormalizedWorkerPollInterval.TotalSeconds);
            }
            else
            {
                logger.LogDebug(
                    ReportExportTelemetryEvents.WorkerCycleEmpty,
                    "Report export worker cycle found no work. cycle_duration_ms {cycle_duration_ms} worker_batch_size {worker_batch_size} poll_interval_seconds {poll_interval_seconds} had_work {had_work}",
                    stopwatch.ElapsedMilliseconds,
                    _options.NormalizedWorkerBatchSize,
                    (int)_options.NormalizedWorkerPollInterval.TotalSeconds,
                    false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            logger.LogError(
                ReportExportTelemetryEvents.WorkerCycleFailed,
                exception,
                "Report export worker cycle failed. cycle_duration_ms {cycle_duration_ms} worker_batch_size {worker_batch_size} poll_interval_seconds {poll_interval_seconds} had_work {had_work}",
                stopwatch.ElapsedMilliseconds,
                _options.NormalizedWorkerBatchSize,
                (int)_options.NormalizedWorkerPollInterval.TotalSeconds,
                false);
        }
    }
}
