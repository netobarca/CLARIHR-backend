using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.SystemCatalogs;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Abstractions.SystemCatalogs;

public interface ISystemCatalogRepository
{
    void Add(CountryScopedCatalogItem item);

    Task<CountryScopedCatalogItem?> GetByIdAsync(
        SystemCatalogType catalogType,
        Guid id,
        CancellationToken cancellationToken);

    Task<bool> ExistsByCodeAsync(
        SystemCatalogType catalogType,
        long countryCatalogItemId,
        string normalizedCode,
        long? excludingId,
        CancellationToken cancellationToken);

    Task<PagedResponse<SystemCatalogItemResponse>> SearchAsync(
        SystemCatalogType catalogType,
        long countryCatalogItemId,
        bool? isActive,
        string? search,
        Guid? parentPublicId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<SystemCatalogItemResponse?> GetResponseByIdAsync(
        SystemCatalogType catalogType,
        Guid id,
        CancellationToken cancellationToken);

    Task<DepartmentCatalogLookup?> GetDepartmentLookupByIdAsync(
        Guid id,
        CancellationToken cancellationToken);
}
