using CLARIHR.Domain.LegalRepresentatives;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.LegalRepresentatives;

internal sealed class LegalRepresentativeRepresentationTypeCatalogSeedService(ApplicationDbContext dbContext)
{
    public async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        var existingIds = new HashSet<long>(
            await dbContext.LegalRepresentativeRepresentationTypeCatalogItems
                .AsNoTracking()
                .Select(item => item.Id)
                .ToListAsync(cancellationToken));

        var missingItems = LegalRepresentativeRepresentationTypeCatalog.Items
            .Where(item => !existingIds.Contains(item.Id))
            .Select(LegalRepresentativeRepresentationTypeCatalogItem.Create)
            .ToArray();

        if (missingItems.Length == 0)
        {
            return;
        }

        dbContext.LegalRepresentativeRepresentationTypeCatalogItems.AddRange(missingItems);
        _ = await dbContext.SaveChangesAsync(cancellationToken);
    }
}
