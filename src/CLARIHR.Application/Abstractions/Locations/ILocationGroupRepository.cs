using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Locations.Groups;
using CLARIHR.Domain.Locations;

namespace CLARIHR.Application.Abstractions.Locations;

public interface ILocationGroupRepository
{
    void Add(LocationGroup group);

    Task<LocationGroup?> GetByIdAsync(Guid groupId, CancellationToken cancellationToken);

    Task<LocationGroupResponse?> GetResponseByIdAsync(Guid groupId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid groupId, CancellationToken cancellationToken);

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

    // Direct children of a group (for lazy-loaded tree navigation). Unpaginated: a single node's direct
    // fan-out is bounded, mirroring the GetTreeAsync scale assumption (§LG7). Optional isActive filter.
    Task<IReadOnlyCollection<LocationGroupResponse>> GetChildrenAsync(Guid parentPublicId, bool? isActive, CancellationToken cancellationToken);

    // Root-to-node ancestor chain (breadcrumb), ordered root-first and inclusive of the node itself.
    Task<IReadOnlyList<LocationGroupPathNodeResponse>> GetAncestorPathAsync(Guid groupPublicId, CancellationToken cancellationToken);

    // Active/inactive reference counts (child groups + work centers) and the derived CanInactivate flag,
    // mirroring CostCenter/LegalRepresentative usage. Returns null when the group is not in the tenant.
    Task<LocationGroupUsageResponse?> GetUsageByIdAsync(Guid groupPublicId, CancellationToken cancellationToken);

    Task<bool> HasActiveChildrenAsync(long groupId, CancellationToken cancellationToken);

    Task<bool> HasActiveGroupsAtLevelAsync(Guid tenantId, int levelOrder, CancellationToken cancellationToken);

    Task<bool> IsDescendantAsync(long ancestorGroupId, long candidateDescendantId, CancellationToken cancellationToken);

    Task<bool> HasActiveWorkCentersAsync(long groupId, CancellationToken cancellationToken);
}
