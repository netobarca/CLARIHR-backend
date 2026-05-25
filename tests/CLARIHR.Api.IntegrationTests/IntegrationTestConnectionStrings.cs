using Npgsql;

namespace CLARIHR.Api.IntegrationTests;

internal static class IntegrationTestConnectionStrings
{
    public static string Create()
    {
        var configured = Environment.GetEnvironmentVariable("CLARIHR_INTEGRATION_TEST_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        // Unified on the docker-compose Postgres (same instance the app uses in
        // Development) so a dev only needs Docker — no native PostgreSQL on the
        // host. Each test factory gets its own ephemeral database. Override with
        // CLARIHR_INTEGRATION_TEST_CONNECTION_STRING (e.g. in CI).
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = "localhost",
            Port = 5433,
            Database = $"clarihr_integration_tests_{Guid.NewGuid():N}",
            Username = "clarihr",
            Password = "clarihr"
        };

        return builder.ConnectionString;
    }
}
