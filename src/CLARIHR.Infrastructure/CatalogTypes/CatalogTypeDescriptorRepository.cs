using CLARIHR.Application.Abstractions.CatalogTypes;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.JobProfileCatalogTypes;
using CLARIHR.Domain.CatalogTypes;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CLARIHR.Infrastructure.CatalogTypes;

internal sealed class CatalogTypeDescriptorRepository(
    ApplicationDbContext dbContext,
    IMemoryCache memoryCache)
    : ICatalogTypeDescriptorRepository
{
    // Intentionally a GLOBAL (non-tenant) cache key. project-foundation.md §12.5
    // ("caché siempre tenant-scoped") governs tenant-scoped data; CatalogTypeDescriptor
    // is system-scoped by domain design (CatalogTypeDescriptor : SystemScopedCatalogItem,
    // NO TenantId column, mutated only via Backoffice under the PlatformOperator policy),
    // so this registry is platform-global reference metadata shared across all tenants.
    // A global key here is correct, NOT a §12.5 violation — do not re-scope per tenant.
    // The system-scoping invariant that justifies this is locked by
    // CatalogTypeDescriptorSystemScopingGuardrailsTests: if the entity ever becomes
    // tenant-scoped that test goes red and forces revisiting this key.
    // Invalidation is backoffice-driven (Invalidate() on every catalog-type mutation).
    private const string AllCacheKey = "catalog-type-descriptors:all";

    // §D4 (doc technical-debt/07): this TTL IS the cross-instance freshness
    // guarantee. Invalidate() is process-local, so other instances converge to a
    // Backoffice edit within at most this window (immediate on the editing
    // instance). The freshness contract documented on
    // ICatalogTypeDescriptorRepository.GetAllAsync states "≤ ~3 min"; keep them in
    // sync — CatalogTypeDescriptorCacheFreshnessGuardrailsTests fails if this
    // exceeds 3 minutes so the documented guarantee cannot silently rot.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(3);

    public void Add(CatalogTypeDescriptor item) =>
        dbContext.CatalogTypeDescriptors.Add(item);

    public Task<CatalogTypeDescriptor?> GetByIdAsync(
        Guid publicId,
        CancellationToken cancellationToken) =>
        dbContext.CatalogTypeDescriptors
            .SingleOrDefaultAsync(item => item.PublicId == publicId, cancellationToken);

    public Task<bool> CodeExistsAsync(
        string normalizedCode,
        long? excludingId,
        CancellationToken cancellationToken) =>
        dbContext.CatalogTypeDescriptors.AnyAsync(
            item => item.NormalizedCode == normalizedCode &&
                    (!excludingId.HasValue || item.Id != excludingId.Value),
            cancellationToken);

    public async Task<PagedResponse<JobProfileCatalogTypeResponse>> SearchAsync(
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.CatalogTypeDescriptors.AsNoTracking();

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
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ThenBy(item => item.Code)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new JobProfileCatalogTypeResponse(
                item.PublicId,
                item.Code,
                item.Name,
                item.SortOrder,
                item.IsActive,
                item.ConcurrencyToken,
                item.CreatedUtc,
                item.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<JobProfileCatalogTypeResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<JobProfileCatalogTypeResponse?> GetResponseByIdAsync(
        Guid publicId,
        CancellationToken cancellationToken) =>
        dbContext.CatalogTypeDescriptors
            .AsNoTracking()
            .Where(item => item.PublicId == publicId)
            .Select(item => new JobProfileCatalogTypeResponse(
                item.PublicId,
                item.Code,
                item.Name,
                item.SortOrder,
                item.IsActive,
                item.ConcurrencyToken,
                item.CreatedUtc,
                item.ModifiedUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CatalogTypeDescriptorLookup>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        if (memoryCache.TryGetValue(AllCacheKey, out IReadOnlyList<CatalogTypeDescriptorLookup>? cached) &&
            cached is not null)
        {
            return cached;
        }

        var items = await dbContext.CatalogTypeDescriptors
            .AsNoTracking()
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Select(item => new CatalogTypeDescriptorLookup(item.Code, item.Name, item.IsActive))
            .ToListAsync(cancellationToken);

        memoryCache.Set(AllCacheKey, (IReadOnlyList<CatalogTypeDescriptorLookup>)items, CacheTtl);
        return items;
    }

    public void Invalidate() => memoryCache.Remove(AllCacheKey);
}
