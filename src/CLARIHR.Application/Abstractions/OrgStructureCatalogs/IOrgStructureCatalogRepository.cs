using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.OrgStructureCatalogs;
using CLARIHR.Domain.OrgStructureCatalogs;

namespace CLARIHR.Application.Abstractions.OrgStructureCatalogs;

public interface IOrgStructureCatalogRepository
{
    void AddOrgUnitType(OrgUnitTypeCatalogItem item);

    void RemoveOrgUnitType(OrgUnitTypeCatalogItem item);

    void AddFunctionalArea(FunctionalAreaCatalogItem item);

    /// <summary>
    /// 00003 / B-04 — borrado duro del area funcional, condicionado por
    /// <see cref="GetFunctionalAreaUsageByIdAsync"/>.
    /// </summary>
    void RemoveFunctionalArea(FunctionalAreaCatalogItem item);

    /// <summary>
    /// Que referencia al area funcional, separado por origen. Incluye la preferencia del tablero, que
    /// apunta por CODIGO y no tiene clave foranea.
    /// </summary>
    Task<FunctionalAreaUsageResponse?> GetFunctionalAreaUsageByIdAsync(
        Guid functionalAreaId,
        CancellationToken cancellationToken);

    Task<OrgUnitTypeCatalogItem?> GetOrgUnitTypeByIdAsync(Guid orgUnitTypeId, CancellationToken cancellationToken);

    Task<FunctionalAreaCatalogItem?> GetFunctionalAreaByIdAsync(Guid functionalAreaId, CancellationToken cancellationToken);

    Task<bool> ExistsOrgUnitTypeOutsideTenantAsync(Guid orgUnitTypeId, CancellationToken cancellationToken);

    Task<OrgUnitTypeUsageResponse?> GetOrgUnitTypeUsageByIdAsync(Guid orgUnitTypeId, CancellationToken cancellationToken);

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

    Task<PagedResponse<OrgUnitTypeCatalogListItemResponse>> SearchOrgUnitTypesAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PagedResponse<FunctionalAreaCatalogListItemResponse>> SearchFunctionalAreasAsync(
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

    // OSC-002: set-based batch "which of these catalog public ids are in use" — replaces the per-item
    // GetXByIdAsync + dependency probes (~3 queries/item) in the includeAllowedActions list enrichment
    // with a single round trip. Returns the subset of the input public ids that are in use.
    Task<IReadOnlySet<Guid>> GetOrgUnitTypePublicIdsInUseAsync(IReadOnlyCollection<Guid> publicIds, CancellationToken cancellationToken);

    Task<IReadOnlySet<Guid>> GetFunctionalAreaPublicIdsInUseAsync(IReadOnlyCollection<Guid> publicIds, CancellationToken cancellationToken);

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
