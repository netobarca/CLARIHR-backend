using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.OrgStructureCatalogs;
using CLARIHR.Domain.OrgStructureCatalogs;

namespace CLARIHR.Application.Abstractions.OrgStructureCatalogs;

public interface IOrgStructureCatalogRepository
{
    void AddCompanyType(CompanyTypeCatalogItem item);

    void AddOrgUnitType(OrgUnitTypeCatalogItem item);

    void AddFunctionalArea(FunctionalAreaCatalogItem item);

    Task<CompanyTypeCatalogItem?> GetCompanyTypeByIdAsync(Guid companyTypeId, CancellationToken cancellationToken);

    Task<OrgUnitTypeCatalogItem?> GetOrgUnitTypeByIdAsync(Guid orgUnitTypeId, CancellationToken cancellationToken);

    Task<FunctionalAreaCatalogItem?> GetFunctionalAreaByIdAsync(Guid functionalAreaId, CancellationToken cancellationToken);

    Task<bool> ExistsOrgUnitTypeOutsideTenantAsync(Guid orgUnitTypeId, CancellationToken cancellationToken);

    Task<bool> ExistsFunctionalAreaOutsideTenantAsync(Guid functionalAreaId, CancellationToken cancellationToken);

    Task<bool> CompanyTypeCodeExistsAsync(
        Guid ownerUserPublicId,
        string normalizedCode,
        long? excludingId,
        CancellationToken cancellationToken);

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

    Task<PagedResponse<CompanyTypeCatalogItemResponse>> SearchCompanyTypesAsync(
        Guid ownerUserPublicId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
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

    Task<CompanyTypeCatalogItemResponse?> GetCompanyTypeResponseByIdAsync(Guid companyTypeId, CancellationToken cancellationToken);

    Task<OrgUnitTypeCatalogItemResponse?> GetOrgUnitTypeResponseByIdAsync(Guid orgUnitTypeId, CancellationToken cancellationToken);

    Task<FunctionalAreaCatalogItemResponse?> GetFunctionalAreaResponseByIdAsync(Guid functionalAreaId, CancellationToken cancellationToken);

    Task<bool> HasCompaniesUsingCompanyTypeAsync(long companyTypeCatalogItemId, CancellationToken cancellationToken);

    Task<bool> HasOrgUnitsUsingOrgUnitTypeAsync(long orgUnitTypeCatalogItemId, CancellationToken cancellationToken);

    Task<bool> HasPositionCategoryClassificationsUsingOrgUnitTypeAsync(long orgUnitTypeCatalogItemId, CancellationToken cancellationToken);

    Task<bool> HasOrgUnitsUsingFunctionalAreaAsync(long functionalAreaCatalogItemId, CancellationToken cancellationToken);

    Task<CatalogReferenceLookup?> GetActiveCompanyTypeLookupAsync(
        Guid ownerUserPublicId,
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
