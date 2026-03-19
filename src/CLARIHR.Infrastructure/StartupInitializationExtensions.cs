using CLARIHR.Infrastructure.IdentityAccess;
using CLARIHR.Infrastructure.LegalRepresentatives;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CLARIHR.Infrastructure;

public static class StartupInitializationExtensions
{
    private const int InitializationMaxAttempts = 6;
    private static readonly TimeSpan InitializationRetryDelay = TimeSpan.FromSeconds(5);

    public static async Task InitializeInfrastructureAsync(
        this IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= InitializationMaxAttempts; attempt++)
        {
            try
            {
                using var scope = services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var backfillService = scope.ServiceProvider.GetRequiredService<RbacCatalogBackfillService>();
                var documentTypeCatalogSeedService = scope.ServiceProvider.GetRequiredService<LegalRepresentativeDocumentTypeCatalogSeedService>();
                var positionTitleCatalogSeedService = scope.ServiceProvider.GetRequiredService<LegalRepresentativePositionTitleCatalogSeedService>();
                var representationTypeCatalogSeedService = scope.ServiceProvider.GetRequiredService<LegalRepresentativeRepresentationTypeCatalogSeedService>();

                await dbContext.Database.MigrateAsync(cancellationToken);
                await backfillService.EnsureSeededAsync(cancellationToken);
                await documentTypeCatalogSeedService.EnsureSeededAsync(cancellationToken);
                await positionTitleCatalogSeedService.EnsureSeededAsync(cancellationToken);
                await representationTypeCatalogSeedService.EnsureSeededAsync(cancellationToken);

                return;
            }
            catch (Exception exception) when (IsInitializationUnavailable(exception) && attempt < InitializationMaxAttempts)
            {
                logger.LogWarning(
                    exception,
                    "Infrastructure initialization attempt {Attempt}/{MaxAttempts} failed because PostgreSQL is not ready yet. Retrying in {RetryDelaySeconds} seconds.",
                    attempt,
                    InitializationMaxAttempts,
                    InitializationRetryDelay.TotalSeconds);

                await Task.Delay(InitializationRetryDelay, cancellationToken);
            }
            catch (Exception exception) when (IsInitializationUnavailable(exception))
            {
                logger.LogError(
                    exception,
                    "Infrastructure initialization failed after {MaxAttempts} attempts. The application will stop so Docker can restart it cleanly.",
                    InitializationMaxAttempts);

                throw;
            }
        }

        throw new InvalidOperationException("Infrastructure initialization did not complete.");
    }

    private static bool IsInitializationUnavailable(Exception exception) =>
        exception is NpgsqlException ||
        exception is DbUpdateException { InnerException: NpgsqlException };
}
