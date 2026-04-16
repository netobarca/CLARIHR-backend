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
                item.NormalizedValue,
                item.UsageCount
            })
            .ToArrayAsync(cancellationToken);

        return matches
            .Select(item =>
            {
                var isExactMatch = string.Equals(item.NormalizedValue, normalizedSearch, StringComparison.Ordinal);
                var isPrefixMatch = item.NormalizedValue.StartsWith(normalizedSearch, StringComparison.Ordinal);
                var score = ComputeSimilarity(item.NormalizedValue, normalizedSearch);

                return new InternalCatalogSearchResult(
                    item.PublicId,
                    item.Value,
                    score,
                    isExactMatch,
                    isPrefixMatch,
                    item.UsageCount);
            })
            .Where(item => item.IsExactMatch || item.IsPrefixMatch || item.Score >= minScore)
            .OrderByDescending(static item => item.IsExactMatch)
            .ThenByDescending(static item => item.IsPrefixMatch)
            .ThenByDescending(static item => item.Score)
            .ThenByDescending(static item => item.UsageCount)
            .ThenBy(static item => item.Value)
            .Take(limit)
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
                item.NormalizedValue,
                item.UsageCount
            })
            .ToArrayAsync(cancellationToken);

        return matches
            .Select(item =>
            {
                var isExactMatch = string.Equals(item.NormalizedValue, normalizedValue, StringComparison.Ordinal);
                var isPrefixMatch = item.NormalizedValue.StartsWith(normalizedValue, StringComparison.Ordinal);
                var score = ComputeSimilarity(item.NormalizedValue, normalizedValue);

                return new InternalCatalogSearchResult(
                    item.PublicId,
                    item.Value,
                    score,
                    isExactMatch,
                    isPrefixMatch,
                    item.UsageCount);
            })
            .Where(item => item.IsExactMatch || item.Score >= minScore)
            .OrderByDescending(static item => item.IsExactMatch)
            .ThenByDescending(static item => item.Score)
            .ThenByDescending(static item => item.UsageCount)
            .ThenBy(static item => item.Value)
            .Take(limit)
            .ToArray();
    }

    private static double ComputeSimilarity(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return 1d;
        }

        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return 0d;
        }

        var editScore = ComputeEditDistanceSimilarity(left, right);
        var tokenScore = ComputeTokenOverlapSimilarity(left, right);

        return Math.Max(editScore, tokenScore);
    }

    private static double ComputeEditDistanceSimilarity(string left, string right)
    {
        var maxLength = Math.Max(left.Length, right.Length);
        if (maxLength == 0)
        {
            return 1d;
        }

        var distance = ComputeLevenshteinDistance(left, right);
        var similarity = 1d - (double)distance / maxLength;
        return Math.Clamp(similarity, 0d, 1d);
    }

    private static double ComputeTokenOverlapSimilarity(string left, string right)
    {
        var leftTokens = left
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
        var rightTokens = right
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

        if (leftTokens.Count == 0 || rightTokens.Count == 0)
        {
            return 0d;
        }

        var overlap = leftTokens.Intersect(rightTokens, StringComparer.Ordinal).Count();
        var union = leftTokens.Union(rightTokens, StringComparer.Ordinal).Count();
        return union == 0 ? 0d : (double)overlap / union;
    }

    private static int ComputeLevenshteinDistance(string left, string right)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var column = 0; column <= right.Length; column++)
        {
            previous[column] = column;
        }

        for (var row = 1; row <= left.Length; row++)
        {
            current[0] = row;
            for (var column = 1; column <= right.Length; column++)
            {
                var substitutionCost = left[row - 1] == right[column - 1] ? 0 : 1;
                current[column] = Math.Min(
                    Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + substitutionCost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
