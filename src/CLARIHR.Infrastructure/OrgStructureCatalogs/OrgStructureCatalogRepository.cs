using CLARIHR.Application.Abstractions.OrgStructureCatalogs;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.OrgStructureCatalogs;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.OrgStructureCatalogs;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.OrgStructureCatalogs;

internal sealed class OrgStructureCatalogRepository(ApplicationDbContext dbContext) : IOrgStructureCatalogRepository
{
    public void AddCompanyType(CompanyTypeCatalogItem item) => dbContext.CompanyTypeCatalogItems.Add(item);

    public void AddOrgUnitType(OrgUnitTypeCatalogItem item) => dbContext.OrgUnitTypeCatalogItems.Add(item);

    public void AddFunctionalArea(FunctionalAreaCatalogItem item) => dbContext.FunctionalAreaCatalogItems.Add(item);

    public Task<CompanyTypeCatalogItem?> GetCompanyTypeByIdAsync(Guid companyTypeId, CancellationToken cancellationToken) =>
        dbContext.CompanyTypeCatalogItems.SingleOrDefaultAsync(item => item.PublicId == companyTypeId, cancellationToken);

    public Task<OrgUnitTypeCatalogItem?> GetOrgUnitTypeByIdAsync(Guid orgUnitTypeId, CancellationToken cancellationToken) =>
        dbContext.OrgUnitTypeCatalogItems.SingleOrDefaultAsync(item => item.PublicId == orgUnitTypeId, cancellationToken);

    public Task<FunctionalAreaCatalogItem?> GetFunctionalAreaByIdAsync(Guid functionalAreaId, CancellationToken cancellationToken) =>
        dbContext.FunctionalAreaCatalogItems.SingleOrDefaultAsync(item => item.PublicId == functionalAreaId, cancellationToken);

    public Task<bool> ExistsOrgUnitTypeOutsideTenantAsync(Guid orgUnitTypeId, CancellationToken cancellationToken) =>
        dbContext.OrgUnitTypeCatalogItems
            .IgnoreQueryFilters()
            .AnyAsync(item => item.PublicId == orgUnitTypeId, cancellationToken);

    public Task<bool> ExistsFunctionalAreaOutsideTenantAsync(Guid functionalAreaId, CancellationToken cancellationToken) =>
        dbContext.FunctionalAreaCatalogItems
            .IgnoreQueryFilters()
            .AnyAsync(item => item.PublicId == functionalAreaId, cancellationToken);

    public Task<bool> CompanyTypeCodeExistsAsync(
        Guid ownerUserPublicId,
        string normalizedCode,
        long? excludingId,
        CancellationToken cancellationToken) =>
        dbContext.CompanyTypeCatalogItems.AnyAsync(
            item => item.OwnerUserPublicId == ownerUserPublicId &&
                    item.NormalizedCode == normalizedCode &&
                    (!excludingId.HasValue || item.Id != excludingId.Value),
            cancellationToken);

    public Task<bool> OrgUnitTypeCodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        long? excludingId,
        CancellationToken cancellationToken) =>
        dbContext.OrgUnitTypeCatalogItems.AnyAsync(
            item => item.TenantId == tenantId &&
                    item.NormalizedCode == normalizedCode &&
                    (!excludingId.HasValue || item.Id != excludingId.Value),
            cancellationToken);

    public Task<bool> FunctionalAreaCodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        long? excludingId,
        CancellationToken cancellationToken) =>
        dbContext.FunctionalAreaCatalogItems.AnyAsync(
            item => item.TenantId == tenantId &&
                    item.NormalizedCode == normalizedCode &&
                    (!excludingId.HasValue || item.Id != excludingId.Value),
            cancellationToken);

    public async Task<PagedResponse<CompanyTypeCatalogItemResponse>> SearchCompanyTypesAsync(
        Guid ownerUserPublicId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.CompanyTypeCatalogItems
            .AsNoTracking()
            .Where(item => item.OwnerUserPublicId == ownerUserPublicId);

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
            .Select(item => new CompanyTypeCatalogItemResponse(
                item.PublicId,
                item.Code,
                item.Name,
                item.Description,
                item.SortOrder,
                item.IsActive,
                item.ConcurrencyToken,
                item.CreatedUtc,
                item.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<CompanyTypeCatalogItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<PagedResponse<OrgUnitTypeCatalogItemResponse>> SearchOrgUnitTypesAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.OrgUnitTypeCatalogItems
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId);

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
            .Select(item => new OrgUnitTypeCatalogItemResponse(
                item.PublicId,
                item.Code,
                item.Name,
                item.Description,
                item.SortOrder,
                item.IsActive,
                item.ConcurrencyToken,
                item.CreatedUtc,
                item.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<OrgUnitTypeCatalogItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<PagedResponse<FunctionalAreaCatalogItemResponse>> SearchFunctionalAreasAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.FunctionalAreaCatalogItems
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId);

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
            .Select(item => new FunctionalAreaCatalogItemResponse(
                item.PublicId,
                item.Code,
                item.Name,
                item.Description,
                item.SortOrder,
                item.IsActive,
                item.ConcurrencyToken,
                item.CreatedUtc,
                item.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<FunctionalAreaCatalogItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<CompanyTypeCatalogItemResponse?> GetCompanyTypeResponseByIdAsync(Guid companyTypeId, CancellationToken cancellationToken) =>
        dbContext.CompanyTypeCatalogItems
            .AsNoTracking()
            .Where(item => item.PublicId == companyTypeId)
            .Select(item => new CompanyTypeCatalogItemResponse(
                item.PublicId,
                item.Code,
                item.Name,
                item.Description,
                item.SortOrder,
                item.IsActive,
                item.ConcurrencyToken,
                item.CreatedUtc,
                item.ModifiedUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<OrgUnitTypeCatalogItemResponse?> GetOrgUnitTypeResponseByIdAsync(Guid orgUnitTypeId, CancellationToken cancellationToken) =>
        dbContext.OrgUnitTypeCatalogItems
            .AsNoTracking()
            .Where(item => item.PublicId == orgUnitTypeId)
            .Select(item => new OrgUnitTypeCatalogItemResponse(
                item.PublicId,
                item.Code,
                item.Name,
                item.Description,
                item.SortOrder,
                item.IsActive,
                item.ConcurrencyToken,
                item.CreatedUtc,
                item.ModifiedUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<FunctionalAreaCatalogItemResponse?> GetFunctionalAreaResponseByIdAsync(Guid functionalAreaId, CancellationToken cancellationToken) =>
        dbContext.FunctionalAreaCatalogItems
            .AsNoTracking()
            .Where(item => item.PublicId == functionalAreaId)
            .Select(item => new FunctionalAreaCatalogItemResponse(
                item.PublicId,
                item.Code,
                item.Name,
                item.Description,
                item.SortOrder,
                item.IsActive,
                item.ConcurrencyToken,
                item.CreatedUtc,
                item.ModifiedUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> HasCompaniesUsingCompanyTypeAsync(long companyTypeCatalogItemId, CancellationToken cancellationToken) =>
        dbContext.Companies.AnyAsync(
            company => company.CompanyTypeCatalogItemId == companyTypeCatalogItemId && company.Status == CompanyStatus.Active,
            cancellationToken);

    public Task<bool> HasOrgUnitsUsingOrgUnitTypeAsync(long orgUnitTypeCatalogItemId, CancellationToken cancellationToken) =>
        dbContext.OrgUnits.AnyAsync(
            orgUnit => orgUnit.OrgUnitTypeCatalogItemId == orgUnitTypeCatalogItemId && orgUnit.IsActive,
            cancellationToken);

    public Task<bool> HasPositionCategoryClassificationsUsingOrgUnitTypeAsync(long orgUnitTypeCatalogItemId, CancellationToken cancellationToken) =>
        dbContext.PositionCategoryClassifications.AnyAsync(
            classification => classification.OrgUnitTypeCatalogItemId == orgUnitTypeCatalogItemId && classification.IsActive,
            cancellationToken);

    public Task<bool> HasOrgUnitsUsingFunctionalAreaAsync(long functionalAreaCatalogItemId, CancellationToken cancellationToken) =>
        dbContext.OrgUnits.AnyAsync(
            orgUnit => orgUnit.FunctionalAreaCatalogItemId == functionalAreaCatalogItemId && orgUnit.IsActive,
            cancellationToken);

    public Task<CatalogReferenceLookup?> GetActiveCompanyTypeLookupAsync(
        Guid ownerUserPublicId,
        Guid companyTypeId,
        CancellationToken cancellationToken) =>
        dbContext.CompanyTypeCatalogItems
            .AsNoTracking()
            .Where(item => item.OwnerUserPublicId == ownerUserPublicId && item.PublicId == companyTypeId && item.IsActive)
            .Select(item => new CatalogReferenceLookup(item.Id, item.PublicId, item.Code, item.Name, item.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<CatalogReferenceLookup?> GetActiveOrgUnitTypeLookupAsync(
        Guid tenantId,
        Guid orgUnitTypeId,
        CancellationToken cancellationToken) =>
        dbContext.OrgUnitTypeCatalogItems
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PublicId == orgUnitTypeId && item.IsActive)
            .Select(item => new CatalogReferenceLookup(item.Id, item.PublicId, item.Code, item.Name, item.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<CatalogReferenceLookup?> GetActiveFunctionalAreaLookupAsync(
        Guid tenantId,
        Guid functionalAreaId,
        CancellationToken cancellationToken) =>
        dbContext.FunctionalAreaCatalogItems
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PublicId == functionalAreaId && item.IsActive)
            .Select(item => new CatalogReferenceLookup(item.Id, item.PublicId, item.Code, item.Name, item.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
}
