using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.PositionSlots;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PositionSlots.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.PositionSlots;

internal sealed class PositionSlotAuthorizationService(
    ICurrentUserService currentUserService,
    CLARIHR.Application.Abstractions.Tenancy.ITenantContext tenantContext,
    ApplicationDbContext dbContext) : IPositionSlotAuthorizationService
{
    public Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureAuthorizedAsync(companyId, manageRequired: false, cancellationToken);

    public Task<Result> EnsureCanManageAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureAuthorizedAsync(companyId, manageRequired: true, cancellationToken);

    public Error TenantMismatch(RbacPermissionAction action) => PositionSlotErrors.TenantMismatch(action);

    private async Task<Result> EnsureAuthorizedAsync(Guid companyId, bool manageRequired, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || !tenantContext.TenantId.HasValue || string.IsNullOrWhiteSpace(currentUserService.UserId))
        {
            return Result.Failure(AuthorizationErrors.Unauthenticated);
        }

        if (tenantContext.TenantId.Value != companyId)
        {
            return Result.Failure(PositionSlotErrors.TenantMismatch(manageRequired ? RbacPermissionAction.Update : RbacPermissionAction.Read));
        }

        if (currentUserService.Roles.Contains(PositionSlotPermissionCodes.PlatformAdminRole, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Success();
        }

        var normalizedClaims = currentUserService.Permissions
            .Select(static permission => permission.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var requiredClaims = manageRequired
            ? new[]
            {
                PositionSlotPermissionCodes.Admin.ToUpperInvariant(),
                PositionSlotPermissionCodes.ManageAdministration.ToUpperInvariant()
            }
            : new[]
            {
                PositionSlotPermissionCodes.Read.ToUpperInvariant(),
                PositionSlotPermissionCodes.Admin.ToUpperInvariant(),
                PositionSlotPermissionCodes.ManageAdministration.ToUpperInvariant()
            };

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
            : Result.Failure(PositionSlotErrors.Forbidden);
    }
}
