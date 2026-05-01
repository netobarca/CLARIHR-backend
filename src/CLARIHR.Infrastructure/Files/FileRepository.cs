using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Domain.Files;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Files;

internal sealed class FileRepository(ApplicationDbContext dbContext) : IFileRepository
{
    public async Task<StoredFile?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken)
    {
        return await dbContext.StoredFiles
            .FirstOrDefaultAsync(f => f.PublicId == publicId, cancellationToken);
    }

    public async Task AddAsync(StoredFile file, CancellationToken cancellationToken)
    {
        await dbContext.StoredFiles.AddAsync(file, cancellationToken);
    }

    public async Task<IReadOnlyList<StoredFile>> GetExpiredPendingUploadsAsync(
        DateTime olderThan,
        int batchSize,
        CancellationToken cancellationToken)
    {
        return await dbContext.StoredFiles
            // Intentional tenant filter bypass: cleanup job must scan all tenants to purge abandoned uploads.
            .IgnoreQueryFilters()
            .Where(f => f.Status == FileStatus.PendingUpload && f.CreatedUtc < olderThan)
            .OrderBy(f => f.CreatedUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }
}
