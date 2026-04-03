using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.OrgStructureCatalogs;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.OrgStructureCatalogs.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.OrgStructureCatalogs;

internal sealed class OrgStructureCatalogAuthorizationService(
    ICurrentUserService currentUserService,
    CLARIHR.Application.Abstractions.Tenancy.ITenantContext tenantContext,
    IPlanEntitlementService planEntitlementService,
    ApplicationDbContext dbContext)
    : IOrgStructureCatalogAuthorizationService
{
    public Task<Result<Guid>> EnsureAccountScopeAsync(CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
        {
            return Task.FromResult(Result<Guid>.Failure(AuthorizationErrors.Unauthenticated));
        }

        return Guid.TryParse(currentUserService.UserId, out var currentUserPublicId)
            ? Task.FromResult(Result<Guid>.Success(currentUserPublicId))
            : Task.FromResult(Result<Guid>.Failure(AuthorizationErrors.Unauthenticated));
    }

    public Task<Result> EnsureCanReadTenantAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureAuthorizedTenantAsync(companyId, manageRequired: false, cancellationToken);

    public Task<Result> EnsureCanManageTenantAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureAuthorizedTenantAsync(companyId, manageRequired: true, cancellationToken);

    public Error TenantMismatch(RbacPermissionAction action) => OrgStructureCatalogErrors.TenantMismatch(action);

    private async Task<Result> EnsureAuthorizedTenantAsync(Guid companyId, bool manageRequired, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || !tenantContext.TenantId.HasValue || string.IsNullOrWhiteSpace(currentUserService.UserId))
        {
            return Result.Failure(AuthorizationErrors.Unauthenticated);
        }

        if (tenantContext.TenantId.Value != companyId)
        {
            return Result.Failure(OrgStructureCatalogErrors.TenantMismatch(manageRequired ? RbacPermissionAction.Update : RbacPermissionAction.Read));
        }

        if (!await planEntitlementService.IsModuleEnabledAsync(companyId, CommercialModuleKeys.OrgStructureCatalogs, cancellationToken))
        {
            return Result.Failure(OrgStructureCatalogErrors.Forbidden);
        }

        var normalizedClaims = currentUserService.Permissions
            .Select(static permission => permission.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var requiredClaims = manageRequired
            ? new[]
            {
                OrgStructureCatalogPermissionCodes.Admin.ToUpperInvariant(),
                OrgStructureCatalogPermissionCodes.OrgUnitsAdmin.ToUpperInvariant(),
                OrgStructureCatalogPermissionCodes.ManageAdministration.ToUpperInvariant()
            }
            : new[]
            {
                OrgStructureCatalogPermissionCodes.Read.ToUpperInvariant(),
                OrgStructureCatalogPermissionCodes.Admin.ToUpperInvariant(),
                OrgStructureCatalogPermissionCodes.OrgUnitsRead.ToUpperInvariant(),
                OrgStructureCatalogPermissionCodes.OrgUnitsAdmin.ToUpperInvariant(),
                OrgStructureCatalogPermissionCodes.ManageAdministration.ToUpperInvariant()
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
            : Result.Failure(OrgStructureCatalogErrors.Forbidden);
    }
}
