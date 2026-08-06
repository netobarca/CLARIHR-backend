using CLARIHR.Infrastructure.CatalogTypes;
using CLARIHR.Infrastructure.LegalRepresentatives;
using CLARIHR.Infrastructure.Persistence;
using CLARIHR.Application.Abstractions.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CLARIHR.Infrastructure;

public static class StartupInitializationExtensions
{
    private const int InitializationMaxAttempts = 6;
    private static readonly TimeSpan InitializationRetryDelay = TimeSpan.FromSeconds(5);

    // No environment gets demo data — Development included. Every database comes up empty and is configured
    // from scratch through the API, so what a developer sees locally is exactly what a new customer sees.
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
                var positionTitleCatalogSeedService = scope.ServiceProvider.GetRequiredService<LegalRepresentativePositionTitleCatalogSeedService>();
                var representationTypeCatalogSeedService = scope.ServiceProvider.GetRequiredService<LegalRepresentativeRepresentationTypeCatalogSeedService>();
                var catalogTypeDescriptorSeedService = scope.ServiceProvider.GetRequiredService<CatalogTypeDescriptorSeedService>();
                var planEntitlementService = scope.ServiceProvider.GetRequiredService<IPlanEntitlementService>();

                await dbContext.Database.MigrateAsync(cancellationToken);
                await positionTitleCatalogSeedService.EnsureSeededAsync(cancellationToken);
                await representationTypeCatalogSeedService.EnsureSeededAsync(cancellationToken);
                await catalogTypeDescriptorSeedService.EnsureSeededAsync(cancellationToken);
                await planEntitlementService.EnsureSystemPlanDefaultsAsync(cancellationToken);

                // Nothing tenant-scoped is backfilled here any more (same rule as CompanyProvisioningService).
                // Two backfills used to run on every startup for EVERY tenant and were removed: the
                // position-description catalogs (functions, contract types, strategic objectives, equipment,
                // responsibilities, category/classification tree) and the competency framework. Both describe how
                // a company organises and evaluates its jobs, which the system cannot guess, and neither can be
                // deleted afterwards (activate/inactivate only). The position-description seeder additionally
                // planted a DEPARTAMENTO org-unit type as the FK anchor of its seeded classification, leaking a
                // guessed org-unit type into every tenant. What stays above is platform-level: migrations, the
                // system catalog descriptors and the plan entitlement defaults.
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
                    "Infrastructure initialization failed after {MaxAttempts} attempts. The application will stop and must be restarted after PostgreSQL is available.",
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
