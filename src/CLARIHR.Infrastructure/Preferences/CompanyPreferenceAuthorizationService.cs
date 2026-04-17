using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Preferences.Common;
using CLARIHR.Infrastructure.Authorization;
using CLARIHR.Infrastructure.Persistence;

namespace CLARIHR.Infrastructure.Preferences;

internal sealed class CompanyPreferenceAuthorizationService(
    ICurrentUserService currentUserService,
    CLARIHR.Application.Abstractions.Tenancy.ITenantContext tenantContext,
    ApplicationDbContext dbContext) : ICompanyPreferenceAuthorizationService
{
    public Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureAuthorizedAsync(companyId, manageRequired: false, cancellationToken);

    public Task<Result> EnsureCanManageAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureAuthorizedAsync(companyId, manageRequired: true, cancellationToken);

    public Error TenantMismatch(RbacPermissionAction action) => PreferenceErrors.TenantMismatch(action);

    private async Task<Result> EnsureAuthorizedAsync(Guid companyId, bool manageRequired, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || !tenantContext.TenantId.HasValue || string.IsNullOrWhiteSpace(currentUserService.UserId))
        {
            return Result.Failure(AuthorizationErrors.Unauthenticated);
        }

        if (tenantContext.TenantId.Value != companyId)
        {
            return Result.Failure(PreferenceErrors.TenantMismatch(manageRequired ? RbacPermissionAction.Update : RbacPermissionAction.Read));
        }

        var normalizedClaims = currentUserService.Permissions
            .Select(static permission => permission.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var requiredClaims = manageRequired
            ? new[]
            {
                CompanyPreferencePermissionCodes.Admin.ToUpperInvariant(),
                CompanyPreferencePermissionCodes.ManageAdministration.ToUpperInvariant()
            }
            : new[]
            {
                CompanyPreferencePermissionCodes.Read.ToUpperInvariant(),
                CompanyPreferencePermissionCodes.Admin.ToUpperInvariant(),
                CompanyPreferencePermissionCodes.ManageAdministration.ToUpperInvariant()
            };

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
            : Result.Failure(PreferenceErrors.CompanyForbidden);
    }
}
