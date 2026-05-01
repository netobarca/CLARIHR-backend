using CLARIHR.Domain.Files;

namespace CLARIHR.Application.Abstractions.Files;

public interface IFileRepository
{
    Task<StoredFile?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken);

    Task AddAsync(StoredFile file, CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredFile>> GetExpiredPendingUploadsAsync(
        DateTime olderThan,
        int batchSize,
        CancellationToken cancellationToken);
}
