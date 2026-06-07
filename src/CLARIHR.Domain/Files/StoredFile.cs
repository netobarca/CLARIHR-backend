using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Files;

public sealed class StoredFile : TenantEntity
{
    private StoredFile() { }

    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string Extension { get; private set; } = string.Empty;
    public StorageProvider Provider { get; private set; }
    public string ContainerName { get; private set; } = string.Empty;
    public string ObjectKey { get; private set; } = string.Empty;
    public FileStatus Status { get; private set; }
    public FilePurpose Purpose { get; private set; }
    public FileUploadType UploadType { get; private set; }
    public Guid? EntityId { get; private set; }
    public string CreatedByUserId { get; private set; } = string.Empty;
    public DateTime? UploadConfirmedUtc { get; private set; }
    public DateTime? DeletedUtc { get; private set; }
    public string? FailureReason { get; private set; }
    public Guid ConcurrencyToken { get; private set; } = Guid.NewGuid();

    public static StoredFile Create(
        string fileName,
        string contentType,
        long sizeBytes,
        string extension,
        StorageProvider provider,
        string containerName,
        string objectKey,
        FilePurpose purpose,
        FileUploadType uploadType,
        string createdByUserId,
        Guid? entityId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdByUserId);

        if (sizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "SizeBytes must be positive.");
        }

        return new StoredFile
        {
            FileName = fileName.Trim(),
            ContentType = contentType.Trim().ToLowerInvariant(),
            SizeBytes = sizeBytes,
            Extension = extension.Trim().ToLowerInvariant(),
            Provider = provider,
            ContainerName = containerName.Trim(),
            ObjectKey = objectKey.Trim(),
            Status = FileStatus.PendingUpload,
            Purpose = purpose,
            UploadType = uploadType,
            CreatedByUserId = createdByUserId,
            EntityId = entityId
        };
    }

    public void MarkActive(long actualSizeBytes, string actualContentType)
    {
        EnsureStatus(FileStatus.PendingUpload);

        Status = FileStatus.Active;
        SizeBytes = actualSizeBytes;
        ContentType = actualContentType.Trim().ToLowerInvariant();
        UploadConfirmedUtc = DateTime.UtcNow;
        RefreshConcurrencyToken();
    }

    public void MarkFailed(string reason)
    {
        EnsureStatus(FileStatus.PendingUpload);

        Status = FileStatus.Failed;
        FailureReason = reason;
        RefreshConcurrencyToken();
    }

    public void MarkDeleted()
    {
        if (Status is FileStatus.Deleted)
        {
            return;
        }

        Status = FileStatus.Deleted;
        DeletedUtc = DateTime.UtcNow;
        RefreshConcurrencyToken();
    }

    public void MarkQuarantined(string reason)
    {
        EnsureStatus(FileStatus.Active);

        Status = FileStatus.Quarantined;
        FailureReason = reason;
        RefreshConcurrencyToken();
    }

    public void SetEntityId(Guid entityId)
    {
        if (entityId == Guid.Empty)
        {
            throw new ArgumentException("EntityId cannot be empty.", nameof(entityId));
        }

        EntityId = entityId;
    }

    private void EnsureStatus(FileStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException(
                $"Cannot transition from '{Status}' — expected '{expected}'.");
        }
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
