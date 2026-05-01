using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CLARIHR.Infrastructure.Files.Configuration;
using Microsoft.Extensions.Options;

namespace CLARIHR.Infrastructure.Files.Azure;

internal sealed class BlobServiceClientFactory : IDisposable
{
    private static readonly TimeSpan UserDelegationKeyLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan UserDelegationKeyRefreshThreshold = TimeSpan.FromMinutes(5);

    private readonly AzureBlobProviderOptions _options;
    private readonly BlobServiceClient? _client;
    private readonly SemaphoreSlim _delegationKeyLock = new(1, 1);
    private UserDelegationKey? _cachedDelegationKey;
    private DateTimeOffset _cachedDelegationKeyExpiresOn;
    private bool _disposed;

    public BlobServiceClientFactory(IOptions<FileStorageOptions> options)
    {
        _options = options.Value.AzureBlob;

        if (!_options.IsConfigured)
        {
            return;
        }

        TokenCredential credential = _options.UseManagedIdentity
            ? new ManagedIdentityCredential()
            : new DefaultAzureCredential();

        _client = new BlobServiceClient(new Uri(_options.BlobEndpoint), credential);
    }

    public bool IsConfigured => _client is not null;

    public BlobServiceClient Client =>
        _client ?? throw new InvalidOperationException(
            "Azure Blob storage is not configured. Set Storage:AzureBlob:AccountName, BlobEndpoint and DefaultContainer.");

    public string AccountName => _options.AccountName;

    public async Task<UserDelegationKey> GetUserDelegationKeyAsync(CancellationToken cancellationToken)
    {
        if (_client is null)
        {
            throw new InvalidOperationException(
                "Azure Blob storage is not configured. Set Storage:AzureBlob:AccountName, BlobEndpoint and DefaultContainer.");
        }

        var now = DateTimeOffset.UtcNow;

        if (_cachedDelegationKey is not null &&
            _cachedDelegationKeyExpiresOn - now > UserDelegationKeyRefreshThreshold)
        {
            return _cachedDelegationKey;
        }

        await _delegationKeyLock.WaitAsync(cancellationToken);

        try
        {
            now = DateTimeOffset.UtcNow;

            if (_cachedDelegationKey is not null &&
                _cachedDelegationKeyExpiresOn - now > UserDelegationKeyRefreshThreshold)
            {
                return _cachedDelegationKey;
            }

            var startsOn = now.AddMinutes(-5);
            var expiresOn = now.Add(UserDelegationKeyLifetime);

            var response = await _client.GetUserDelegationKeyAsync(startsOn, expiresOn, cancellationToken);

            _cachedDelegationKey = response.Value;
            _cachedDelegationKeyExpiresOn = expiresOn;
            return _cachedDelegationKey;
        }
        finally
        {
            _delegationKeyLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _delegationKeyLock.Dispose();
        _disposed = true;
    }
}
