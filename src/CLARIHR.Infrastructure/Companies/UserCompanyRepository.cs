using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CompanyUsers;
using CLARIHR.Application.Features.CompanyUsers.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Companies;

internal sealed class UserCompanyRepository(ApplicationDbContext dbContext) : IUserCompanyRepository
{
    public void Add(UserCompanyMembership membership) => dbContext.UserCompanyMemberships.Add(membership);

    public Task<bool> ExistsInCompanyAsync(Guid companyPublicId, string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.UserCompanyMemberships
            .Join(
                dbContext.AuthUsers.Where(user => user.NormalizedEmail == normalizedEmail),
                membership => membership.UserId,
                user => user.Id,
                (membership, _) => membership)
            .Join(
                dbContext.Companies.Where(company => company.PublicId == companyPublicId),
                membership => membership.CompanyId,
                company => company.Id,
                (_, _) => true)
            .AnyAsync(cancellationToken);

    public Task<bool> HasAnyMembershipAsync(long userId, CancellationToken cancellationToken) =>
        dbContext.UserCompanyMemberships.AnyAsync(membership => membership.UserId == userId, cancellationToken);

    public Task<bool> HasPrimaryCompanyAsync(long userId, CancellationToken cancellationToken) =>
        dbContext.UserCompanyMemberships.AnyAsync(
            membership => membership.UserId == userId && membership.IsPrimary,
            cancellationToken);

    public Task<Guid?> GetPrimaryCompanyPublicIdAsync(long userId, CancellationToken cancellationToken) =>
        dbContext.UserCompanyMemberships
            .Where(membership => membership.UserId == userId && membership.IsPrimary)
            .Join(
                dbContext.Companies,
                membership => membership.CompanyId,
                company => company.Id,
                (_, company) => (Guid?)company.PublicId)
            .SingleOrDefaultAsync(cancellationToken);

    // AC-1: token issuance resolves the primary company ONLY when it is Active, so a session is never
    // minted against an archived primary company (login / refresh would otherwise keep emitting a tenant
    // claim for an archived company until the 14-day refresh TTL).
    public Task<Guid?> GetActivePrimaryCompanyPublicIdAsync(long userId, CancellationToken cancellationToken) =>
        dbContext.UserCompanyMemberships
            .Where(membership => membership.UserId == userId && membership.IsPrimary)
            .Join(
                dbContext.Companies.Where(company => company.Status == CompanyStatus.Active),
                membership => membership.CompanyId,
                company => company.Id,
                (_, company) => (Guid?)company.PublicId)
            .SingleOrDefaultAsync(cancellationToken);

    // AC-1: every member of a company (any membership status), used to revoke their refresh tokens when the
    // company is archived. Returns internal user ids; refresh tokens are keyed by internal user id.
    public async Task<IReadOnlyCollection<long>> GetMemberUserIdsAsync(Guid companyPublicId, CancellationToken cancellationToken) =>
        await dbContext.UserCompanyMemberships
            .AsNoTracking()
            .Join(
                dbContext.Companies.Where(company => company.PublicId == companyPublicId),
                membership => membership.CompanyId,
                company => company.Id,
                (membership, _) => membership.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

    public Task<UserCompanyMembership?> GetPrimaryMembershipAsync(long userId, CancellationToken cancellationToken) =>
        dbContext.UserCompanyMemberships.SingleOrDefaultAsync(
            membership => membership.UserId == userId && membership.IsPrimary,
            cancellationToken);

    public Task<UserCompanyMembership?> GetMembershipAsync(long userId, Guid companyPublicId, CancellationToken cancellationToken) =>
        dbContext.UserCompanyMemberships
            .Join(
                dbContext.Companies.Where(company => company.PublicId == companyPublicId),
                membership => membership.CompanyId,
                company => company.Id,
                (membership, _) => membership)
            .SingleOrDefaultAsync(membership => membership.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<UserCompanyMembership>> GetMembershipsAsync(
        IReadOnlyCollection<long> userIds,
        Guid companyPublicId,
        CancellationToken cancellationToken) =>
        await dbContext.UserCompanyMemberships
            .Join(
                dbContext.Companies.Where(company => company.PublicId == companyPublicId),
                membership => membership.CompanyId,
                company => company.Id,
                (membership, _) => membership)
            .Where(membership => userIds.Contains(membership.UserId))
            .ToListAsync(cancellationToken);

    public async Task<string?> GetRoleNormalizedNameAsync(long userId, Guid companyPublicId, CancellationToken cancellationToken)
    {
        var linkedUserPublicId = await dbContext.UserCompanyMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId)
            .Join(
                dbContext.Companies.AsNoTracking().Where(company => company.PublicId == companyPublicId),
                membership => membership.CompanyId,
                company => company.Id,
                (membership, _) => membership.UserId)
            .Join(
                dbContext.AuthUsers.AsNoTracking(),
                membershipUserId => membershipUserId,
                user => user.Id,
                (_, user) => (Guid?)user.PublicId)
            .SingleOrDefaultAsync(cancellationToken);

        if (linkedUserPublicId.HasValue)
        {
            var normalizedRoleNames = await dbContext.IamUsers
                // Intentional tenant filter bypass: applies explicit companyPublicId tenant filter before materializing IAM roles.
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(user =>
                    user.TenantId == companyPublicId &&
                    user.LinkedUserPublicId == linkedUserPublicId.Value)
                .SelectMany(user => user.RoleAssignments)
                .Select(assignment => assignment.Role.NormalizedName)
                .Distinct()
                .OrderBy(role => role)
                .ToListAsync(cancellationToken);

            if (normalizedRoleNames.Count > 0)
            {
                return normalizedRoleNames[0];
            }
        }

        return await dbContext.UserCompanyMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId)
            .Join(
                dbContext.Companies.AsNoTracking().Where(company => company.PublicId == companyPublicId),
                membership => membership.CompanyId,
                company => company.Id,
                (membership, _) => membership.RoleId)
            .Join(
                dbContext.IamRoles
                    // Intentional tenant filter bypass: resolves fallback membership role by internal RoleId without exposing cross-tenant rows.
                    .IgnoreQueryFilters()
                    .AsNoTracking(),
                roleId => roleId,
                role => role.Id,
                (_, role) => role.NormalizedName)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<UserCompanyMembership?> FindByUserPublicIdAsync(Guid companyPublicId, Guid userPublicId, CancellationToken cancellationToken) =>
        dbContext.UserCompanyMemberships
            .Join(
                dbContext.AuthUsers.Where(user => user.PublicId == userPublicId),
                membership => membership.UserId,
                user => user.Id,
                (membership, _) => membership)
            .Join(
                dbContext.Companies.Where(company => company.PublicId == companyPublicId),
                membership => membership.CompanyId,
                company => company.Id,
                (membership, _) => membership)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> UserExistsOutsideCompanyAsync(Guid companyPublicId, Guid userPublicId, CancellationToken cancellationToken) =>
        dbContext.UserCompanyMemberships
            .Join(
                dbContext.AuthUsers.Where(user => user.PublicId == userPublicId),
                membership => membership.UserId,
                user => user.Id,
                (membership, _) => membership)
            .Join(
                dbContext.Companies,
                membership => membership.CompanyId,
                company => company.Id,
                (membership, company) => company.PublicId)
            .AnyAsync(otherCompanyPublicId => otherCompanyPublicId != companyPublicId, cancellationToken);

    public Task<bool> HasActiveMembershipAsync(long userId, Guid companyPublicId, CancellationToken cancellationToken) =>
        dbContext.UserCompanyMemberships
            .Where(membership => membership.UserId == userId && membership.Status == UserCompanyStatus.Active)
            .Join(
                dbContext.Companies.Where(company => company.PublicId == companyPublicId),
                membership => membership.CompanyId,
                company => company.Id,
                (_, _) => true)
            .AnyAsync(cancellationToken);

    public async Task<bool> HasAnyActiveAdministratorAsync(Guid companyPublicId, CancellationToken cancellationToken)
    {
        var adminUsers = await GetActiveAdministratorUserIdsAsync(companyPublicId, cancellationToken);
        return adminUsers.Count > 0;
    }

    public async Task SetPrimaryCompanyAsync(long userId, Guid companyPublicId, CancellationToken cancellationToken)
    {
        var targetCompanyId = await dbContext.Companies
            .Where(company => company.PublicId == companyPublicId)
            .Select(company => (long?)company.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (!targetCompanyId.HasValue)
        {
            return;
        }

        _ = await dbContext.UserCompanyMemberships
            .Where(membership =>
                membership.UserId == userId &&
                membership.IsPrimary &&
                membership.CompanyId != targetCompanyId.Value)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(membership => membership.IsPrimary, false)
                    .SetProperty(membership => membership.ModifiedUtc, DateTime.UtcNow),
                cancellationToken);

        _ = await dbContext.UserCompanyMemberships
            .Where(membership =>
                membership.UserId == userId &&
                membership.CompanyId == targetCompanyId.Value &&
                !membership.IsPrimary)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(membership => membership.IsPrimary, true)
                    .SetProperty(membership => membership.ModifiedUtc, DateTime.UtcNow),
                cancellationToken);
    }

    public async Task<bool> IsLastActiveAdministratorAsync(Guid companyPublicId, Guid userPublicId, CancellationToken cancellationToken)
    {
        var adminUsers = await GetActiveAdministratorUserIdsAsync(companyPublicId, cancellationToken);
        return adminUsers.Count == 1 && adminUsers.First() == userPublicId;
    }

    public async Task<PagedResponse<CompanyUserSummaryResponse>> GetUsersAsync(
        Guid companyPublicId,
        int pageNumber,
        int pageSize,
        UserStatus? status,
        Guid? roleId,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = dbContext.UserCompanyMemberships
            .AsNoTracking()
            .Join(
                dbContext.Companies.AsNoTracking().Where(company => company.PublicId == companyPublicId),
                membership => membership.CompanyId,
                company => company.Id,
                (membership, _) => membership)
            .Join(
                dbContext.AuthUsers.AsNoTracking(),
                membership => membership.UserId,
                user => user.Id,
                (membership, user) => new
                {
                    user.PublicId,
                    user.Email,
                    user.FirstName,
                    user.LastName,
                    user.NormalizedEmail,
                    user.Status,
                    membership.RoleId
                });

        if (status.HasValue)
        {
            query = query.Where(item => item.Status == status.Value);
        }

        if (roleId.HasValue)
        {
            query = query.Where(item =>
                dbContext.IamUsers
                    // Intentional tenant filter bypass: applies explicit companyPublicId tenant filter before role matching.
                    .IgnoreQueryFilters()
                    .Any(iamUser =>
                        iamUser.TenantId == companyPublicId &&
                        iamUser.LinkedUserPublicId == item.PublicId &&
                        iamUser.RoleAssignments.Any(assignment => assignment.Role.PublicId == roleId.Value)) ||
                dbContext.IamRoles
                    // Intentional tenant filter bypass: resolves fallback membership role by internal RoleId without exposing cross-tenant rows.
                    .IgnoreQueryFilters()
                    .Any(role => role.Id == item.RoleId && role.PublicId == roleId.Value));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(item =>
                item.NormalizedEmail.Contains(normalizedSearch) ||
                item.FirstName.ToUpper().Contains(normalizedSearch) ||
                item.LastName.ToUpper().Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var pageItems = await query
            .OrderBy(item => item.LastName)
            .ThenBy(item => item.FirstName)
            .ThenBy(item => item.Email)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var roleLookup = await GetRoleLookupAsync(
            companyPublicId,
            pageItems.Select(static item => item.PublicId).ToArray(),
            cancellationToken);
        var fallbackRoles = await GetFallbackRoleLookupAsync(
            pageItems.Select(static item => item.RoleId).Distinct().ToArray(),
            cancellationToken);

        var items = pageItems
            .Select(item => new CompanyUserSummaryResponse(
                item.PublicId,
                item.Email,
                item.FirstName,
                item.LastName,
                ResolveRoles(roleLookup, fallbackRoles, item.PublicId, item.RoleId),
                item.Status))
            .ToArray();

        return new PagedResponse<CompanyUserSummaryResponse>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<CompanyUserResponse?> GetUserAsync(Guid companyPublicId, Guid userPublicId, CancellationToken cancellationToken)
    {
        var item = await dbContext.UserCompanyMemberships
            .AsNoTracking()
            .Join(
                dbContext.Companies.AsNoTracking().Where(company => company.PublicId == companyPublicId),
                membership => membership.CompanyId,
                company => company.Id,
                (membership, _) => membership)
            .Join(
                dbContext.AuthUsers.AsNoTracking(),
                membership => membership.UserId,
                user => user.Id,
                (membership, user) => new
                {
                    user.PublicId,
                    user.Email,
                    user.FirstName,
                    user.LastName,
                    user.Status,
                    membership.RoleId
                })
            .SingleOrDefaultAsync(item => item.PublicId == userPublicId, cancellationToken);

        if (item is null)
        {
            return null;
        }

        var roleLookup = await GetRoleLookupAsync(companyPublicId, [item.PublicId], cancellationToken);
        var fallbackRoles = await GetFallbackRoleLookupAsync([item.RoleId], cancellationToken);

        return new CompanyUserResponse(
            item.PublicId,
            item.Email,
            item.FirstName,
            item.LastName,
            ResolveRoles(roleLookup, fallbackRoles, item.PublicId, item.RoleId),
            item.Status);
    }

    public async Task<IReadOnlyCollection<Guid>> GetActiveAdministratorUserIdsAsync(
        Guid companyPublicId,
        CancellationToken cancellationToken)
    {
        var normalizedManageUsers = CompanyUserPermissionCodes.ManageUsers.ToUpperInvariant();
        var normalizedManageAdministration = IdentityPermissionCodes.ManageAdministration.ToUpperInvariant();
        var activeMemberUserIds = await dbContext.UserCompanyMemberships
            .AsNoTracking()
            .Where(membership => membership.Status == UserCompanyStatus.Active)
            .Join(
                dbContext.Companies.AsNoTracking().Where(company => company.PublicId == companyPublicId),
                membership => membership.CompanyId,
                company => company.Id,
                (membership, _) => membership)
            .Join(
                dbContext.AuthUsers.AsNoTracking().Where(user => user.Status == UserStatus.Active),
                membership => membership.UserId,
                user => user.Id,
                (membership, user) => new { membership.RoleId, user.PublicId })
            .ToListAsync(cancellationToken);

        var activeUserPublicIds = activeMemberUserIds
            .Select(static item => item.PublicId)
            .Distinct()
            .ToArray();

        if (activeUserPublicIds.Length == 0)
        {
            return Array.Empty<Guid>();
        }

        var iamAdminUsers = await dbContext.IamUsers
            // Intentional tenant filter bypass: applies explicit companyPublicId tenant filter before materializing IAM administrators.
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user =>
                user.TenantId == companyPublicId &&
                user.IsActive &&
                user.LinkedUserPublicId.HasValue &&
                activeUserPublicIds.Contains(user.LinkedUserPublicId.Value))
            .Where(user => user.RoleAssignments.Any(assignment =>
                assignment.Role.PermissionAssignments.Any(permissionAssignment =>
                    permissionAssignment.Permission.NormalizedCode == normalizedManageUsers ||
                    permissionAssignment.Permission.NormalizedCode == normalizedManageAdministration)))
            .Select(user => user.LinkedUserPublicId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (iamAdminUsers.Count > 0)
        {
            return iamAdminUsers;
        }

        // Fallback (no IAM-linked administrator resolved — e.g. incomplete IAM linkage / legacy data):
        // resolve which of the membership RoleIds are administrative in a SINGLE set-based query, then
        // match in memory — instead of probing dbContext.IamRoles once per active member, which was an
        // N+1 of *synchronous* .Any() calls inside an in-memory Where (one blocking round-trip per member).
        var membershipRoleIds = activeMemberUserIds
            .Select(static item => item.RoleId)
            .Distinct()
            .ToArray();

        var administrativeRoleIds = (await dbContext.IamRoles
                // Intentional tenant filter bypass: resolves fallback membership roles by internal RoleId set from tenant-scoped memberships, without exposing cross-tenant rows.
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(role =>
                    membershipRoleIds.Contains(role.Id) &&
                    role.PermissionAssignments.Any(assignment =>
                        assignment.Permission.NormalizedCode == normalizedManageUsers ||
                        assignment.Permission.NormalizedCode == normalizedManageAdministration))
                .Select(static role => role.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        return activeMemberUserIds
            .Where(item => administrativeRoleIds.Contains(item.RoleId))
            .Select(static item => item.PublicId)
            .Distinct()
            .ToArray();
    }

    private async Task<Dictionary<Guid, IReadOnlyCollection<CompanyUserRoleResponse>>> GetRoleLookupAsync(
        Guid companyPublicId,
        IReadOnlyCollection<Guid> userPublicIds,
        CancellationToken cancellationToken)
    {
        if (userPublicIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyCollection<CompanyUserRoleResponse>>();
        }

        var assignments = await dbContext.IamUsers
            // Intentional tenant filter bypass: applies explicit companyPublicId tenant filter before materializing role assignments.
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user =>
                user.TenantId == companyPublicId &&
                user.LinkedUserPublicId.HasValue &&
                userPublicIds.Contains(user.LinkedUserPublicId.Value))
            .SelectMany(user => user.RoleAssignments.Select(assignment => new
            {
                UserPublicId = user.LinkedUserPublicId!.Value,
                Role = new CompanyUserRoleResponse(
                    assignment.Role.PublicId,
                    assignment.Role.Name,
                    assignment.Role.Description,
                    assignment.Role.IsSystemRole)
            }))
            .ToListAsync(cancellationToken);

        return assignments
            .GroupBy(static item => item.UserPublicId)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyCollection<CompanyUserRoleResponse>)group
                    .Select(static item => item.Role)
                    .DistinctBy(static role => role.Id)
                    .OrderBy(static role => role.Name, StringComparer.Ordinal)
                    .ToArray());
    }

    private async Task<Dictionary<long, CompanyUserRoleResponse>> GetFallbackRoleLookupAsync(
        IReadOnlyCollection<long> roleIds,
        CancellationToken cancellationToken)
    {
        if (roleIds.Count == 0)
        {
            return new Dictionary<long, CompanyUserRoleResponse>();
        }

        return await dbContext.IamRoles
            // Intentional tenant filter bypass: resolves fallback role metadata by internal RoleId set from tenant-scoped memberships.
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(role => roleIds.Contains(role.Id))
            .ToDictionaryAsync(
                role => role.Id,
                role => new CompanyUserRoleResponse(
                    role.PublicId,
                    role.Name,
                    role.Description,
                    role.IsSystemRole),
                cancellationToken);
    }

    private static IReadOnlyCollection<CompanyUserRoleResponse> ResolveRoles(
        IReadOnlyDictionary<Guid, IReadOnlyCollection<CompanyUserRoleResponse>> roleLookup,
        IReadOnlyDictionary<long, CompanyUserRoleResponse> fallbackRoles,
        Guid userPublicId,
        long fallbackRoleId)
    {
        if (roleLookup.TryGetValue(userPublicId, out var roles) && roles.Count > 0)
        {
            return roles;
        }

        return fallbackRoles.TryGetValue(fallbackRoleId, out var fallbackRole)
            ? [fallbackRole]
            : Array.Empty<CompanyUserRoleResponse>();
    }
}
