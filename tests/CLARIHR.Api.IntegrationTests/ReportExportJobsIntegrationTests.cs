using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Application.Abstractions.Reports;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.LegalRepresentatives.Common;
using CLARIHR.Application.Features.Reports;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Files;
using CLARIHR.Domain.Reports;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CLARIHR.Api.IntegrationTests;

public sealed class ReportExportJobsIntegrationTests(ReportExportIntegrationTestWebApplicationFactory factory)
    : IClassFixture<ReportExportIntegrationTestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = IntegrationTestJson.CreateOptions();

    [Fact]
    public async Task ReportExportJobs_CreateListGetAndCancel_ShouldPersistQueuedJob()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId,
                scenario.TenantId,
                LegalRepresentativePermissionCodes.Read));

        var createResponse = await client.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/report-export-jobs",
            new
            {
                resourceKey = "LEGAL_REPRESENTATIVES",
                format = "csv",
                parameters = new { isActive = true }
            });

        Assert.Equal(HttpStatusCode.Accepted, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ReportExportJobResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(ReportExportJobStatus.Queued, created.Status);
        Assert.Equal("LEGAL_REPRESENTATIVES", created.ResourceKey);
        Assert.Equal("csv", created.Format);

        var listResponse = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/report-export-jobs?status=Queued&pageNumber=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<PagedResponse<ReportExportJobResponse>>(JsonOptions);
        Assert.NotNull(list);
        Assert.Contains(list.Items, item => item.Id == created.Id && item.Status == ReportExportJobStatus.Queued);

        var detailResponse = await client.GetAsync($"/api/v1/report-export-jobs/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<ReportExportJobResponse>(JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(created.Id, detail.Id);
        Assert.Equal(ReportExportJobStatus.Queued, detail.Status);

        var cancelResponse = await client.PatchAsJsonAsync(
            $"/api/v1/report-export-jobs/{created.Id}/cancel",
            new { concurrencyToken = detail.ConcurrencyToken },
            JsonOptions);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<ReportExportJobResponse>(JsonOptions);
        Assert.NotNull(cancelled);
        Assert.Equal(ReportExportJobStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public async Task ReportExportJobs_Download_ShouldReturnConflictUntilJobSucceeds()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId,
                scenario.TenantId,
                LegalRepresentativePermissionCodes.Read));

        var createResponse = await client.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/report-export-jobs",
            new
            {
                resourceKey = "LEGAL_REPRESENTATIVES",
                format = "csv",
                parameters = new { isActive = true }
            });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<ReportExportJobResponse>(JsonOptions);
        Assert.NotNull(created);

        var notReadyResponse = await client.GetAsync($"/api/v1/report-export-jobs/{created.Id}/download");
        Assert.Equal(HttpStatusCode.Conflict, notReadyResponse.StatusCode);
        Assert.Equal("REPORT_EXPORT_JOB_NOT_READY", await ReadProblemCodeAsync(notReadyResponse));

        var fileName = "LegalRepresentatives.csv";
        var blobName = $"tests/report-export-jobs/{created.Id:N}.csv";
        var content = Encoding.UTF8.GetBytes("FullName,Email\nSecurity Representative,security.representative@acme-one.test\n");
        factory.Storage.Seed(blobName, content, "text/csv");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var job = await dbContext.ReportExportJobs
                .IgnoreQueryFilters()
                .SingleAsync(item => item.PublicId == created.Id);

            job.MarkSucceeded(
                rowCount: 1,
                blobName,
                fileName,
                contentType: "text/csv",
                sizeBytes: content.Length,
                completedUtc: DateTime.UtcNow,
                expiresUtc: DateTime.UtcNow.AddHours(1));

            await dbContext.SaveChangesAsync();
        }

        var detailResponse = await client.GetAsync($"/api/v1/report-export-jobs/{created.Id}");
        detailResponse.EnsureSuccessStatusCode();
        var detail = await detailResponse.Content.ReadFromJsonAsync<ReportExportJobResponse>(JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(ReportExportJobStatus.Succeeded, detail.Status);
        Assert.Equal(fileName, detail.FileName);
        Assert.Equal(content.Length, detail.SizeBytes);

        var downloadResponse = await client.GetAsync($"/api/v1/report-export-jobs/{created.Id}/download");
        downloadResponse.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", downloadResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(content, await downloadResponse.Content.ReadAsByteArrayAsync());
    }

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.TryGetProperty("code", out var code)
            ? code.GetString()
            : null;
    }
}

public sealed class ReportExportIntegrationTestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly SemaphoreSlim _resetLock = new(1, 1);
    private readonly string _connectionString;

    public ReportExportIntegrationTestWebApplicationFactory()
    {
        _connectionString = IntegrationTestConnectionStrings.Create();
        Storage = new InMemoryReportExportStorage();
    }

    internal InMemoryReportExportStorage Storage { get; }

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

            RemoveInfrastructureHostedServices(services);

            var storageDescriptors = services
                .Where(static descriptor =>
                    descriptor.ServiceType == typeof(IFileStorageProvider) ||
                    descriptor.ServiceType == typeof(IFileStorageProviderResolver))
                .ToArray();

            foreach (var descriptor in storageDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddSingleton(Storage);
            services.AddSingleton<IFileStorageProvider>(Storage);
            services.AddSingleton<IFileStorageProviderResolver>(
                static serviceProvider => new SingleFileStorageProviderResolver(serviceProvider.GetRequiredService<InMemoryReportExportStorage>()));
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
            Storage.Clear();
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
            Storage.Clear();

            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.MigrateAsync();

            var planEntitlementService = scope.ServiceProvider.GetRequiredService<IPlanEntitlementService>();
            await planEntitlementService.EnsureSystemPlanDefaultsAsync(CancellationToken.None);

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

    private static void RemoveInfrastructureHostedServices(IServiceCollection services)
    {
        var hostedServiceDescriptors = services
            .Where(static descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType?.Namespace?.StartsWith("CLARIHR.Infrastructure", StringComparison.Ordinal) == true)
            .ToArray();

        foreach (var descriptor in hostedServiceDescriptors)
        {
            services.Remove(descriptor);
        }
    }
}

internal sealed class InMemoryReportExportStorage : IFileStorageProvider
{
    private readonly object _gate = new();
    private readonly Dictionary<string, StoredBlob> _blobs = new(StringComparer.Ordinal);

    public StorageProvider ProviderType => StorageProvider.AzureBlob;

    public void Seed(string blobName, byte[] content, string contentType)
    {
        lock (_gate)
        {
            _blobs[BuildStorageKey("clarihr-files", blobName)] = new StoredBlob(content.ToArray(), contentType);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _blobs.Clear();
        }
    }

    public Task<CreateUploadSessionResult> CreateUploadSessionAsync(
        CreateUploadSessionProviderCommand command,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<CreateReadSessionResult> CreateReadSessionAsync(
        CreateReadSessionCommand command,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> ExistsAsync(string containerName, string objectKey, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_blobs.ContainsKey(BuildStorageKey(containerName, objectKey)));
        }
    }

    public Task<FileObjectInfo?> GetObjectInfoAsync(string containerName, string objectKey, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_blobs.TryGetValue(BuildStorageKey(containerName, objectKey), out var blob))
            {
                return Task.FromResult<FileObjectInfo?>(null);
            }

            return Task.FromResult<FileObjectInfo?>(new FileObjectInfo(blob.Content.Length, blob.ContentType, DateTime.UtcNow));
        }
    }

    public Task DeleteAsync(string containerName, string objectKey, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _blobs.Remove(BuildStorageKey(containerName, objectKey));
        }

        return Task.CompletedTask;
    }

    public async Task<FileObjectInfo> UploadStreamAsync(
        string containerName,
        string objectKey,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        var bytes = buffer.ToArray();

        lock (_gate)
        {
            _blobs[BuildStorageKey(containerName, objectKey)] = new StoredBlob(bytes, contentType);
        }

        return new FileObjectInfo(bytes.Length, contentType, DateTime.UtcNow);
    }

    public Task<Stream?> OpenReadStreamAsync(
        string containerName,
        string objectKey,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_blobs.TryGetValue(BuildStorageKey(containerName, objectKey), out var blob))
            {
                return Task.FromResult<Stream?>(null);
            }

            return Task.FromResult<Stream?>(new MemoryStream(blob.Content.ToArray(), writable: false));
        }
    }

    private static string BuildStorageKey(string containerName, string objectKey) => $"{containerName}/{objectKey}";

    private sealed record StoredBlob(byte[] Content, string ContentType);
}

internal sealed class SingleFileStorageProviderResolver(InMemoryReportExportStorage storage) : IFileStorageProviderResolver
{
    public IFileStorageProvider Resolve(StorageProvider provider) =>
        provider == storage.ProviderType
            ? storage
            : throw new InvalidOperationException($"No storage provider registered for '{provider}'.");
}
