using CLARIHR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

public sealed class BackofficeIntegrationTestWebApplicationFactory
    : WebApplicationFactory<CLARIHR.Backoffice.Api.Program>, IAsyncLifetime
{
    private readonly SemaphoreSlim _resetLock = new(1, 1);
    private readonly string _connectionString;

    public BackofficeIntegrationTestWebApplicationFactory()
    {
        _connectionString = IntegrationTestConnectionStrings.Create();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = _connectionString,
                ["Authentication:Jwt:Issuer"] = "clarihr-integration",
                ["Authentication:Jwt:Audience"] = "clarihr-integration",
                ["Authentication:Jwt:PlatformAudience"] = "clarihr-platform-integration",
                ["Authentication:Jwt:SigningKey"] = "clarihr-integration-signing-key-2026",
                ["Authentication:Jwt:AccessTokenExpirationMinutes"] = "15",
                ["Authentication:Jwt:RefreshTokenExpirationDays"] = "14"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(static options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultScheme = TestAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    static _ => { });
        });
    }

    public async Task InitializeAsync()
    {
        await ResetDatabaseAsync();
    }

    public new async Task DisposeAsync()
    {
        await _resetLock.WaitAsync();
        try
        {
            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await dbContext.Database.EnsureDeletedAsync();
        }
        catch
        {
            // Best-effort cleanup for ephemeral integration databases.
        }
        finally
        {
            _resetLock.Release();
        }

        await base.DisposeAsync().AsTask();
    }

    internal async Task<IntegrationTestScenario> ResetDatabaseAsync(
        Func<ApplicationDbContext, Task>? customSeed = null)
    {
        await _resetLock.WaitAsync();
        try
        {
            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.MigrateAsync();

            var scenario = await IntegrationTestSeeder.SeedAsync(dbContext);
            if (customSeed is not null)
            {
                await customSeed(dbContext);
                await dbContext.SaveChangesAsync();
            }

            return scenario;
        }
        finally
        {
            _resetLock.Release();
        }
    }

    internal async Task<IntegrationTestScenario> ResetDatabaseWithServicesAsync(
        Func<IServiceProvider, ApplicationDbContext, Task> customSeed)
    {
        await _resetLock.WaitAsync();
        try
        {
            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.MigrateAsync();

            var scenario = await IntegrationTestSeeder.SeedAsync(dbContext);
            await customSeed(scope.ServiceProvider, dbContext);
            await dbContext.SaveChangesAsync();

            return scenario;
        }
        finally
        {
            _resetLock.Release();
        }
    }
}
