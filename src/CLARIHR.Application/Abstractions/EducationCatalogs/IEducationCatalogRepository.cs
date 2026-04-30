using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.EducationCatalogs;
using CLARIHR.Application.Features.EducationCatalogs.Common;
using CLARIHR.Domain.EducationCatalogs;

namespace CLARIHR.Application.Abstractions.EducationCatalogs;

public interface IEducationCatalogRepository
{
    void Add(EducationCatalogItem item);

    Task<EducationCatalogItem?> GetByIdAsync(
        EducationCatalogType catalogType,
        Guid id,
        CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(
        EducationCatalogType catalogType,
        string normalizedCode,
        long? excludingId,
        CancellationToken cancellationToken);

    Task<PagedResponse<EducationCatalogItemResponse>> SearchAsync(
        EducationCatalogType catalogType,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<EducationCatalogItemResponse?> GetResponseByIdAsync(
        EducationCatalogType catalogType,
        Guid id,
        CancellationToken cancellationToken);

    Task<EducationCatalogLookup?> GetActiveLookupByIdAsync(
        EducationCatalogType catalogType,
        Guid id,
        CancellationToken cancellationToken);

    Task<bool> IsInUseAsync(
        EducationCatalogType catalogType,
        long catalogItemId,
        CancellationToken cancellationToken);
}
