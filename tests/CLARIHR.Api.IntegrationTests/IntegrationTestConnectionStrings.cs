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

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = "/tmp",
            Port = 5432,
            Database = $"clarihr_integration_tests_{Guid.NewGuid():N}",
            Username = Environment.UserName
        };

        return builder.ConnectionString;
    }
}
