using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.OrgUnits;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.OrgUnits.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.OrgUnits;

internal sealed class OrgUnitAuthorizationService(
    ICurrentUserService currentUserService,
    CLARIHR.Application.Abstractions.Tenancy.ITenantContext tenantContext,
    IPlanEntitlementService planEntitlementService,
    ApplicationDbContext dbContext) : IOrgUnitAuthorizationService
{
    public Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureAuthorizedAsync(companyId, manageRequired: false, cancellationToken);

    public Task<Result> EnsureCanManageAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureAuthorizedAsync(companyId, manageRequired: true, cancellationToken);

    public Error TenantMismatch(RbacPermissionAction action) => OrgUnitErrors.TenantMismatch(action);

    private async Task<Result> EnsureAuthorizedAsync(Guid companyId, bool manageRequired, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || !tenantContext.TenantId.HasValue || string.IsNullOrWhiteSpace(currentUserService.UserId))
        {
            return Result.Failure(AuthorizationErrors.Unauthenticated);
        }

        if (tenantContext.TenantId.Value != companyId)
        {
            return Result.Failure(OrgUnitErrors.TenantMismatch(manageRequired ? RbacPermissionAction.Update : RbacPermissionAction.Read));
        }

        if (!await planEntitlementService.IsModuleEnabledAsync(companyId, CommercialModuleKeys.OrgUnits, cancellationToken))
        {
            return Result.Failure(OrgUnitErrors.Forbidden);
        }

        var normalizedClaims = currentUserService.Permissions
            .Select(static permission => permission.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var requiredClaims = manageRequired
            ? new[]
            {
                OrgUnitPermissionCodes.Admin.ToUpperInvariant(),
                OrgUnitPermissionCodes.ManageAdministration.ToUpperInvariant()
            }
            : new[]
            {
                OrgUnitPermissionCodes.Read.ToUpperInvariant(),
                OrgUnitPermissionCodes.Admin.ToUpperInvariant(),
                OrgUnitPermissionCodes.ManageAdministration.ToUpperInvariant()
            };

        if (requiredClaims.Any(normalizedClaims.Contains))
        {
            return Result.Success();
        }

        if (!Guid.TryParse(currentUserService.UserId, out var currentUserPublicId))
        {
            return Result.Failure(AuthorizationErrors.Unauthenticated);
        }

        var normalizedPermissionCodes = requiredClaims;
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
                    normalizedPermissionCodes.Contains(assignment.Permission.NormalizedCode)),
                cancellationToken);

        return isAuthorized
            ? Result.Success()
            : Result.Failure(OrgUnitErrors.Forbidden);
    }
}
