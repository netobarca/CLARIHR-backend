using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Features.Files.Common;
using CLARIHR.Domain.Files;
using CLARIHR.Infrastructure.Files.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CLARIHR.Infrastructure.Files.BackgroundJobs;

internal sealed class PendingFileCleanupBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<FileStorageOptions> options,
    ILogger<PendingFileCleanupBackgroundService> logger) : BackgroundService
{
    private readonly FileCleanupOptions _cleanup = options.Value.Cleanup;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_cleanup.IntervalMinutes));

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
        try
        {
            using var scope = scopeFactory.CreateScope();
            var fileRepository = scope.ServiceProvider.GetRequiredService<IFileRepository>();
            var providerResolver = scope.ServiceProvider.GetRequiredService<IFileStorageProviderResolver>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var cutoff = DateTime.UtcNow.AddHours(-_cleanup.RetentionHours);
            var expiredFiles = await fileRepository.GetExpiredPendingUploadsAsync(
                cutoff, _cleanup.BatchSize, cancellationToken);

            if (expiredFiles.Count == 0)
            {
                logger.LogDebug(
                    FileTelemetryEvents.CleanupCycleCompleted,
                    "File cleanup cycle found no expired pending uploads.");
                return;
            }

            var processedCount = 0;
            var deletedFromStorageCount = 0;

            foreach (var file in expiredFiles)
            {
                try
                {
                    var provider = providerResolver.Resolve(file.Provider);
                    var exists = await provider.ExistsAsync(file.ContainerName, file.ObjectKey, cancellationToken);

                    if (exists)
                    {
                        await provider.DeleteAsync(file.ContainerName, file.ObjectKey, cancellationToken);
                        deletedFromStorageCount++;
                    }

                    file.MarkFailed("Expired pending upload cleaned up by background job.");
                    processedCount++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Failed to clean up expired file {FilePublicId} from storage.",
                        file.PublicId);

                    file.MarkFailed($"Cleanup failed: {ex.Message}");
                    processedCount++;
                }
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                FileTelemetryEvents.CleanupCycleCompleted,
                "File cleanup cycle completed. processed_count {processed_count} deleted_from_storage_count {deleted_from_storage_count} batch_size {batch_size} retention_hours {retention_hours}",
                processedCount,
                deletedFromStorageCount,
                _cleanup.BatchSize,
                _cleanup.RetentionHours);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(
                FileTelemetryEvents.CleanupCycleFailed,
                exception,
                "File cleanup cycle failed.");
        }
    }
}
