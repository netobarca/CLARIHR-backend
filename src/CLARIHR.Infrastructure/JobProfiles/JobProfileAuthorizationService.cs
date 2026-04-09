using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;
using CLARIHR.Infrastructure.Authorization;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.JobProfiles;

internal sealed class JobProfileAuthorizationService(
    ICurrentUserService currentUserService,
    CLARIHR.Application.Abstractions.Tenancy.ITenantContext tenantContext,
    IPlanEntitlementService planEntitlementService,
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

        if (!await planEntitlementService.IsModuleEnabledAsync(companyId, CommercialModuleKeys.JobProfiles, cancellationToken))
        {
            return Result.Failure(JobProfileErrors.Forbidden);
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

        var isAuthorized = await TenantPermissionGrantEvaluator.HasAnyRequiredPermissionAsync(
            dbContext,
            companyId,
            currentUserPublicId,
            requiredClaims,
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
