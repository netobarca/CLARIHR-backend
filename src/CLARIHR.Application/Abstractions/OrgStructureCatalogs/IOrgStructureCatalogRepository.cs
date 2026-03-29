using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.OrgStructureCatalogs;
using CLARIHR.Domain.OrgStructureCatalogs;

namespace CLARIHR.Application.Abstractions.OrgStructureCatalogs;

public interface IOrgStructureCatalogRepository
{
    void AddOrgUnitType(OrgUnitTypeCatalogItem item);

    void AddFunctionalArea(FunctionalAreaCatalogItem item);

    Task<OrgUnitTypeCatalogItem?> GetOrgUnitTypeByIdAsync(Guid orgUnitTypeId, CancellationToken cancellationToken);

    Task<FunctionalAreaCatalogItem?> GetFunctionalAreaByIdAsync(Guid functionalAreaId, CancellationToken cancellationToken);

    Task<bool> ExistsOrgUnitTypeOutsideTenantAsync(Guid orgUnitTypeId, CancellationToken cancellationToken);

    Task<bool> ExistsFunctionalAreaOutsideTenantAsync(Guid functionalAreaId, CancellationToken cancellationToken);

    Task<bool> OrgUnitTypeCodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        long? excludingId,
        CancellationToken cancellationToken);

    Task<bool> FunctionalAreaCodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        long? excludingId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CompanyTypeCatalogItemResponse>> GetActiveCompanyTypesByCountryCodeAsync(
        string countryCode,
        CancellationToken cancellationToken);

    Task<PagedResponse<OrgUnitTypeCatalogItemResponse>> SearchOrgUnitTypesAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PagedResponse<FunctionalAreaCatalogItemResponse>> SearchFunctionalAreasAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<OrgUnitTypeCatalogItemResponse?> GetOrgUnitTypeResponseByIdAsync(Guid orgUnitTypeId, CancellationToken cancellationToken);

    Task<FunctionalAreaCatalogItemResponse?> GetFunctionalAreaResponseByIdAsync(Guid functionalAreaId, CancellationToken cancellationToken);

    Task<bool> HasOrgUnitsUsingOrgUnitTypeAsync(long orgUnitTypeCatalogItemId, CancellationToken cancellationToken);

    Task<bool> HasPositionCategoryClassificationsUsingOrgUnitTypeAsync(long orgUnitTypeCatalogItemId, CancellationToken cancellationToken);

    Task<bool> HasOrgUnitsUsingFunctionalAreaAsync(long functionalAreaCatalogItemId, CancellationToken cancellationToken);

    Task<CatalogReferenceLookup?> GetActiveCompanyTypeLookupAsync(
        long countryCatalogItemId,
        Guid companyTypeId,
        CancellationToken cancellationToken);

    Task<CatalogReferenceLookup?> GetActiveOrgUnitTypeLookupAsync(
        Guid tenantId,
        Guid orgUnitTypeId,
        CancellationToken cancellationToken);

    Task<CatalogReferenceLookup?> GetActiveFunctionalAreaLookupAsync(
        Guid tenantId,
        Guid functionalAreaId,
        CancellationToken cancellationToken);
}
