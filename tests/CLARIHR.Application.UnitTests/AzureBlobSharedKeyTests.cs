using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Infrastructure.Files.Azure;
using CLARIHR.Infrastructure.Files.Configuration;
using Microsoft.Extensions.Options;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// §3.5: the AzureBlob provider must authenticate with a Shared Key (and sign SAS
/// with it) when <c>AccountKey</c> is configured — the path that lets local dev run
/// against Azurite. Without an AccountKey it falls back to managed-identity /
/// DefaultAzureCredential + user-delegation SAS (production, unchanged).
/// </summary>
public sealed class AzureBlobSharedKeyTests
{
    // Azurite's documented well-known account key — public, not a secret.
    private const string WellKnownAzuriteKey =
        "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

    [Fact]
    public void WhenAccountKeyIsSet_UsesSharedKeyCredential()
    {
        using var factory = CreateFactory(new AzureBlobProviderOptions
        {
            AccountName = "devstoreaccount1",
            AccountKey = WellKnownAzuriteKey,
            BlobEndpoint = "http://127.0.0.1:10000/devstoreaccount1",
            DefaultContainer = "clarihr-files"
        });

        Assert.True(factory.IsConfigured);
        Assert.NotNull(factory.SharedKeyCredential);
    }

    [Fact]
    public void WhenNoAccountKey_DoesNotUseSharedKey()
    {
        using var factory = CreateFactory(new AzureBlobProviderOptions
        {
            AccountName = "prodaccount",
            BlobEndpoint = "https://prodaccount.blob.core.windows.net",
            DefaultContainer = "clarihr-files",
            UseManagedIdentity = true
        });

        Assert.True(factory.IsConfigured);
        Assert.Null(factory.SharedKeyCredential);   // production: user-delegation SAS path
    }

    [Fact]
    public void WhenNotConfigured_IsNotConfigured()
    {
        using var factory = CreateFactory(new AzureBlobProviderOptions());

        Assert.False(factory.IsConfigured);
        Assert.Null(factory.SharedKeyCredential);
    }

    [Fact]
    public async Task SharedKeySas_RoundTripsAgainstAzurite()
    {
        // e2e: runs only when Azurite is up and CLARIHR_AZURITE_E2E is set. No-op otherwise.
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CLARIHR_AZURITE_E2E")))
        {
            return;
        }

        var options = Options.Create(new FileStorageOptions
        {
            AzureBlob = new AzureBlobProviderOptions
            {
                AccountName = "devstoreaccount1",
                AccountKey = WellKnownAzuriteKey,
                BlobEndpoint = "http://127.0.0.1:10000/devstoreaccount1",
                DefaultContainer = "clarihr-e2e"
            }
        });

        using var factory = new BlobServiceClientFactory(options);
        var provider = new AzureBlobStorageProvider(factory, options);

        const string container = "clarihr-e2e";
        var key = $"test-{Guid.NewGuid():N}.txt";
        var payload = "hello-azurite"u8.ToArray();

        await using (var content = new MemoryStream(payload))
        {
            await provider.UploadStreamAsync(container, key, "text/plain", content, CancellationToken.None);
        }

        // Read back via a Shared-Key-signed read SAS URL (proves SAS signing works against Azurite).
        var readSession = await provider.CreateReadSessionAsync(
            new CreateReadSessionCommand(container, key), CancellationToken.None);

        using var http = new HttpClient();
        var downloaded = await http.GetByteArrayAsync(readSession.ReadUrl);

        Assert.Equal(payload, downloaded);

        await provider.DeleteAsync(container, key, CancellationToken.None);
    }

    private static BlobServiceClientFactory CreateFactory(AzureBlobProviderOptions azureBlob) =>
        new(Options.Create(new FileStorageOptions { AzureBlob = azureBlob }));
}
