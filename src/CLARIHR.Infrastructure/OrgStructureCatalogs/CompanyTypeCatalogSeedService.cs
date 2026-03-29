using CLARIHR.Application.Abstractions.OrgStructureCatalogs;
using CLARIHR.Domain.OrgStructureCatalogs;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.OrgStructureCatalogs;

internal sealed class CompanyTypeCatalogSeedService(ApplicationDbContext dbContext) : ICompanyTypeCatalogSeedService
{
    public async Task EnsureSeededAsync(Guid ownerUserPublicId, CancellationToken cancellationToken)
    {
        if (ownerUserPublicId == Guid.Empty)
        {
            throw new ArgumentException("Owner user public id cannot be empty.", nameof(ownerUserPublicId));
        }

        var existingCodes = await dbContext.CompanyTypeCatalogItems
            .AsNoTracking()
            .Where(item => item.OwnerUserPublicId == ownerUserPublicId)
            .Select(item => item.NormalizedCode)
            .ToListAsync(cancellationToken);

        var existingCodeSet = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingItems = CompanyTypeCatalog.Items
            .Where(definition => !existingCodeSet.Contains(definition.Code))
            .Select(definition => CompanyTypeCatalogItem.Create(
                ownerUserPublicId,
                definition.Code,
                definition.Name,
                definition.Description,
                definition.SortOrder))
            .ToArray();

        if (missingItems.Length == 0)
        {
            return;
        }

        dbContext.CompanyTypeCatalogItems.AddRange(missingItems);
        _ = await dbContext.SaveChangesAsync(cancellationToken);
    }
}
