using CLARIHR.Domain.Locations;

namespace CLARIHR.Application.Abstractions.Locations;

public interface ILocationHierarchyRepository
{
    void AddConfig(LocationHierarchyConfig config);

    void AddLevel(LocationLevel level);

    Task<LocationHierarchyConfig?> GetConfigAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<LocationHierarchyConfig?> GetConfigByIdAsync(Guid configId, CancellationToken cancellationToken);

    Task<bool> ConfigExistsOutsideTenantAsync(Guid configId, CancellationToken cancellationToken);

    Task<IReadOnlyList<LocationLevel>> GetLevelsAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<LocationLevel?> GetLevelByIdAsync(Guid levelId, CancellationToken cancellationToken);

    Task<bool> LevelExistsOutsideTenantAsync(Guid levelId, CancellationToken cancellationToken);

    Task<bool> LevelOrderExistsAsync(Guid tenantId, int levelOrder, long? excludingLevelId, CancellationToken cancellationToken);

    Task<int> CountActiveLevelsAsync(Guid tenantId, long? excludingLevelId, CancellationToken cancellationToken);

    Task<bool> HasAnyActiveWorkCenterLevelAsync(Guid tenantId, long? excludingLevelId, CancellationToken cancellationToken);

    Task<int?> GetHighestActiveLevelOrderAsync(Guid tenantId, long? excludingLevelId, CancellationToken cancellationToken);
}
