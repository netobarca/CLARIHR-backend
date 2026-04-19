using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Authorization;

internal static class TenantPermissionGrantEvaluator
{
    public static async Task<bool> HasAnyRequiredPermissionAsync(
        ApplicationDbContext dbContext,
        Guid companyPublicId,
        Guid currentUserPublicId,
        IReadOnlyCollection<string> normalizedPermissionCodes,
        CancellationToken cancellationToken)
    {
        if (normalizedPermissionCodes.Count == 0)
        {
            return false;
        }

        var hasActiveMembership = await dbContext.UserCompanyMemberships
            .AsNoTracking()
            .Join(
                dbContext.AuthUsers.AsNoTracking().Where(user => user.PublicId == currentUserPublicId && user.Status == UserStatus.Active),
                membership => membership.UserId,
                user => user.Id,
                (membership, _) => membership)
            .Join(
                dbContext.Companies.AsNoTracking().Where(company => company.PublicId == companyPublicId),
                membership => membership.CompanyId,
                company => company.Id,
                (membership, _) => membership)
            .AnyAsync(membership => membership.Status == UserCompanyStatus.Active, cancellationToken);

        if (!hasActiveMembership)
        {
            return false;
        }

        var hasIamGrant = await dbContext.IamUsers
            // Intentional tenant filter bypass: applies explicit companyPublicId tenant filter before checking IAM grants.
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user =>
                user.TenantId == companyPublicId &&
                user.LinkedUserPublicId == currentUserPublicId &&
                user.IsActive)
            .SelectMany(user => user.RoleAssignments)
            .SelectMany(assignment => assignment.Role.PermissionAssignments)
            .AnyAsync(
                assignment => normalizedPermissionCodes.Contains(assignment.Permission.NormalizedCode),
                cancellationToken);

        if (hasIamGrant)
        {
            return true;
        }

        return await (
            from membership in dbContext.UserCompanyMemberships.AsNoTracking()
            join user in dbContext.AuthUsers.AsNoTracking() on membership.UserId equals user.Id
            join company in dbContext.Companies.AsNoTracking() on membership.CompanyId equals company.Id
            join role in dbContext.IamRoles.AsNoTracking()
                    .Include(role => role.PermissionAssignments)
                    .ThenInclude(assignment => assignment.Permission)
                on membership.RoleId equals role.Id
            where user.PublicId == currentUserPublicId &&
                  user.Status == UserStatus.Active &&
                  membership.Status == UserCompanyStatus.Active &&
                  company.PublicId == companyPublicId
            select role)
            .AnyAsync(
                role => role.PermissionAssignments.Any(assignment =>
                    normalizedPermissionCodes.Contains(assignment.Permission.NormalizedCode)),
                cancellationToken);
    }
}
