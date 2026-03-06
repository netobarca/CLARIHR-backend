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
        OrgUnitType? unitType,
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
            where unit.TenantId == tenantId
            select new
            {
                Unit = unit,
                Parent = parent
            };

        if (isActive.HasValue)
        {
            query = query.Where(item => item.Unit.IsActive == isActive.Value);
        }

        if (unitType.HasValue)
        {
            query = query.Where(item => item.Unit.UnitType == unitType.Value);
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
                item.Unit.UnitType,
                item.Parent != null ? item.Parent.PublicId : null,
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
         where unit.PublicId == orgUnitId
         select new OrgUnitResponse(
             unit.PublicId,
             unit.Code,
             unit.Name,
             unit.UnitType,
             parent != null ? parent.PublicId : null,
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
         where unit.TenantId == tenantId
         select new OrgUnitHierarchyNodeData(
             unit.Id,
             unit.PublicId,
             unit.Code,
             unit.Name,
             unit.UnitType,
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

    public Task<bool> HasActiveChildrenAsync(long orgUnitId, CancellationToken cancellationToken) =>
        dbContext.OrgUnits.AnyAsync(unit => unit.ParentId == orgUnitId && unit.IsActive, cancellationToken);
}
