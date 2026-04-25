using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CLARIHR.Infrastructure.PersonnelFiles;

internal sealed class PersonnelFileDocumentStorageService(
    IOptions<BlobStorageOptions> options) : IPersonnelFileDocumentStorageService
{
    private readonly BlobStorageOptions _options = options.Value;
    private readonly BlobContainerClient? _containerClient =
        options.Value.IsPersonnelDocumentStorageConfigured
            ? new BlobServiceClient(options.Value.ConnectionString).GetBlobContainerClient(options.Value.PersonnelDocumentsContainer)
            : null;

    public bool IsConfigured => _options.IsPersonnelDocumentStorageConfigured && _containerClient is not null;

    public async Task<PersonnelFileStoredDocumentArtifact> UploadAsync(
        Guid tenantId,
        Guid personnelFileId,
        Guid documentId,
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured || _containerClient is null)
        {
            throw new InvalidOperationException("Personnel file document storage is not configured.");
        }

        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var safeFileName = Path.GetFileName(fileName).Trim();
        var blobName = $"companies/{tenantId:D}/personnel-files/{personnelFileId:D}/documents/{documentId:D}/{safeFileName}";
        var blobClient = _containerClient.GetBlobClient(blobName);

        await blobClient.UploadAsync(
            BinaryData.FromBytes(content),
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType,
                    CacheControl = "private, max-age=60"
                }
            },
            cancellationToken);

        return new PersonnelFileStoredDocumentArtifact(
            blobName,
            blobClient.Uri.AbsoluteUri,
            content.LongLength);
    }

    public Task<string?> ResolveForReadAsync(string? persistedBlobUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(persistedBlobUrl))
        {
            return Task.FromResult<string?>(null);
        }

        if (!TryGetManagedBlob(persistedBlobUrl, out var blobName, out var canonicalUrl))
        {
            return Task.FromResult<string?>(persistedBlobUrl.Trim());
        }

        if (!IsConfigured || _containerClient is null)
        {
            return Task.FromResult<string?>(null);
        }

        var blobClient = _containerClient.GetBlobClient(blobName);
        if (!blobClient.CanGenerateSasUri)
        {
            return Task.FromResult<string?>(canonicalUrl);
        }

        var ttlMinutes = _options.PersonnelDocumentSasTtlMinutes > 0
            ? _options.PersonnelDocumentSasTtlMinutes
            : 15;

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _options.PersonnelDocumentsContainer,
            BlobName = blobName,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-1),
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(ttlMinutes)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        _ = cancellationToken;
        return Task.FromResult<string?>(blobClient.GenerateSasUri(sasBuilder).AbsoluteUri);
    }

    public async Task DeleteIfExistsAsync(string blobName, CancellationToken cancellationToken)
    {
        if (!IsConfigured || _containerClient is null || string.IsNullOrWhiteSpace(blobName))
        {
            return;
        }

        _ = await _containerClient.GetBlobClient(blobName.Trim())
            .DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
    }

    private bool TryGetManagedBlob(string persistedBlobUrl, out string blobName, out string canonicalUrl)
    {
        blobName = string.Empty;
        canonicalUrl = string.Empty;

        if (!Uri.TryCreate(persistedBlobUrl.Trim(), UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(_options.AccountName))
        {
            return false;
        }

        if (!string.Equals(uri.Host, $"{_options.AccountName}.blob.core.windows.net", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var path = uri.AbsolutePath.Trim('/');
        if (!path.StartsWith($"{_options.PersonnelDocumentsContainer}/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        blobName = path[(_options.PersonnelDocumentsContainer.Length + 1)..];
        canonicalUrl = $"{uri.Scheme}://{uri.Host}/{_options.PersonnelDocumentsContainer}/{blobName}";
        return !string.IsNullOrWhiteSpace(blobName);
    }
}
