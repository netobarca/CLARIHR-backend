using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.SalaryTabulator;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.SalaryTabulator.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.SalaryTabulator;

internal sealed class SalaryTabulatorAuthorizationService(
    ICurrentUserService currentUserService,
    CLARIHR.Application.Abstractions.Tenancy.ITenantContext tenantContext,
    IPlanEntitlementService planEntitlementService,
    ApplicationDbContext dbContext) : ISalaryTabulatorAuthorizationService
{
    public Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureAuthorizedAsync(companyId, PermissionMode.Read, cancellationToken);

    public Task<Result> EnsureCanRequestAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureAuthorizedAsync(companyId, PermissionMode.Request, cancellationToken);

    public Task<Result> EnsureCanApproveAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureAuthorizedAsync(companyId, PermissionMode.Approve, cancellationToken);

    public Error TenantMismatch(RbacPermissionAction action) => SalaryTabulatorErrors.TenantMismatch(action);

    private async Task<Result> EnsureAuthorizedAsync(Guid companyId, PermissionMode mode, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || !tenantContext.TenantId.HasValue || string.IsNullOrWhiteSpace(currentUserService.UserId))
        {
            return Result.Failure(AuthorizationErrors.Unauthenticated);
        }

        if (tenantContext.TenantId.Value != companyId)
        {
            return Result.Failure(SalaryTabulatorErrors.TenantMismatch(mode == PermissionMode.Read ? RbacPermissionAction.Read : RbacPermissionAction.Update));
        }

        if (!await planEntitlementService.IsModuleEnabledAsync(companyId, CommercialModuleKeys.SalaryTabulator, cancellationToken))
        {
            return Result.Failure(SalaryTabulatorErrors.Forbidden);
        }

        var normalizedClaims = currentUserService.Permissions
            .Select(static permission => permission.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var requiredClaims = mode switch
        {
            PermissionMode.Read => new[]
            {
                SalaryTabulatorPermissionCodes.Read.ToUpperInvariant(),
                SalaryTabulatorPermissionCodes.Request.ToUpperInvariant(),
                SalaryTabulatorPermissionCodes.Approve.ToUpperInvariant(),
                SalaryTabulatorPermissionCodes.Admin.ToUpperInvariant(),
                SalaryTabulatorPermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            PermissionMode.Request => new[]
            {
                SalaryTabulatorPermissionCodes.Request.ToUpperInvariant(),
                SalaryTabulatorPermissionCodes.Admin.ToUpperInvariant(),
                SalaryTabulatorPermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            _ => new[]
            {
                SalaryTabulatorPermissionCodes.Approve.ToUpperInvariant(),
                SalaryTabulatorPermissionCodes.Admin.ToUpperInvariant(),
                SalaryTabulatorPermissionCodes.ManageAdministration.ToUpperInvariant()
            }
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
            : Result.Failure(SalaryTabulatorErrors.Forbidden);
    }

    private enum PermissionMode
    {
        Read = 1,
        Request = 2,
        Approve = 3
    }
}
