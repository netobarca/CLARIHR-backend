using CLARIHR.Application.Abstractions.Locations;
using CLARIHR.Domain.Locations;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Locations;

internal sealed class LocationHierarchyRepository(ApplicationDbContext dbContext) : ILocationHierarchyRepository
{
    public void AddConfig(LocationHierarchyConfig config) => dbContext.LocationHierarchyConfigs.Add(config);

    public void AddLevel(LocationLevel level) => dbContext.LocationLevels.Add(level);

    public Task<LocationHierarchyConfig?> GetConfigAsync(Guid tenantId, CancellationToken cancellationToken) =>
        dbContext.LocationHierarchyConfigs.SingleOrDefaultAsync(config => config.TenantId == tenantId, cancellationToken);

    public Task<LocationHierarchyConfig?> GetConfigByIdAsync(Guid configId, CancellationToken cancellationToken) =>
        dbContext.LocationHierarchyConfigs.SingleOrDefaultAsync(config => config.PublicId == configId, cancellationToken);

    public Task<bool> ConfigExistsOutsideTenantAsync(Guid configId, CancellationToken cancellationToken) =>
        dbContext.LocationHierarchyConfigs
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(config => config.PublicId == configId, cancellationToken);

    public async Task<IReadOnlyList<LocationLevel>> GetLevelsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await dbContext.LocationLevels
            .AsNoTracking()
            .Where(level => level.TenantId == tenantId)
            .OrderBy(level => level.LevelOrder)
            .ToListAsync(cancellationToken);

    public Task<LocationLevel?> GetLevelByIdAsync(Guid levelId, CancellationToken cancellationToken) =>
        dbContext.LocationLevels.SingleOrDefaultAsync(level => level.PublicId == levelId, cancellationToken);

    public Task<bool> LevelExistsOutsideTenantAsync(Guid levelId, CancellationToken cancellationToken) =>
        dbContext.LocationLevels
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(level => level.PublicId == levelId, cancellationToken);

    public Task<bool> LevelOrderExistsAsync(Guid tenantId, int levelOrder, long? excludingLevelId, CancellationToken cancellationToken) =>
        dbContext.LocationLevels.AnyAsync(
            level => level.TenantId == tenantId &&
                     level.LevelOrder == levelOrder &&
                     (!excludingLevelId.HasValue || level.Id != excludingLevelId.Value),
            cancellationToken);

    public Task<int> CountActiveLevelsAsync(Guid tenantId, long? excludingLevelId, CancellationToken cancellationToken) =>
        dbContext.LocationLevels.CountAsync(
            level => level.TenantId == tenantId &&
                     level.IsActive &&
                     (!excludingLevelId.HasValue || level.Id != excludingLevelId.Value),
            cancellationToken);

    public Task<bool> HasAnyActiveWorkCenterLevelAsync(Guid tenantId, long? excludingLevelId, CancellationToken cancellationToken) =>
        dbContext.LocationLevels.AnyAsync(
            level => level.TenantId == tenantId &&
                     level.IsActive &&
                     level.AllowsWorkCenters &&
                     (!excludingLevelId.HasValue || level.Id != excludingLevelId.Value),
            cancellationToken);

    public Task<int?> GetHighestActiveLevelOrderAsync(Guid tenantId, long? excludingLevelId, CancellationToken cancellationToken) =>
        dbContext.LocationLevels
            .Where(level => level.TenantId == tenantId &&
                            level.IsActive &&
                            (!excludingLevelId.HasValue || level.Id != excludingLevelId.Value))
            .OrderByDescending(level => level.LevelOrder)
            .Select(level => (int?)level.LevelOrder)
            .FirstOrDefaultAsync(cancellationToken);
}
