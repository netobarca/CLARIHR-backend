using CLARIHR.Infrastructure.IdentityAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CLARIHR.Infrastructure;

public static class StartupInitializationExtensions
{
    public static async Task InitializeInfrastructureAsync(
        this IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var backfillService = scope.ServiceProvider.GetRequiredService<RbacCatalogBackfillService>();

        try
        {
            await backfillService.EnsureSeededAsync(cancellationToken);
        }
        catch (Exception exception) when (IsInitializationUnavailable(exception))
        {
            logger.LogDebug(
                exception,
                "Skipping infrastructure initialization because the database is not ready yet.");
        }
    }

    private static bool IsInitializationUnavailable(Exception exception) =>
        exception is NpgsqlException ||
        exception is DbUpdateException { InnerException: NpgsqlException };
}
