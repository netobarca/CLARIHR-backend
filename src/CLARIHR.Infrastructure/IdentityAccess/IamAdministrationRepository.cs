using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.IdentityAccess.Contracts;
using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.IdentityAccess;

internal sealed class IamAdministrationRepository(ApplicationDbContext dbContext) : IIamAdministrationRepository
{
    public void AddUser(IamUser user) => dbContext.IamUsers.Add(user);

    public void AddRole(IamRole role) => dbContext.IamRoles.Add(role);

    public void RemoveRole(IamRole role) => dbContext.IamRoles.Remove(role);

    public void AddPermission(IamPermission permission) => dbContext.IamPermissions.Add(permission);

    public void AddPermissionAuditLog(RbacPermissionAuditLog auditLog) => dbContext.RbacPermissionAuditLogs.Add(auditLog);

    public Task<bool> UserEmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.IamUsers.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);

    public Task<bool> RoleNameExistsAsync(string normalizedRoleName, CancellationToken cancellationToken) =>
        dbContext.IamRoles.AnyAsync(role => role.NormalizedName == normalizedRoleName, cancellationToken);

    public Task<bool> PermissionCodeExistsAsync(string normalizedPermissionCode, CancellationToken cancellationToken) =>
        dbContext.IamPermissions.AnyAsync(permission => permission.NormalizedCode == normalizedPermissionCode, cancellationToken);

    public Task<bool> UserPublicIdExistsAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.IamUsers
            .IgnoreQueryFilters()
            .AnyAsync(user => user.PublicId == userId, cancellationToken);

    public Task<bool> RolePublicIdExistsAsync(Guid roleId, CancellationToken cancellationToken) =>
        dbContext.IamRoles
            .IgnoreQueryFilters()
            .AnyAsync(role => role.PublicId == roleId, cancellationToken);

    public Task<bool> PermissionPublicIdExistsAsync(Guid permissionId, CancellationToken cancellationToken) =>
        dbContext.IamPermissions
            .IgnoreQueryFilters()
            .AnyAsync(permission => permission.PublicId == permissionId, cancellationToken);

    public Task<IamUser?> FindUserByPublicIdAsync(Guid userId, bool includeRoles, CancellationToken cancellationToken)
    {
        IQueryable<IamUser> query = dbContext.IamUsers;
        if (includeRoles)
        {
            query = query
                .Include(user => user.RoleAssignments)
                .ThenInclude(assignment => assignment.Role)
                .ThenInclude(role => role.PermissionAssignments)
                .ThenInclude(assignment => assignment.Permission);
        }

        return query.SingleOrDefaultAsync(user => user.PublicId == userId, cancellationToken);
    }

    public Task<IamUser?> FindUserByTenantAndLinkedUserPublicIdAsync(
        Guid tenantId,
        Guid linkedUserPublicId,
        bool includeRoles,
        CancellationToken cancellationToken)
    {
        IQueryable<IamUser> query = dbContext.IamUsers
            .IgnoreQueryFilters()
            .Where(user => user.TenantId == tenantId);

        if (includeRoles)
        {
            query = query
                .Include(user => user.RoleAssignments)
                .ThenInclude(assignment => assignment.Role)
                .ThenInclude(role => role.PermissionAssignments)
                .ThenInclude(assignment => assignment.Permission);
        }

        return query.SingleOrDefaultAsync(user => user.LinkedUserPublicId == linkedUserPublicId, cancellationToken);
    }

    public Task<IamRole?> FindRoleByPublicIdAsync(Guid roleId, bool includePermissions, CancellationToken cancellationToken)
    {
        IQueryable<IamRole> query = dbContext.IamRoles;
        if (includePermissions)
        {
            query = query
                .Include(role => role.PermissionAssignments)
                .ThenInclude(assignment => assignment.Permission);
        }

        return query.SingleOrDefaultAsync(role => role.PublicId == roleId, cancellationToken);
    }

    public Task<IamPermission?> FindPermissionByPublicIdAsync(Guid permissionId, CancellationToken cancellationToken) =>
        dbContext.IamPermissions.SingleOrDefaultAsync(permission => permission.PublicId == permissionId, cancellationToken);

    public async Task<IReadOnlyList<IamRole>> GetRolesByPublicIdsAsync(
        IReadOnlyCollection<Guid> roleIds,
        CancellationToken cancellationToken)
    {
        return await dbContext.IamRoles
            .Where(role => roleIds.Contains(role.PublicId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IamUser>> GetUsersByPublicIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        bool includeRoles,
        CancellationToken cancellationToken)
    {
        IQueryable<IamUser> query = dbContext.IamUsers;
        if (includeRoles)
        {
            query = query
                .Include(user => user.RoleAssignments)
                .ThenInclude(assignment => assignment.Role)
                .ThenInclude(role => role.PermissionAssignments)
                .ThenInclude(assignment => assignment.Permission);
        }

        return await query
            .Where(user => userIds.Contains(user.PublicId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IamUser>> GetUsersAssignedToRoleAsync(
        Guid roleId,
        bool includeRoles,
        CancellationToken cancellationToken)
    {
        IQueryable<IamUser> query = dbContext.IamUsers;
        if (includeRoles)
        {
            query = query
                .Include(user => user.RoleAssignments)
                .ThenInclude(assignment => assignment.Role)
                .ThenInclude(role => role.PermissionAssignments)
                .ThenInclude(assignment => assignment.Permission);
        }

        return await query
            .Where(user => user.RoleAssignments.Any(assignment => assignment.Role.PublicId == roleId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IamUser>> GetActiveUsersAsync(
        bool includeRoles,
        CancellationToken cancellationToken)
    {
        IQueryable<IamUser> query = dbContext.IamUsers;
        if (includeRoles)
        {
            query = query
                .Include(user => user.RoleAssignments)
                .ThenInclude(assignment => assignment.Role)
                .ThenInclude(role => role.PermissionAssignments)
                .ThenInclude(assignment => assignment.Permission);
        }

        return await query
            .Where(user => user.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Guid>> GetActiveAdministratorUserIdsAsync(CancellationToken cancellationToken)
    {
        var activeUsers = await dbContext.IamUsers
            .AsNoTracking()
            .Where(user => user.IsActive)
            .Include(user => user.RoleAssignments)
            .ThenInclude(assignment => assignment.Role)
            .ThenInclude(role => role.PermissionAssignments)
            .ThenInclude(assignment => assignment.Permission)
            .ToListAsync(cancellationToken);

        return activeUsers
            .Where(static user => RbacAuthorizationEvaluator.IsRbacSecurityAdministrator(
                user.RoleAssignments.SelectMany(roleAssignment =>
                    roleAssignment.Role.PermissionAssignments.Select(permissionAssignment => permissionAssignment.Permission.NormalizedCode))))
            .Select(static user => user.PublicId)
            .Distinct()
            .ToArray();
    }

    public async Task<IReadOnlyList<IamPermission>> GetPermissionsByNormalizedCodesAsync(
        IReadOnlyCollection<string> normalizedPermissionCodes,
        CancellationToken cancellationToken)
    {
        return await dbContext.IamPermissions
            .Where(permission => normalizedPermissionCodes.Contains(permission.NormalizedCode))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IamPermission>> GetPermissionsByPublicIdsAsync(
        IReadOnlyCollection<Guid> permissionIds,
        CancellationToken cancellationToken)
    {
        return await dbContext.IamPermissions
            .Where(permission => permissionIds.Contains(permission.PublicId))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResponse<IamUserSummaryResponse>> GetUsersAsync(
        int pageNumber,
        int pageSize,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = dbContext.IamUsers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = Normalize(search);
            query = query.Where(user =>
                user.NormalizedEmail.Contains(normalizedSearch) ||
                user.FirstName.ToUpper().Contains(normalizedSearch) ||
                user.LastName.ToUpper().Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(user => user.LastName)
            .ThenBy(user => user.FirstName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new IamUserSummaryResponse(
                user.PublicId,
                user.Email,
                user.FirstName,
                user.LastName,
                user.IsActive,
                user.RoleAssignments.Count))
            .ToListAsync(cancellationToken);

        return new PagedResponse<IamUserSummaryResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<IamUserResponse?> GetUserAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.IamUsers
            .AsNoTracking()
            .Where(user => user.PublicId == userId)
            .Select(user => new IamUserResponse(
                user.PublicId,
                user.Email,
                user.FirstName,
                user.LastName,
                user.IsActive,
                user.RoleAssignments
                    .OrderBy(assignment => assignment.Role.Name)
                    .Select(assignment => new IamUserRoleResponse(
                        assignment.Role.PublicId,
                        assignment.Role.Name,
                        assignment.Role.Description,
                        assignment.Role.IsSystemRole,
                        assignment.Role.PermissionAssignments
                            .OrderBy(pa => pa.Permission.Code)
                            .Select(pa => new IamPermissionReferenceResponse(
                                pa.Permission.PublicId,
                                pa.Permission.Code,
                                pa.Permission.Name,
                                pa.Permission.Description,
                                pa.Permission.Module,
                                pa.Permission.Screen,
                                pa.Permission.Kind,
                                pa.Permission.Action,
                                pa.Permission.FieldName,
                                pa.Permission.FieldAccess))
                            .ToArray()))
                    .ToArray()))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResponse<IamRoleSummaryResponse>> GetRolesAsync(
        int pageNumber,
        int pageSize,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = dbContext.IamRoles.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = Normalize(search);
            query = query.Where(role =>
                role.NormalizedName.Contains(normalizedSearch) ||
                (role.Description != null && role.Description.ToUpper().Contains(normalizedSearch)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(role => role.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(role => new IamRoleSummaryResponse(
                role.PublicId,
                role.Name,
                role.Description,
                role.IsSystemRole,
                role.PermissionAssignments.Count,
                role.UserAssignments.Count))
            .ToListAsync(cancellationToken);

        return new PagedResponse<IamRoleSummaryResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<IamRoleResponse?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken) =>
        dbContext.IamRoles
            .AsNoTracking()
            .Where(role => role.PublicId == roleId)
            .Select(role => new IamRoleResponse(
                role.PublicId,
                role.Name,
                role.Description,
                role.IsSystemRole,
                role.UserAssignments.Count,
                role.PermissionAssignments
                    .OrderBy(assignment => assignment.Permission.Code)
                    .Select(assignment => new IamPermissionReferenceResponse(
                        assignment.Permission.PublicId,
                        assignment.Permission.Code,
                        assignment.Permission.Name,
                        assignment.Permission.Description,
                        assignment.Permission.Module,
                        assignment.Permission.Screen,
                        assignment.Permission.Kind,
                        assignment.Permission.Action,
                        assignment.Permission.FieldName,
                        assignment.Permission.FieldAccess))
                    .ToArray()))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResponse<IamPermissionSummaryResponse>> GetPermissionsAsync(
        int pageNumber,
        int pageSize,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = dbContext.IamPermissions.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = Normalize(search);
            query = query.Where(permission =>
                permission.NormalizedCode.Contains(normalizedSearch) ||
                permission.NormalizedModule.Contains(normalizedSearch) ||
                permission.NormalizedScreen.Contains(normalizedSearch) ||
                permission.Name.ToUpper().Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(permission => permission.Module)
            .ThenBy(permission => permission.Screen)
            .ThenBy(permission => permission.Code)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(permission => new IamPermissionSummaryResponse(
                permission.PublicId,
                permission.Code,
                permission.Name,
                permission.Description,
                permission.Module,
                permission.Screen,
                permission.Kind,
                permission.Action,
                permission.FieldName,
                permission.FieldAccess))
            .ToListAsync(cancellationToken);

        return new PagedResponse<IamPermissionSummaryResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<IamPermissionResponse?> GetPermissionAsync(Guid permissionId, CancellationToken cancellationToken) =>
        dbContext.IamPermissions
            .AsNoTracking()
            .Where(permission => permission.PublicId == permissionId)
            .Select(permission => new IamPermissionResponse(
                permission.PublicId,
                permission.Code,
                permission.Name,
                permission.Description,
                permission.Module,
                permission.Screen,
                permission.Kind,
                permission.Action,
                permission.FieldName,
                permission.FieldAccess))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<RbacResource>> GetActiveRbacResourcesAsync(CancellationToken cancellationToken)
    {
        return await dbContext.RbacResources
            .AsNoTracking()
            .Where(resource => resource.IsActive)
            .OrderBy(resource => resource.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResponse<RbacPermissionAuditLog>> GetPermissionAuditLogsAsync(
        Guid? roleId,
        string? normalizedResourceKey,
        DateTime? fromUtc,
        DateTime? toUtc,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.RbacPermissionAuditLogs.AsNoTracking();

        if (roleId.HasValue)
        {
            query = query.Where(log => log.RolePublicId == roleId.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedResourceKey))
        {
            query = query.Where(log => log.NormalizedResourceKey == normalizedResourceKey);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(log => log.ChangedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(log => log.ChangedAtUtc <= toUtc.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(log => log.ChangedAtUtc)
            .ThenByDescending(log => log.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<RbacPermissionAuditLog>(items, pageNumber, pageSize, totalCount);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
