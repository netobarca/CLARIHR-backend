using CLARIHR.Domain.LegalRepresentatives;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.LegalRepresentatives;

internal sealed class LegalRepresentativeDocumentTypeCatalogSeedService(ApplicationDbContext dbContext)
{
    public async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        var existingIds = new HashSet<long>(
            await dbContext.LegalRepresentativeDocumentTypeCatalogItems
                .AsNoTracking()
                .Select(item => item.Id)
                .ToListAsync(cancellationToken));

        var missingItems = LegalRepresentativeDocumentTypeCatalog.Items
            .Where(item => !existingIds.Contains(item.Id))
            .Select(LegalRepresentativeDocumentTypeCatalogItem.Create)
            .ToArray();

        if (missingItems.Length == 0)
        {
            return;
        }

        dbContext.LegalRepresentativeDocumentTypeCatalogItems.AddRange(missingItems);
        _ = await dbContext.SaveChangesAsync(cancellationToken);
    }
}
