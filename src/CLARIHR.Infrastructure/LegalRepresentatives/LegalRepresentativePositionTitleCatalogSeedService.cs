using CLARIHR.Domain.LegalRepresentatives;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.LegalRepresentatives;

internal sealed class LegalRepresentativePositionTitleCatalogSeedService(ApplicationDbContext dbContext)
{
    public async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        var existingIds = new HashSet<long>(
            await dbContext.LegalRepresentativePositionTitleCatalogItems
                .AsNoTracking()
                .Select(item => item.Id)
                .ToListAsync(cancellationToken));

        var missingItems = LegalRepresentativePositionTitleCatalog.Items
            .Where(item => !existingIds.Contains(item.Id))
            .Select(LegalRepresentativePositionTitleCatalogItem.Create)
            .ToArray();

        if (missingItems.Length == 0)
        {
            return;
        }

        dbContext.LegalRepresentativePositionTitleCatalogItems.AddRange(missingItems);
        _ = await dbContext.SaveChangesAsync(cancellationToken);
    }
}
