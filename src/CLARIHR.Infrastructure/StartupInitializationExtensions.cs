using CLARIHR.Infrastructure.LegalRepresentatives;
using CLARIHR.Infrastructure.Persistence;
using CLARIHR.Application.Abstractions.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
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
        bool isDevelopment = false,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= InitializationMaxAttempts; attempt++)
        {
            try
            {
                using var scope = services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var documentTypeCatalogSeedService = scope.ServiceProvider.GetRequiredService<LegalRepresentativeDocumentTypeCatalogSeedService>();
                var positionTitleCatalogSeedService = scope.ServiceProvider.GetRequiredService<LegalRepresentativePositionTitleCatalogSeedService>();
                var representationTypeCatalogSeedService = scope.ServiceProvider.GetRequiredService<LegalRepresentativeRepresentationTypeCatalogSeedService>();
                var planEntitlementService = scope.ServiceProvider.GetRequiredService<IPlanEntitlementService>();

                if (await ShouldRebuildDevelopmentDatabaseAsync(dbContext, isDevelopment, logger, cancellationToken))
                {
                    await ResetDevelopmentSchemaAsync(dbContext, logger, cancellationToken);
                }

                await dbContext.Database.MigrateAsync(cancellationToken);
                await documentTypeCatalogSeedService.EnsureSeededAsync(cancellationToken);
                await positionTitleCatalogSeedService.EnsureSeededAsync(cancellationToken);
                await representationTypeCatalogSeedService.EnsureSeededAsync(cancellationToken);
                await planEntitlementService.EnsureSystemPlanDefaultsAsync(cancellationToken);

                if (isDevelopment)
                {
                    var devSeedService = scope.ServiceProvider.GetRequiredService<DevSeedService>();
                    await devSeedService.SeedAsync(cancellationToken);
                }

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

    private static async Task<bool> ShouldRebuildDevelopmentDatabaseAsync(
        ApplicationDbContext dbContext,
        bool isDevelopment,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!isDevelopment)
        {
            return false;
        }

        var databaseCreator = dbContext.Database.GetService<IRelationalDatabaseCreator>();
        if (!await databaseCreator.ExistsAsync(cancellationToken))
        {
            return false;
        }

        var definedMigrations = dbContext.Database.GetMigrations().ToArray();
        if (definedMigrations.Length == 0)
        {
            return false;
        }

        if (await HasDeprecatedLegacyAuthorizationTablesAsync(dbContext, cancellationToken))
        {
            logger.LogWarning(
                "Development database reset triggered because deprecated legacy authorization tables were found in database {Database}.",
                dbContext.Database.GetDbConnection().Database);
            return true;
        }

        var appliedMigrations = (await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
        if (appliedMigrations.Length == 0 && !await HasUserTablesAsync(dbContext, cancellationToken))
        {
            return false;
        }

        if (HasAlignedMigrationHistory(appliedMigrations, definedMigrations))
        {
            return false;
        }

        logger.LogWarning(
            "Development database reset triggered. Applied migrations [{Applied}] do not align with current migration history [{Defined}].",
            appliedMigrations.Length == 0 ? "none" : string.Join(", ", appliedMigrations),
            string.Join(", ", definedMigrations));

        return true;
    }

    private static bool HasAlignedMigrationHistory(
        IReadOnlyList<string> appliedMigrations,
        IReadOnlyList<string> definedMigrations)
    {
        if (appliedMigrations.Count > definedMigrations.Count)
        {
            return false;
        }

        for (var index = 0; index < appliedMigrations.Count; index++)
        {
            if (!string.Equals(appliedMigrations[index], definedMigrations[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<bool> HasUserTablesAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        try
        {
            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND table_type = 'BASE TABLE'
                      AND table_name <> '__EFMigrationsHistory')
                """;

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is true || (result is bool boolResult && boolResult);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<bool> HasDeprecatedLegacyAuthorizationTablesAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var deprecatedTables = new[]
        {
            "field_catalog",
            "field_permission_audit_logs",
            "rbac_permission_audit_logs",
            "rbac_resource_catalog"
        };

        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        try
        {
            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND table_name = ANY (@tableNames))
                """;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "tableNames";
            parameter.Value = deprecatedTables;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is true || (result is bool boolResult && boolResult);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task ResetDevelopmentSchemaAsync(
        ApplicationDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Resetting development schema in database {Database}. Existing public schema objects will be dropped and recreated.",
            dbContext.Database.GetDbConnection().Database);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            DROP SCHEMA IF EXISTS public CASCADE;
            CREATE SCHEMA public;
            GRANT ALL ON SCHEMA public TO CURRENT_USER;
            GRANT ALL ON SCHEMA public TO PUBLIC;
            """,
            cancellationToken);
    }
}
