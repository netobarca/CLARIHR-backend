using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CLARIHR.Application.Abstractions.Reports;
using CLARIHR.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace CLARIHR.Infrastructure.Reports;

internal sealed class ReportExportBlobStorage : IReportExportStorage
{
    private readonly BlobStorageOptions _options;
    private readonly BlobContainerClient? _containerClient;

    public ReportExportBlobStorage(IOptions<BlobStorageOptions> options)
    {
        _options = options.Value;
        _containerClient = _options.IsReportExportStorageConfigured
            ? new BlobServiceClient(_options.ConnectionString).GetBlobContainerClient(_options.ReportExportsContainer)
            : null;
    }

    public bool IsConfigured => _options.IsReportExportStorageConfigured && _containerClient is not null;

    public async Task<ReportExportStoredArtifact> UploadAsync(
        Guid tenantId,
        Guid jobId,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured || _containerClient is null)
        {
            throw new InvalidOperationException("Report export storage is not configured.");
        }

        if (content.CanSeek)
        {
            content.Position = 0;
        }

        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var blobName = $"tenants/{tenantId:D}/report-exports/{jobId:D}/{fileName}";
        var blobClient = _containerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(
            content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType,
                    CacheControl = "no-store"
                }
            },
            cancellationToken);

        var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        return new ReportExportStoredArtifact(blobName, fileName, contentType, properties.Value.ContentLength);
    }

    public async Task<Stream?> OpenReadAsync(string blobName, CancellationToken cancellationToken)
    {
        if (!IsConfigured || _containerClient is null || string.IsNullOrWhiteSpace(blobName))
        {
            return null;
        }

        try
        {
            var blobClient = _containerClient.GetBlobClient(blobName);
            return await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
        {
            return null;
        }
    }

    public async Task DeleteIfExistsAsync(string blobName, CancellationToken cancellationToken)
    {
        if (!IsConfigured || _containerClient is null || string.IsNullOrWhiteSpace(blobName))
        {
            return;
        }

        _ = await _containerClient.GetBlobClient(blobName)
            .DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
    }
}
