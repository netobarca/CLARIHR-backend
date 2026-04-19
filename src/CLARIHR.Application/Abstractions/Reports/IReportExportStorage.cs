namespace CLARIHR.Application.Abstractions.Reports;

public sealed record ReportExportStoredArtifact(
    string BlobName,
    string FileName,
    string ContentType,
    long SizeBytes);

public interface IReportExportStorage
{
    bool IsConfigured { get; }

    Task<ReportExportStoredArtifact> UploadAsync(
        Guid tenantId,
        Guid jobId,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken);

    Task<Stream?> OpenReadAsync(string blobName, CancellationToken cancellationToken);

    Task DeleteIfExistsAsync(string blobName, CancellationToken cancellationToken);
}
