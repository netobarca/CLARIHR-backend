using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.JobProfiles;

internal sealed class JobProfileAuthorizationService(
    ICurrentUserService currentUserService,
    CLARIHR.Application.Abstractions.Tenancy.ITenantContext tenantContext,
    ApplicationDbContext dbContext) : IJobProfileAuthorizationService
{
    public Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureAuthorizedAsync(companyId, PermissionScope.Read, cancellationToken);

    public Task<Result> EnsureCanManageProfilesAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureAuthorizedAsync(companyId, PermissionScope.ProfileAdmin, cancellationToken);

    public Task<Result> EnsureCanManageCatalogsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureAuthorizedAsync(companyId, PermissionScope.CatalogAdmin, cancellationToken);

    public Error TenantMismatch(RbacPermissionAction action) => JobProfileErrors.TenantMismatch(action);

    private async Task<Result> EnsureAuthorizedAsync(Guid companyId, PermissionScope scope, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || !tenantContext.TenantId.HasValue || string.IsNullOrWhiteSpace(currentUserService.UserId))
        {
            return Result.Failure(AuthorizationErrors.Unauthenticated);
        }

        if (tenantContext.TenantId.Value != companyId)
        {
            var action = scope == PermissionScope.Read ? RbacPermissionAction.Read : RbacPermissionAction.Update;
            return Result.Failure(JobProfileErrors.TenantMismatch(action));
        }

        var normalizedClaims = currentUserService.Permissions
            .Select(static permission => permission.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var requiredClaims = ResolveRequiredClaims(scope);
        if (requiredClaims.Any(normalizedClaims.Contains))
        {
            return Result.Success();
        }

        if (!Guid.TryParse(currentUserService.UserId, out var currentUserPublicId))
        {
            return Result.Failure(AuthorizationErrors.Unauthenticated);
        }

        var isAuthorized = await
            (from membership in dbContext.UserCompanyMemberships.AsNoTracking()
             join user in dbContext.AuthUsers.AsNoTracking() on membership.UserId equals user.Id
             join company in dbContext.Companies.AsNoTracking() on membership.CompanyId equals company.Id
             join role in dbContext.IamRoles.AsNoTracking()
                    .Include(role => role.PermissionAssignments)
                    .ThenInclude(assignment => assignment.Permission)
                 on membership.RoleId equals role.Id
             where user.PublicId == currentUserPublicId &&
                   user.Status == UserStatus.Active &&
                   membership.Status == UserCompanyStatus.Active &&
                   company.PublicId == companyId
             select role)
            .AnyAsync(
                role => role.PermissionAssignments.Any(assignment =>
                    requiredClaims.Contains(assignment.Permission.NormalizedCode)),
                cancellationToken);

        return isAuthorized
            ? Result.Success()
            : Result.Failure(JobProfileErrors.Forbidden);
    }

    private static string[] ResolveRequiredClaims(PermissionScope scope) =>
        scope switch
        {
            PermissionScope.Read =>
            [
                JobProfilePermissionCodes.Read.ToUpperInvariant(),
                JobProfilePermissionCodes.Admin.ToUpperInvariant(),
                JobProfilePermissionCodes.CatalogAdmin.ToUpperInvariant(),
                JobProfilePermissionCodes.ManageAdministration.ToUpperInvariant()
            ],
            PermissionScope.ProfileAdmin =>
            [
                JobProfilePermissionCodes.Admin.ToUpperInvariant(),
                JobProfilePermissionCodes.ManageAdministration.ToUpperInvariant()
            ],
            PermissionScope.CatalogAdmin =>
            [
                JobProfilePermissionCodes.CatalogAdmin.ToUpperInvariant(),
                JobProfilePermissionCodes.ManageAdministration.ToUpperInvariant()
            ],
            _ =>
            [
                JobProfilePermissionCodes.Admin.ToUpperInvariant(),
                JobProfilePermissionCodes.ManageAdministration.ToUpperInvariant()
            ]
        };

    private enum PermissionScope
    {
        Read = 1,
        ProfileAdmin = 2,
        CatalogAdmin = 3
    }
}
