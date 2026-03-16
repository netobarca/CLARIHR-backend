using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CompanyUsers;
using CLARIHR.Application.Features.CompanyUsers.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.IdentityAccess;
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

        // Clear the current primary first to avoid violating the partial unique index
        // while switching the flag to another membership in the same transaction.
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
        var normalizedManageUsers = CompanyUserPermissionCodes.ManageUsers.ToUpperInvariant();
        var normalizedManageAdministration = IdentityPermissionCodes.ManageAdministration.ToUpperInvariant();

        var adminUsers = await dbContext.UserCompanyMemberships
            .AsNoTracking()
            .Where(membership => membership.Status == UserCompanyStatus.Active)
            .Join(
                dbContext.Companies.Where(company => company.PublicId == companyPublicId),
                membership => membership.CompanyId,
                company => company.Id,
                (membership, _) => membership)
            .Join(
                dbContext.AuthUsers.Where(user => user.Status == UserStatus.Active),
                membership => membership.UserId,
                user => user.Id,
                (membership, user) => new { membership.RoleId, user.PublicId })
            .Join(
                dbContext.IamRoles.AsNoTracking(),
                item => item.RoleId,
                role => role.Id,
                (item, role) => new { item.PublicId, Role = role })
            .Where(item => item.Role.PermissionAssignments.Any(assignment =>
                assignment.Permission.NormalizedCode == normalizedManageUsers ||
                assignment.Permission.NormalizedCode == normalizedManageAdministration))
            .Select(item => item.PublicId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return adminUsers.Count == 1 && adminUsers[0] == userPublicId;
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
                (membership, user) => new { Membership = membership, User = user })
            .Join(
                dbContext.IamRoles.AsNoTracking(),
                item => item.Membership.RoleId,
                role => role.Id,
                (item, role) => new
                {
                    item.Membership,
                    item.User,
                    Role = role
                });

        if (status.HasValue)
        {
            query = query.Where(item => item.User.Status == status.Value);
        }

        if (roleId.HasValue)
        {
            query = query.Where(item => item.Role.PublicId == roleId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(item =>
                item.User.NormalizedEmail.Contains(normalizedSearch) ||
                item.User.FirstName.ToUpper().Contains(normalizedSearch) ||
                item.User.LastName.ToUpper().Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(item => item.User.LastName)
            .ThenBy(item => item.User.FirstName)
            .ThenBy(item => item.User.Email)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new CompanyUserSummaryResponse(
                item.User.PublicId,
                item.User.Email,
                item.User.FirstName,
                item.User.LastName,
                item.Role.PublicId,
                item.Role.Name,
                item.User.Status))
            .ToListAsync(cancellationToken);

        return new PagedResponse<CompanyUserSummaryResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<CompanyUserResponse?> GetUserAsync(Guid companyPublicId, Guid userPublicId, CancellationToken cancellationToken) =>
        dbContext.UserCompanyMemberships
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
                (membership, user) => new { Membership = membership, User = user })
            .Join(
                dbContext.IamRoles.AsNoTracking(),
                item => item.Membership.RoleId,
                role => role.Id,
                (item, role) => new
                {
                    item.Membership,
                    item.User,
                    Role = role
                })
            .Where(item => item.User.PublicId == userPublicId)
            .Select(item => new CompanyUserResponse(
                item.User.PublicId,
                item.User.Email,
                item.User.FirstName,
                item.User.LastName,
                item.Role.PublicId,
                item.Role.Name,
                item.User.Status))
            .SingleOrDefaultAsync(cancellationToken);
}
