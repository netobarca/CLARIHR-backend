using CLARIHR.Application.Abstractions.OrgUnits;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.OrgUnits;
using CLARIHR.Domain.OrgUnits;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.OrgUnits;

internal sealed class OrgUnitRepository(ApplicationDbContext dbContext) : IOrgUnitRepository
{
    public void Add(OrgUnit orgUnit) => dbContext.OrgUnits.Add(orgUnit);

    public Task<OrgUnit?> GetByIdAsync(Guid orgUnitId, CancellationToken cancellationToken) =>
        dbContext.OrgUnits.SingleOrDefaultAsync(unit => unit.PublicId == orgUnitId, cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid orgUnitId, CancellationToken cancellationToken) =>
        dbContext.OrgUnits
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(unit => unit.PublicId == orgUnitId, cancellationToken);

    public Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingOrgUnitId, CancellationToken cancellationToken) =>
        dbContext.OrgUnits.AnyAsync(
            unit => unit.TenantId == tenantId &&
                    unit.NormalizedCode == normalizedCode &&
                    (!excludingOrgUnitId.HasValue || unit.Id != excludingOrgUnitId.Value),
            cancellationToken);

    public async Task<PagedResponse<OrgUnitResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        Guid? orgUnitTypeId,
        Guid? functionalAreaId,
        Guid? parentId,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query =
            from unit in dbContext.OrgUnits.AsNoTracking()
            join parent in dbContext.OrgUnits.AsNoTracking() on unit.ParentId equals parent.Id into parentGroup
            from parent in parentGroup.DefaultIfEmpty()
            join unitType in dbContext.OrgUnitTypeCatalogItems.AsNoTracking() on unit.OrgUnitTypeCatalogItemId equals unitType.Id
            join functionalArea in dbContext.FunctionalAreaCatalogItems.AsNoTracking() on unit.FunctionalAreaCatalogItemId equals functionalArea.Id into functionalAreaGroup
            from functionalArea in functionalAreaGroup.DefaultIfEmpty()
            where unit.TenantId == tenantId
            select new
            {
                Unit = unit,
                Parent = parent,
                UnitType = unitType,
                FunctionalArea = functionalArea
            };

        if (isActive.HasValue)
        {
            query = query.Where(item => item.Unit.IsActive == isActive.Value);
        }

        if (orgUnitTypeId.HasValue)
        {
            query = query.Where(item => item.UnitType.PublicId == orgUnitTypeId.Value);
        }

        if (functionalAreaId.HasValue)
        {
            query = query.Where(item => item.FunctionalArea != null && item.FunctionalArea.PublicId == functionalAreaId.Value);
        }

        if (parentId.HasValue)
        {
            query = query.Where(item => item.Parent != null && item.Parent.PublicId == parentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(item =>
                item.Unit.NormalizedCode.Contains(normalizedSearch) ||
                item.Unit.NormalizedName.Contains(normalizedSearch) ||
                item.UnitType.NormalizedCode.Contains(normalizedSearch) ||
                item.UnitType.NormalizedName.Contains(normalizedSearch) ||
                (item.FunctionalArea != null &&
                 (item.FunctionalArea.NormalizedCode.Contains(normalizedSearch) ||
                  item.FunctionalArea.NormalizedName.Contains(normalizedSearch))) ||
                (item.Parent != null && item.Parent.NormalizedName.Contains(normalizedSearch)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.Unit.SortOrder ?? int.MaxValue)
            .ThenBy(item => item.Unit.Name)
            .ThenBy(item => item.Unit.Code)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new OrgUnitResponse(
                item.Unit.PublicId,
                item.Unit.Code,
                item.Unit.Name,
                new OrgUnitCatalogReferenceResponse(item.UnitType.PublicId, item.UnitType.Code, item.UnitType.Name),
                item.FunctionalArea != null
                    ? new OrgUnitCatalogReferenceResponse(item.FunctionalArea.PublicId, item.FunctionalArea.Code, item.FunctionalArea.Name)
                    : null,
                item.Parent != null
                    ? new OrgUnitCatalogReferenceResponse(item.Parent.PublicId, item.Parent.Code, item.Parent.Name)
                    : null,
                item.Unit.SortOrder,
                item.Unit.Description,
                item.Unit.CostCenterCode,
                item.Unit.ManagerEmployeeId,
                item.Unit.IsActive,
                item.Unit.ConcurrencyToken,
                item.Unit.CreatedUtc,
                item.Unit.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<OrgUnitResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<OrgUnitResponse?> GetResponseByIdAsync(Guid orgUnitId, CancellationToken cancellationToken) =>
        (from unit in dbContext.OrgUnits.AsNoTracking()
         join parent in dbContext.OrgUnits.AsNoTracking() on unit.ParentId equals parent.Id into parentGroup
         from parent in parentGroup.DefaultIfEmpty()
         join unitType in dbContext.OrgUnitTypeCatalogItems.AsNoTracking() on unit.OrgUnitTypeCatalogItemId equals unitType.Id
         join functionalArea in dbContext.FunctionalAreaCatalogItems.AsNoTracking() on unit.FunctionalAreaCatalogItemId equals functionalArea.Id into functionalAreaGroup
         from functionalArea in functionalAreaGroup.DefaultIfEmpty()
         where unit.PublicId == orgUnitId
         select new OrgUnitResponse(
             unit.PublicId,
             unit.Code,
             unit.Name,
             new OrgUnitCatalogReferenceResponse(unitType.PublicId, unitType.Code, unitType.Name),
             functionalArea != null
                 ? new OrgUnitCatalogReferenceResponse(functionalArea.PublicId, functionalArea.Code, functionalArea.Name)
                 : null,
             parent != null
                 ? new OrgUnitCatalogReferenceResponse(parent.PublicId, parent.Code, parent.Name)
                 : null,
             unit.SortOrder,
             unit.Description,
             unit.CostCenterCode,
             unit.ManagerEmployeeId,
             unit.IsActive,
             unit.ConcurrencyToken,
             unit.CreatedUtc,
             unit.ModifiedUtc))
        .SingleOrDefaultAsync(cancellationToken);

    public Task<IReadOnlyList<OrgUnitHierarchyNodeData>> GetHierarchyAsync(Guid tenantId, CancellationToken cancellationToken) =>
        (from unit in dbContext.OrgUnits.AsNoTracking()
         join parent in dbContext.OrgUnits.AsNoTracking() on unit.ParentId equals parent.Id into parentGroup
         from parent in parentGroup.DefaultIfEmpty()
         join unitType in dbContext.OrgUnitTypeCatalogItems.AsNoTracking() on unit.OrgUnitTypeCatalogItemId equals unitType.Id
         join functionalArea in dbContext.FunctionalAreaCatalogItems.AsNoTracking() on unit.FunctionalAreaCatalogItemId equals functionalArea.Id into functionalAreaGroup
         from functionalArea in functionalAreaGroup.DefaultIfEmpty()
         where unit.TenantId == tenantId
         select new OrgUnitHierarchyNodeData(
             unit.Id,
             unit.PublicId,
             unit.Code,
             unit.Name,
             unit.OrgUnitTypeCatalogItemId,
             unitType.PublicId,
             unitType.Code,
             unitType.Name,
             unit.FunctionalAreaCatalogItemId,
             functionalArea != null ? functionalArea.PublicId : null,
             functionalArea != null ? functionalArea.Code : null,
             functionalArea != null ? functionalArea.Name : null,
             unit.ParentId,
             parent != null ? parent.PublicId : null,
             unit.SortOrder,
             unit.Description,
             unit.CostCenterCode,
             unit.ManagerEmployeeId,
             unit.IsActive,
             unit.ConcurrencyToken,
             unit.CreatedUtc,
             unit.ModifiedUtc))
        .ToListAsync(cancellationToken)
        .ContinueWith(static task => (IReadOnlyList<OrgUnitHierarchyNodeData>)task.Result, cancellationToken);

    public async Task<IReadOnlyCollection<OrgUnitExportRow>> GetExportRowsAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        Guid? orgUnitTypeId,
        Guid? functionalAreaId,
        Guid? parentId,
        int? maxRows,
        CancellationToken cancellationToken)
    {
        var hierarchy = await GetHierarchyAsync(tenantId, cancellationToken);
        var filtered = hierarchy.AsEnumerable();

        if (isActive.HasValue)
        {
            filtered = filtered.Where(node => node.IsActive == isActive.Value);
        }

        if (orgUnitTypeId.HasValue)
        {
            filtered = filtered.Where(node => node.OrgUnitTypeId == orgUnitTypeId.Value);
        }

        if (functionalAreaId.HasValue)
        {
            filtered = filtered.Where(node => node.FunctionalAreaId == functionalAreaId.Value);
        }

        if (parentId.HasValue)
        {
            filtered = filtered.Where(node => node.ParentId == parentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            var byId = hierarchy.ToDictionary(node => node.Id);
            filtered = filtered.Where(node =>
                node.Code.ToUpperInvariant().Contains(normalizedSearch) ||
                node.Name.ToUpperInvariant().Contains(normalizedSearch) ||
                (node.ParentId.HasValue &&
                 byId.TryGetValue(node.ParentId.Value, out var parentNode) &&
                 parentNode.Name.ToUpperInvariant().Contains(normalizedSearch)));
        }

        var nodesById = hierarchy.ToDictionary(node => node.Id);
        return filtered
            .OrderBy(node => node.SortOrder ?? int.MaxValue)
            .ThenBy(node => node.Name)
            .ThenBy(node => node.Code)
            .Take(maxRows ?? int.MaxValue)
            .Select(node =>
            {
                var parent = node.ParentId.HasValue && nodesById.TryGetValue(node.ParentId.Value, out var parentNode)
                    ? parentNode
                    : null;

                return new OrgUnitExportRow(
                    node.Id,
                    node.Code,
                    node.Name,
                    node.OrgUnitTypeId,
                    node.OrgUnitTypeCode,
                    node.OrgUnitTypeName,
                    node.FunctionalAreaId,
                    node.FunctionalAreaCode,
                    node.FunctionalAreaName,
                    parent?.Code,
                    parent?.Name,
                    node.SortOrder,
                    node.Description,
                    node.CostCenterCode,
                    node.ManagerEmployeeId,
                    node.IsActive,
                    node.CreatedAtUtc,
                    node.ModifiedAtUtc);
            })
            .ToArray();
    }

    public Task<bool> HasActiveChildrenAsync(long orgUnitId, CancellationToken cancellationToken) =>
        dbContext.OrgUnits.AnyAsync(unit => unit.ParentId == orgUnitId && unit.IsActive, cancellationToken);
}
