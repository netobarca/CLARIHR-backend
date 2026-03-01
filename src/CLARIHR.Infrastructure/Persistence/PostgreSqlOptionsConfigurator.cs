using System.Reflection;
using Microsoft.EntityFrameworkCore;

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

        if (TryUseNpgsql(optionsBuilder, connectionString))
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
}
