using System.Collections.Concurrent;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CLARIHR.Infrastructure.JobProfiles;

internal sealed class JobCatalogRepository(
    ApplicationDbContext dbContext,
    IMemoryCache memoryCache) : IJobCatalogRepository
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(3);
    private static readonly ConcurrentDictionary<string, byte> CacheKeys = new(StringComparer.Ordinal);

    public void Add(JobCatalogItem item) => dbContext.JobCatalogItems.Add(item);

    public Task<JobCatalogItem?> GetByIdAsync(Guid itemId, CancellationToken cancellationToken) =>
        dbContext.JobCatalogItems.SingleOrDefaultAsync(item => item.PublicId == itemId, cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid itemId, CancellationToken cancellationToken) =>
        dbContext.JobCatalogItems
            .IgnoreQueryFilters()
            .AnyAsync(item => item.PublicId == itemId, cancellationToken);

    public Task<bool> CodeExistsAsync(
        Guid tenantId,
        JobCatalogCategory category,
        string normalizedCode,
        long? excludingItemId,
        CancellationToken cancellationToken) =>
        dbContext.JobCatalogItems.AnyAsync(
            item => item.TenantId == tenantId &&
                    item.Category == category &&
                    item.NormalizedCode == normalizedCode &&
                    (!excludingItemId.HasValue || item.Id != excludingItemId.Value),
            cancellationToken);

    public async Task<PagedResponse<JobCatalogItemResponse>> SearchAsync(
        Guid tenantId,
        JobCatalogCategory category,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var cacheKey = BuildCacheKey(tenantId, category, isActive, search, pageNumber, pageSize);
        if (memoryCache.TryGetValue(cacheKey, out PagedResponse<JobCatalogItemResponse>? cached) && cached is not null)
        {
            return cached;
        }

        var query = dbContext.JobCatalogItems
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.Category == category);

        if (isActive.HasValue)
        {
            query = query.Where(item => item.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(item =>
                item.NormalizedCode.Contains(normalizedSearch) ||
                item.NormalizedName.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Code)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new JobCatalogItemResponse(
                item.PublicId,
                item.Category,
                item.Code,
                item.Name,
                item.IsSystem,
                item.IsActive,
                item.ConcurrencyToken,
                item.CreatedUtc,
                item.ModifiedUtc))
            .ToListAsync(cancellationToken);

        var payload = new PagedResponse<JobCatalogItemResponse>(items, pageNumber, pageSize, totalCount);
        memoryCache.Set(cacheKey, payload, CacheTtl);
        _ = CacheKeys.TryAdd(cacheKey, 0);

        return payload;
    }

    public Task<JobCatalogItemResponse?> GetResponseByIdAsync(Guid itemId, CancellationToken cancellationToken) =>
        dbContext.JobCatalogItems
            .AsNoTracking()
            .Where(item => item.PublicId == itemId)
            .Select(item => new JobCatalogItemResponse(
                item.PublicId,
                item.Category,
                item.Code,
                item.Name,
                item.IsSystem,
                item.IsActive,
                item.ConcurrencyToken,
                item.CreatedUtc,
                item.ModifiedUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<JobCatalogItem?> ResolveActiveItemAsync(
        Guid tenantId,
        JobCatalogCategory category,
        Guid itemId,
        CancellationToken cancellationToken) =>
        dbContext.JobCatalogItems
            .SingleOrDefaultAsync(
                item => item.TenantId == tenantId &&
                        item.Category == category &&
                        item.PublicId == itemId &&
                        item.IsActive,
                cancellationToken);

    public Task<JobCatalogItem?> FindActiveByNameAsync(
        Guid tenantId,
        JobCatalogCategory category,
        string normalizedName,
        CancellationToken cancellationToken) =>
        dbContext.JobCatalogItems
            .SingleOrDefaultAsync(
                item => item.TenantId == tenantId &&
                        item.Category == category &&
                        item.NormalizedName == normalizedName &&
                        item.IsActive,
                cancellationToken);

    public void InvalidateCategoryCache(Guid tenantId, JobCatalogCategory category)
    {
        var prefix = BuildCachePrefix(tenantId, category);
        var keys = CacheKeys.Keys
            .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();

        foreach (var key in keys)
        {
            memoryCache.Remove(key);
            _ = CacheKeys.TryRemove(key, out _);
        }
    }

    private static string BuildCacheKey(
        Guid tenantId,
        JobCatalogCategory category,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize) =>
        $"{BuildCachePrefix(tenantId, category)}:{isActive?.ToString() ?? "null"}:{(search ?? string.Empty).Trim().ToUpperInvariant()}:{pageNumber}:{pageSize}";

    private static string BuildCachePrefix(Guid tenantId, JobCatalogCategory category) =>
        $"job-catalog:{tenantId:D}:{category}";
}
