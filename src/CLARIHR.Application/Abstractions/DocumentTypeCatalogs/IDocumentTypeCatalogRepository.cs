using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.DocumentTypeCatalogs;
using CLARIHR.Domain.DocumentTypeCatalogs;

namespace CLARIHR.Application.Abstractions.DocumentTypeCatalogs;

public interface IDocumentTypeCatalogRepository
{
    void Add(DocumentTypeCatalogItem item);

    Task<DocumentTypeCatalogItem?> GetByIdAsync(
        Guid publicId,
        CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(
        string normalizedCode,
        long? excludingId,
        CancellationToken cancellationToken);

    Task<PagedResponse<DocumentTypeCatalogItemResponse>> SearchAsync(
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<DocumentTypeCatalogItemResponse?> GetResponseByIdAsync(
        Guid publicId,
        CancellationToken cancellationToken);

    Task<DocumentTypeCatalogLookup?> GetActiveLookupByIdAsync(
        Guid publicId,
        CancellationToken cancellationToken);

    Task<bool> IsInUseAsync(
        long catalogItemId,
        CancellationToken cancellationToken);
}
