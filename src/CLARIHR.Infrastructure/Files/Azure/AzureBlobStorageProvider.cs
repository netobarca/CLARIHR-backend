using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Domain.Files;
using CLARIHR.Infrastructure.Files.Configuration;
using Microsoft.Extensions.Options;

namespace CLARIHR.Infrastructure.Files.Azure;

internal sealed class AzureBlobStorageProvider(
    BlobServiceClientFactory clientFactory,
    IOptions<FileStorageOptions> options) : IFileStorageProvider
{
    private readonly AzureBlobProviderOptions _options = options.Value.AzureBlob;

    public StorageProvider ProviderType => StorageProvider.AzureBlob;

    public async Task<CreateUploadSessionResult> CreateUploadSessionAsync(
        CreateUploadSessionProviderCommand command,
        CancellationToken cancellationToken)
    {
        var delegationKey = await clientFactory.GetUserDelegationKeyAsync(cancellationToken);
        var expiresOn = DateTimeOffset.UtcNow.AddMinutes(_options.UploadUrlExpirationMinutes);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = command.ContainerName,
            BlobName = command.ObjectKey,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
            ExpiresOn = expiresOn
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Create | BlobSasPermissions.Write);
        sasBuilder.ContentType = command.ContentType;

        var sasToken = sasBuilder
            .ToSasQueryParameters(delegationKey, clientFactory.AccountName)
            .ToString();

        var blobUri = new UriBuilder(_options.BlobEndpoint)
        {
            Path = $"{command.ContainerName}/{command.ObjectKey}",
            Query = sasToken
        };

        var requiredHeaders = new Dictionary<string, string>
        {
            ["x-ms-blob-type"] = "BlockBlob",
            ["Content-Type"] = command.ContentType
        };

        return new CreateUploadSessionResult(
            blobUri.Uri.ToString(),
            expiresOn.UtcDateTime,
            requiredHeaders);
    }

    public async Task<CreateReadSessionResult> CreateReadSessionAsync(
        CreateReadSessionCommand command,
        CancellationToken cancellationToken)
    {
        var delegationKey = await clientFactory.GetUserDelegationKeyAsync(cancellationToken);
        var expiresOn = DateTimeOffset.UtcNow.AddMinutes(_options.ReadUrlExpirationMinutes);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = command.ContainerName,
            BlobName = command.ObjectKey,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
            ExpiresOn = expiresOn
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sasToken = sasBuilder
            .ToSasQueryParameters(delegationKey, clientFactory.AccountName)
            .ToString();

        var blobUri = new UriBuilder(_options.BlobEndpoint)
        {
            Path = $"{command.ContainerName}/{command.ObjectKey}",
            Query = sasToken
        };

        return new CreateReadSessionResult(
            blobUri.Uri.ToString(),
            expiresOn.UtcDateTime);
    }

    public async Task<bool> ExistsAsync(string containerName, string objectKey, CancellationToken cancellationToken)
    {
        var containerClient = clientFactory.Client.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(objectKey);
        var response = await blobClient.ExistsAsync(cancellationToken);
        return response.Value;
    }

    public async Task<FileObjectInfo?> GetObjectInfoAsync(string containerName, string objectKey, CancellationToken cancellationToken)
    {
        var containerClient = clientFactory.Client.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(objectKey);

        try
        {
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            return new FileObjectInfo(
                properties.Value.ContentLength,
                properties.Value.ContentType,
                properties.Value.LastModified.UtcDateTime);
        }
        catch (global::Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string containerName, string objectKey, CancellationToken cancellationToken)
    {
        var containerClient = clientFactory.Client.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(objectKey);
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }
}
