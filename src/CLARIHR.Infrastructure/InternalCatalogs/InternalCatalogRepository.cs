using CLARIHR.Application.Abstractions.InternalCatalogs;
using CLARIHR.Domain.InternalCatalogs;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.InternalCatalogs;

internal sealed class InternalCatalogRepository(ApplicationDbContext dbContext) : IInternalCatalogRepository
{
    public void Add(InternalCatalogValue value) => dbContext.InternalCatalogValues.Add(value);

    public Task<InternalCatalogValue?> GetByIdAsync(Guid valueId, CancellationToken cancellationToken) =>
        dbContext.InternalCatalogValues
            .SingleOrDefaultAsync(item => item.PublicId == valueId, cancellationToken);

    public Task<InternalCatalogValue?> FindActiveByExactValueAsync(
        string catalogKey,
        string normalizedValue,
        CancellationToken cancellationToken)
    {
        var normalizedCatalogKey = InternalCatalogValue.InternalCatalogNormalization.NormalizeCatalogKey(catalogKey);

        return dbContext.InternalCatalogValues
            .SingleOrDefaultAsync(
                item => item.IsActive &&
                        item.CatalogKey == normalizedCatalogKey &&
                        item.NormalizedValue == normalizedValue,
                cancellationToken);
    }

    public async Task<IReadOnlyCollection<InternalCatalogSearchResult>> SearchAsync(
        string catalogKey,
        string normalizedSearch,
        int limit,
        double minScore,
        CancellationToken cancellationToken)
    {
        var normalizedCatalogKey = InternalCatalogValue.InternalCatalogNormalization.NormalizeCatalogKey(catalogKey);

        var matches = await dbContext.InternalCatalogValues
            .AsNoTracking()
            .Where(item => item.IsActive && item.CatalogKey == normalizedCatalogKey)
            .Select(item => new
            {
                item.PublicId,
                item.Value,
                Score = EF.Functions.TrigramsWordSimilarity(item.NormalizedValue, normalizedSearch),
                IsExactMatch = item.NormalizedValue == normalizedSearch,
                IsPrefixMatch = item.NormalizedValue.StartsWith(normalizedSearch),
                item.UsageCount
            })
            .Where(item => item.IsExactMatch || item.IsPrefixMatch || item.Score >= minScore)
            .OrderByDescending(item => item.IsExactMatch)
            .ThenByDescending(item => item.IsPrefixMatch)
            .ThenByDescending(item => item.Score)
            .ThenByDescending(item => item.UsageCount)
            .ThenBy(item => item.Value)
            .Take(limit)
            .ToArrayAsync(cancellationToken);

        return matches
            .Select(static item => new InternalCatalogSearchResult(
                item.PublicId,
                item.Value,
                item.Score,
                item.IsExactMatch,
                item.IsPrefixMatch,
                item.UsageCount))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<InternalCatalogSearchResult>> FindSimilarAsync(
        string catalogKey,
        string normalizedValue,
        int limit,
        double minScore,
        CancellationToken cancellationToken)
    {
        var normalizedCatalogKey = InternalCatalogValue.InternalCatalogNormalization.NormalizeCatalogKey(catalogKey);

        var matches = await dbContext.InternalCatalogValues
            .AsNoTracking()
            .Where(item => item.IsActive && item.CatalogKey == normalizedCatalogKey)
            .Select(item => new
            {
                item.PublicId,
                item.Value,
                Score = EF.Functions.TrigramsSimilarity(item.NormalizedValue, normalizedValue),
                IsExactMatch = item.NormalizedValue == normalizedValue,
                IsPrefixMatch = item.NormalizedValue.StartsWith(normalizedValue),
                item.UsageCount
            })
            .Where(item => item.IsExactMatch || item.Score >= minScore)
            .OrderByDescending(item => item.IsExactMatch)
            .ThenByDescending(item => item.Score)
            .ThenByDescending(item => item.UsageCount)
            .ThenBy(item => item.Value)
            .Take(limit)
            .ToArrayAsync(cancellationToken);

        return matches
            .Select(static item => new InternalCatalogSearchResult(
                item.PublicId,
                item.Value,
                item.Score,
                item.IsExactMatch,
                item.IsPrefixMatch,
                item.UsageCount))
            .ToArray();
    }
}
