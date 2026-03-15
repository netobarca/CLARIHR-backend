using CLARIHR.Application.Abstractions.Locations;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Locations.Groups;
using CLARIHR.Domain.Locations;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Locations;

internal sealed class LocationGroupRepository(ApplicationDbContext dbContext) : ILocationGroupRepository
{
    public void Add(LocationGroup group) => dbContext.LocationGroups.Add(group);

    public void Remove(LocationGroup group) => dbContext.LocationGroups.Remove(group);

    public Task<LocationGroup?> GetByIdAsync(Guid groupId, CancellationToken cancellationToken) =>
        dbContext.LocationGroups.SingleOrDefaultAsync(group => group.PublicId == groupId, cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid groupId, CancellationToken cancellationToken) =>
        dbContext.LocationGroups
            .IgnoreQueryFilters()
            .AnyAsync(group => group.PublicId == groupId, cancellationToken);

    public Task<LocationGroup?> GetByIdIgnoreFiltersAsync(Guid groupId, CancellationToken cancellationToken) =>
        dbContext.LocationGroups
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(group => group.PublicId == groupId, cancellationToken);

    public Task<IReadOnlyList<LocationGroup>> GetGroupsForUpdateAsync(Guid tenantId, CancellationToken cancellationToken) =>
        dbContext.LocationGroups
            .Where(group => group.TenantId == tenantId)
            .OrderBy(group => group.LevelOrder)
            .ThenBy(group => group.Name)
            .ToListAsync(cancellationToken)
            .ContinueWith(static task => (IReadOnlyList<LocationGroup>)task.Result, cancellationToken);

    public Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingGroupId, CancellationToken cancellationToken) =>
        dbContext.LocationGroups.AnyAsync(
            group => group.TenantId == tenantId &&
                     group.NormalizedCode == normalizedCode &&
                     (!excludingGroupId.HasValue || group.Id != excludingGroupId.Value),
            cancellationToken);

    public async Task<IReadOnlyList<LocationGroupTreeNodeData>> GetTreeAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.LocationGroups
            .AsNoTracking()
            .Where(group => group.TenantId == tenantId)
            .OrderBy(group => group.LevelOrder)
            .ThenBy(group => group.Name)
            .Select(group => new LocationGroupTreeNodeData(
                group.PublicId,
                group.LevelOrder,
                group.Code,
                group.Name,
                dbContext.LocationGroups
                    .AsNoTracking()
                    .Where(parent => parent.Id == group.ParentId)
                    .Select(parent => (Guid?)parent.PublicId)
                    .FirstOrDefault(),
                group.Description,
                group.IsActive,
                group.IsDefault,
                group.ConcurrencyToken))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResponse<LocationGroupResponse>> SearchAsync(
        Guid tenantId,
        int? levelOrder,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.LocationGroups
            .AsNoTracking()
            .Where(group => group.TenantId == tenantId);

        if (levelOrder.HasValue)
        {
            query = query.Where(group => group.LevelOrder == levelOrder.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(group => group.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(group =>
                group.NormalizedCode.Contains(normalizedSearch) ||
                group.NormalizedName.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(group => group.LevelOrder)
            .ThenBy(group => group.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(group => new LocationGroupResponse(
                group.PublicId,
                group.LevelOrder,
                group.Code,
                group.Name,
                dbContext.LocationGroups
                    .AsNoTracking()
                    .Where(parent => parent.Id == group.ParentId)
                    .Select(parent => (Guid?)parent.PublicId)
                    .FirstOrDefault(),
                group.Description,
                group.IsActive,
                group.IsDefault,
                group.ConcurrencyToken,
                group.CreatedUtc,
                group.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<LocationGroupResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<bool> HasActiveChildrenAsync(long groupId, CancellationToken cancellationToken) =>
        dbContext.LocationGroups.AnyAsync(group => group.ParentId == groupId && group.IsActive, cancellationToken);

    public Task<bool> HasActiveGroupsAtLevelAsync(Guid tenantId, int levelOrder, CancellationToken cancellationToken) =>
        dbContext.LocationGroups.AnyAsync(
            group => group.TenantId == tenantId && group.LevelOrder == levelOrder && group.IsActive,
            cancellationToken);

    public async Task<bool> IsDescendantAsync(long ancestorGroupId, long candidateDescendantId, CancellationToken cancellationToken)
    {
        var frontier = new HashSet<long> { ancestorGroupId };

        while (frontier.Count > 0)
        {
            var children = await dbContext.LocationGroups
                .AsNoTracking()
                .Where(group => group.ParentId.HasValue && frontier.Contains(group.ParentId.Value))
                .Select(group => group.Id)
                .ToListAsync(cancellationToken);

            if (children.Contains(candidateDescendantId))
            {
                return true;
            }

            frontier = children.ToHashSet();
        }

        return false;
    }

    public Task<bool> HasActiveWorkCentersAsync(long groupId, CancellationToken cancellationToken) =>
        dbContext.WorkCenters.AnyAsync(workCenter => workCenter.LocationGroupId == groupId && workCenter.IsActive, cancellationToken);
}
