using CLARIHR.Application.Features.IdentityAccess.Contracts;
using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Application.Common.Pagination;

namespace CLARIHR.Application.Abstractions.IdentityAccess;

public interface IIamAdministrationRepository
{
    void AddUser(IamUser user);

    void AddRole(IamRole role);

    void RemoveRole(IamRole role);

    void AddPermission(IamPermission permission);

    Task<bool> UserEmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<bool> RoleNameExistsAsync(string normalizedRoleName, CancellationToken cancellationToken);

    Task<bool> UserPublicIdExistsAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> RolePublicIdExistsAsync(Guid roleId, CancellationToken cancellationToken);

    Task<IamUser?> FindUserByPublicIdAsync(Guid userId, bool includeRoles, CancellationToken cancellationToken);

    Task<IamUser?> FindUserByTenantAndLinkedUserPublicIdAsync(
        Guid tenantId,
        Guid linkedUserPublicId,
        bool includeRoles,
        CancellationToken cancellationToken);

    Task<IamRole?> FindRoleByPublicIdAsync(Guid roleId, bool includePermissions, CancellationToken cancellationToken);

    Task<IamRole?> FindSystemRoleByTenantAndNormalizedNameAsync(
        Guid tenantId,
        string normalizedRoleName,
        bool includePermissions,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<IamRole>> GetRolesByPublicIdsAsync(
        IReadOnlyCollection<Guid> roleIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<IamUser>> GetUsersByPublicIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        bool includeRoles,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<IamUser>> GetUsersAssignedToRoleAsync(
        Guid roleId,
        bool includeRoles,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<IamUser>> GetActiveUsersAsync(
        bool includeRoles,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Guid>> GetActiveAdministratorUserIdsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<IamPermission>> GetPermissionsByNormalizedCodesAsync(
        IReadOnlyCollection<string> normalizedPermissionCodes,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<IamPermission>> GetPermissionsByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<IamPermission>> GetPermissionsByTenantAndNormalizedCodesAsync(
        Guid tenantId,
        IReadOnlyCollection<string> normalizedPermissionCodes,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<IamPermission>> GetPermissionsByPublicIdsAsync(
        IReadOnlyCollection<Guid> permissionIds,
        CancellationToken cancellationToken);

    Task<PagedResponse<IamUserSummaryResponse>> GetUsersAsync(
        int pageNumber,
        int pageSize,
        string? search,
        CancellationToken cancellationToken);

    Task<IamUserResponse?> GetUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<PagedResponse<IamRoleSummaryResponse>> GetRolesAsync(
        int pageNumber,
        int pageSize,
        string? search,
        CancellationToken cancellationToken);

    Task<IamRoleResponse?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
