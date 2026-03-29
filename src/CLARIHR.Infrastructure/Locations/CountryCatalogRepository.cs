using CLARIHR.Application.Abstractions.Locations;
using CLARIHR.Application.Features.Locations.Countries;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Locations;

internal sealed class CountryCatalogRepository(ApplicationDbContext dbContext) : ICountryCatalogRepository
{
    public async Task<IReadOnlyCollection<CountryCatalogItemResponse>> GetActiveItemsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.CountryCatalogItems
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Select(item => new CountryCatalogItemResponse(
                item.PublicId,
                item.Code,
                item.Name,
                item.SortOrder))
            .ToArrayAsync(cancellationToken);
    }

    public Task<CountryCatalogLookup?> GetActiveByCodeAsync(string countryCode, CancellationToken cancellationToken)
    {
        var normalizedCode = countryCode.Trim().ToUpperInvariant();

        return dbContext.CountryCatalogItems
            .AsNoTracking()
            .Where(item => item.IsActive && item.NormalizedCode == normalizedCode)
            .Select(item => new CountryCatalogLookup(
                item.Id,
                item.PublicId,
                item.Code,
                item.Name,
                item.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
