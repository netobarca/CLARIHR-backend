using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.Leave;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Leave.Common;
using CLARIHR.Domain.Companies;
using CLARIHR.Infrastructure.Authorization;
using CLARIHR.Infrastructure.Persistence;

namespace CLARIHR.Infrastructure.Leave;

internal sealed class LeaveConfigurationAuthorizationService(
    ICurrentUserService currentUserService,
    CLARIHR.Application.Abstractions.Tenancy.ITenantContext tenantContext,
    IPlanEntitlementService planEntitlementService,
    ApplicationDbContext dbContext) : ILeaveConfigurationAuthorizationService
{
    public Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureAuthorizedAsync(companyId, manageRequired: false, cancellationToken);

    public Task<Result> EnsureCanManageAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureAuthorizedAsync(companyId, manageRequired: true, cancellationToken);

    public Error TenantMismatch(RbacPermissionAction action) => LeaveConfigurationErrors.TenantMismatch(action);

    private async Task<Result> EnsureAuthorizedAsync(Guid companyId, bool manageRequired, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || !tenantContext.TenantId.HasValue || string.IsNullOrWhiteSpace(currentUserService.UserId))
        {
            return Result.Failure(AuthorizationErrors.Unauthenticated);
        }

        if (tenantContext.TenantId.Value != companyId)
        {
            return Result.Failure(LeaveConfigurationErrors.TenantMismatch(manageRequired ? RbacPermissionAction.Update : RbacPermissionAction.Read));
        }

        if (!await planEntitlementService.IsModuleEnabledAsync(companyId, CommercialModuleKeys.PersonnelFiles, cancellationToken))
        {
            return Result.Failure(LeaveConfigurationErrors.Forbidden);
        }

        var normalizedClaims = currentUserService.Permissions
            .Select(static permission => permission.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var requiredClaims = manageRequired
            ? new[]
            {
                LeaveConfigurationPermissionCodes.Admin.ToUpperInvariant(),
                LeaveConfigurationPermissionCodes.ManageAdministration.ToUpperInvariant()
            }
            : new[]
            {
                LeaveConfigurationPermissionCodes.Read.ToUpperInvariant(),
                LeaveConfigurationPermissionCodes.Admin.ToUpperInvariant(),
                LeaveConfigurationPermissionCodes.ManageAdministration.ToUpperInvariant()
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
            : Result.Failure(LeaveConfigurationErrors.Forbidden);
    }
}
