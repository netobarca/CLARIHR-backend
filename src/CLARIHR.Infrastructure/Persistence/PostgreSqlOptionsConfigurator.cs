using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CLARIHR.Infrastructure.Persistence;

internal static class PostgreSqlOptionsConfigurator
{
    private const string ProviderAssemblyName = "Npgsql.EntityFrameworkCore.PostgreSQL";

    public static void Configure(DbContextOptionsBuilder optionsBuilder, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var normalizedConnectionString = NormalizeConnectionString(connectionString);

        if (TryUseNpgsql(optionsBuilder, normalizedConnectionString))
        {
            return;
        }

        throw new InvalidOperationException(
            "PostgreSQL provider is not available. Add the Npgsql.EntityFrameworkCore.PostgreSQL package to enable runtime database connectivity.");
    }

    private static bool TryUseNpgsql(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        try
        {
            var providerAssembly = AppDomain.CurrentDomain.GetAssemblies()
                                       .FirstOrDefault(static assembly => assembly.GetName().Name == ProviderAssemblyName) ??
                                   Assembly.Load(ProviderAssemblyName);

            var useNpgsqlMethod = providerAssembly
                .GetTypes()
                .Where(static type => type is { IsAbstract: true, IsSealed: true })
                .SelectMany(static type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .FirstOrDefault(method =>
                {
                    if (method.Name != "UseNpgsql")
                    {
                        return false;
                    }

                    var parameters = method.GetParameters();

                    return parameters.Length >= 2 &&
                           parameters[0].ParameterType == typeof(DbContextOptionsBuilder) &&
                           parameters[1].ParameterType == typeof(string);
                });

            if (useNpgsqlMethod is null)
            {
                return false;
            }

            var parameters = useNpgsqlMethod.GetParameters();
            var arguments = new object?[parameters.Length];
            arguments[0] = optionsBuilder;
            arguments[1] = connectionString;

            for (var index = 2; index < parameters.Length; index++)
            {
                arguments[index] = parameters[index].HasDefaultValue ? parameters[index].DefaultValue : null;
            }

            _ = useNpgsqlMethod.Invoke(null, arguments);

            return true;
        }
        catch (Exception exception) when (exception is FileNotFoundException or FileLoadException or BadImageFormatException)
        {
            return false;
        }
    }

    private static string NormalizeConnectionString(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            if (!ShouldForceSsl(builder, connectionString))
            {
                return builder.ConnectionString;
            }

            builder.SslMode = SslMode.Require;

            return builder.ConnectionString;
        }
        catch (ArgumentException)
        {
            return connectionString;
        }
    }

    private static bool ShouldForceSsl(NpgsqlConnectionStringBuilder builder, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(builder.Host) || IsLocalHost(builder.Host))
        {
            return false;
        }

        return !ContainsKey(connectionString, "Ssl Mode") &&
               !ContainsKey(connectionString, "SslMode");
    }

    private static bool ContainsKey(string connectionString, string key) =>
        connectionString.Contains($"{key}=", StringComparison.OrdinalIgnoreCase);

    private static bool IsLocalHost(string host)
    {
        if (host.StartsWith('/'))
        {
            return true;
        }

        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);
    }
}
