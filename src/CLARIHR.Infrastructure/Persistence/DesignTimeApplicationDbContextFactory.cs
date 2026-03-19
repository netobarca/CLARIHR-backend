using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace CLARIHR.Infrastructure.Persistence;

public sealed class DesignTimeApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var apiProjectPath = ResolveApiProjectPath();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiProjectPath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration["Database:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database:ConnectionString is required to create ApplicationDbContext at design time.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        PostgreSqlOptionsConfigurator.Configure(optionsBuilder, connectionString);

        return new ApplicationDbContext(
            optionsBuilder.Options,
            new DesignTimeTenantContext(),
            new DesignTimeDateTimeProvider());
    }

    private static string ResolveApiProjectPath()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            Path.Combine(currentDirectory, "src", "CLARIHR.Api"),
            Path.Combine(currentDirectory, "..", "CLARIHR.Api"),
            currentDirectory
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "appsettings.json")))
            {
                return Path.GetFullPath(candidate);
            }
        }

        throw new DirectoryNotFoundException("Could not locate the CLARIHR.Api project for design-time configuration.");
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid? TenantId => null;
    }

    private sealed class DesignTimeDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => GlobalCatalogSeedData.SeededAtUtc;
    }
}
