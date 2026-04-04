using CLARIHR.Domain.InternalCatalogs;

namespace CLARIHR.Application.Abstractions.InternalCatalogs;

public sealed record InternalCatalogSearchResult(
    Guid Id,
    string Value,
    double Score,
    bool IsExactMatch,
    bool IsPrefixMatch,
    int UsageCount);

public interface IInternalCatalogRepository
{
    void Add(InternalCatalogValue value);

    Task<InternalCatalogValue?> GetByIdAsync(Guid valueId, CancellationToken cancellationToken);

    Task<InternalCatalogValue?> FindActiveByExactValueAsync(
        string catalogKey,
        string normalizedValue,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<InternalCatalogSearchResult>> SearchAsync(
        string catalogKey,
        string normalizedSearch,
        int limit,
        double minScore,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<InternalCatalogSearchResult>> FindSimilarAsync(
        string catalogKey,
        string normalizedValue,
        int limit,
        double minScore,
        CancellationToken cancellationToken);
}
