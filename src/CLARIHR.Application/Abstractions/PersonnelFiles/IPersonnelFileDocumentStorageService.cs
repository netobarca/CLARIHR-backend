using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Abstractions.PersonnelFiles;

public sealed record PersonnelFileStoredDocumentArtifact(
    string BlobName,
    string BlobUrl,
    long SizeBytes);

public interface IPersonnelFileDocumentStorageService
{
    bool IsConfigured { get; }

    Task<PersonnelFileStoredDocumentArtifact> UploadAsync(
        Guid tenantId,
        Guid personnelFileId,
        Guid documentId,
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken);

    Task<string?> ResolveForReadAsync(string? persistedBlobUrl, CancellationToken cancellationToken);

    Task DeleteIfExistsAsync(string blobName, CancellationToken cancellationToken);
}
