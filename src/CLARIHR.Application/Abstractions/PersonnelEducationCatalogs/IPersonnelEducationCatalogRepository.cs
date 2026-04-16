using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PersonnelEducationCatalogs;
using CLARIHR.Application.Features.PersonnelEducationCatalogs.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Abstractions.PersonnelEducationCatalogs;

public interface IPersonnelEducationCatalogRepository
{
    void Add(PersonnelEducationCatalogItem item);

    Task<PersonnelEducationCatalogCountryLookup?> GetCompanyCountryAsync(
        Guid companyId,
        CancellationToken cancellationToken);

    Task<PersonnelEducationCatalogItem?> GetByIdAsync(
        PersonnelEducationCatalogType catalogType,
        Guid id,
        CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(
        PersonnelEducationCatalogType catalogType,
        Guid id,
        CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(
        Guid companyId,
        PersonnelEducationCatalogType catalogType,
        string normalizedCode,
        long? excludingId,
        CancellationToken cancellationToken);

    Task<PagedResponse<PersonnelEducationCatalogItemResponse>> SearchAsync(
        Guid companyId,
        PersonnelEducationCatalogType catalogType,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PersonnelEducationCatalogItemResponse?> GetResponseByIdAsync(
        Guid companyId,
        PersonnelEducationCatalogType catalogType,
        Guid id,
        CancellationToken cancellationToken);

    Task<PersonnelEducationCatalogLookup?> GetActiveLookupByIdAsync(
        Guid companyId,
        PersonnelEducationCatalogType catalogType,
        Guid id,
        CancellationToken cancellationToken);

    Task<bool> IsInUseAsync(
        PersonnelEducationCatalogType catalogType,
        long catalogItemId,
        CancellationToken cancellationToken);
}
