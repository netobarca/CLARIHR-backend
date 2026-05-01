using CLARIHR.Domain.Files;

namespace CLARIHR.Application.Abstractions.Files;

public interface IFileStorageProvider
{
    StorageProvider ProviderType { get; }

    Task<CreateUploadSessionResult> CreateUploadSessionAsync(
        CreateUploadSessionProviderCommand command,
        CancellationToken cancellationToken);

    Task<CreateReadSessionResult> CreateReadSessionAsync(
        CreateReadSessionCommand command,
        CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string containerName, string objectKey, CancellationToken cancellationToken);

    Task<FileObjectInfo?> GetObjectInfoAsync(string containerName, string objectKey, CancellationToken cancellationToken);

    Task DeleteAsync(string containerName, string objectKey, CancellationToken cancellationToken);
}

public sealed record CreateUploadSessionProviderCommand(
    string ContainerName,
    string ObjectKey,
    string ContentType,
    long SizeBytes);

public sealed record CreateUploadSessionResult(
    string UploadUrl,
    DateTime ExpiresUtc,
    IReadOnlyDictionary<string, string> RequiredHeaders);

public sealed record CreateReadSessionCommand(
    string ContainerName,
    string ObjectKey);

public sealed record CreateReadSessionResult(
    string ReadUrl,
    DateTime ExpiresUtc);

public sealed record FileObjectInfo(
    long SizeBytes,
    string ContentType,
    DateTime LastModifiedUtc);
