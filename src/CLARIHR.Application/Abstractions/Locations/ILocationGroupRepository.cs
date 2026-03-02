using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Locations.Groups;
using CLARIHR.Domain.Locations;

namespace CLARIHR.Application.Abstractions.Locations;

public interface ILocationGroupRepository
{
    void Add(LocationGroup group);

    Task<LocationGroup?> GetByIdAsync(Guid groupId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid groupId, CancellationToken cancellationToken);

    Task<LocationGroup?> GetByIdIgnoreFiltersAsync(Guid groupId, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingGroupId, CancellationToken cancellationToken);

    Task<IReadOnlyList<LocationGroupTreeNodeData>> GetTreeAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<PagedResponse<LocationGroupResponse>> SearchAsync(
        Guid tenantId,
        int? levelOrder,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<bool> HasActiveChildrenAsync(long groupId, CancellationToken cancellationToken);

    Task<bool> HasActiveGroupsAtLevelAsync(Guid tenantId, int levelOrder, CancellationToken cancellationToken);

    Task<bool> IsDescendantAsync(long ancestorGroupId, long candidateDescendantId, CancellationToken cancellationToken);

    Task<bool> HasActiveWorkCentersAsync(long groupId, CancellationToken cancellationToken);
}
