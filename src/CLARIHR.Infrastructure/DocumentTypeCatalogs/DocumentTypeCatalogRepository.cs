using CLARIHR.Application.Abstractions.DocumentTypeCatalogs;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.DocumentTypeCatalogs;
using CLARIHR.Domain.DocumentTypeCatalogs;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.DocumentTypeCatalogs;

internal sealed class DocumentTypeCatalogRepository(ApplicationDbContext dbContext) : IDocumentTypeCatalogRepository
{
    public void Add(DocumentTypeCatalogItem item) =>
        dbContext.DocumentTypeCatalogItems.Add(item);

    public Task<DocumentTypeCatalogItem?> GetByIdAsync(
        Guid publicId,
        CancellationToken cancellationToken) =>
        dbContext.DocumentTypeCatalogItems
            .SingleOrDefaultAsync(item => item.PublicId == publicId, cancellationToken);

    public Task<bool> CodeExistsAsync(
        string normalizedCode,
        long? excludingId,
        CancellationToken cancellationToken) =>
        dbContext.DocumentTypeCatalogItems.AnyAsync(
            item => item.NormalizedCode == normalizedCode &&
                    (!excludingId.HasValue || item.Id != excludingId.Value),
            cancellationToken);

    public async Task<PagedResponse<DocumentTypeCatalogItemResponse>> SearchAsync(
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.DocumentTypeCatalogItems.AsNoTracking();

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
            .Select(item => new DocumentTypeCatalogItemResponse(
                item.PublicId,
                item.Code,
                item.Name,
                item.SortOrder,
                item.IsActive,
                item.ConcurrencyToken,
                item.CreatedUtc,
                item.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<DocumentTypeCatalogItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<DocumentTypeCatalogItemResponse?> GetResponseByIdAsync(
        Guid publicId,
        CancellationToken cancellationToken) =>
        dbContext.DocumentTypeCatalogItems
            .AsNoTracking()
            .Where(item => item.PublicId == publicId)
            .Select(item => new DocumentTypeCatalogItemResponse(
                item.PublicId,
                item.Code,
                item.Name,
                item.SortOrder,
                item.IsActive,
                item.ConcurrencyToken,
                item.CreatedUtc,
                item.ModifiedUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<DocumentTypeCatalogLookup?> GetActiveLookupByIdAsync(
        Guid publicId,
        CancellationToken cancellationToken) =>
        dbContext.DocumentTypeCatalogItems
            .AsNoTracking()
            .Where(item => item.PublicId == publicId && item.IsActive)
            .Select(item => new DocumentTypeCatalogLookup(item.Id, item.PublicId, item.Code, item.Name, item.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> IsInUseAsync(
        long catalogItemId,
        CancellationToken cancellationToken) =>
        dbContext.PersonnelFileDocuments.AnyAsync(
            doc => doc.DocumentTypeCatalogItemId == catalogItemId,
            cancellationToken);
}
