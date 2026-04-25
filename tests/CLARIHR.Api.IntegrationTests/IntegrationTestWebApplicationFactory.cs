using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CLARIHR.Api.IntegrationTests;

public sealed class IntegrationTestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly SemaphoreSlim _resetLock = new(1, 1);
    private readonly string _connectionString;
    internal InMemoryPersonnelFileDocumentStorageService DocumentStorage { get; } = new();

    public IntegrationTestWebApplicationFactory()
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
            services.RemoveAll<IPersonnelFileDocumentStorageService>();
            services.AddSingleton(DocumentStorage);
            services.AddSingleton<IPersonnelFileDocumentStorageService>(DocumentStorage);

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
            DocumentStorage.Clear();

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
            DocumentStorage.Clear();

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

internal sealed class InMemoryPersonnelFileDocumentStorageService : IPersonnelFileDocumentStorageService
{
    private readonly Dictionary<string, StoredBlob> _blobs = new(StringComparer.Ordinal);

    public bool IsConfigured => true;

    public void Clear()
    {
        lock (_blobs)
        {
            _blobs.Clear();
        }
    }

    public Task<PersonnelFileStoredDocumentArtifact> UploadAsync(
        Guid tenantId,
        Guid personnelFileId,
        Guid documentId,
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var safeFileName = Path.GetFileName(fileName).Trim();
        var blobName = $"companies/{tenantId:D}/personnel-files/{personnelFileId:D}/documents/{documentId:D}/{safeFileName}";

        lock (_blobs)
        {
            _blobs[blobName] = new StoredBlob(content.ToArray(), contentType);
        }

        return Task.FromResult(new PersonnelFileStoredDocumentArtifact(
            blobName,
            $"https://integration.local/clarihr-personnel-documents/{blobName}",
            content.LongLength));
    }

    public Task<string?> ResolveForReadAsync(string? persistedBlobUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(persistedBlobUrl))
        {
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult<string?>($"{persistedBlobUrl.Trim()}?sig=fake");
    }

    public Task DeleteIfExistsAsync(string blobName, CancellationToken cancellationToken)
    {
        lock (_blobs)
        {
            _ = _blobs.Remove(blobName);
        }

        return Task.CompletedTask;
    }

    private sealed record StoredBlob(byte[] Content, string ContentType);
}
