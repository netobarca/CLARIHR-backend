using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.PositionSlots;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PositionSlots.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;
using CLARIHR.Infrastructure.Authorization;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.PositionSlots;

internal sealed class PositionSlotAuthorizationService(
    ICurrentUserService currentUserService,
    CLARIHR.Application.Abstractions.Tenancy.ITenantContext tenantContext,
    IPlanEntitlementService planEntitlementService,
    ApplicationDbContext dbContext) : IPositionSlotAuthorizationService
{
    private static readonly string ReadCode = PositionSlotPermissionCodes.Read.ToUpperInvariant();
    private static readonly string AdminCode = PositionSlotPermissionCodes.Admin.ToUpperInvariant();
    private static readonly string ManageAdminCode = PositionSlotPermissionCodes.ManageAdministration.ToUpperInvariant();

    public Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureAuthorizedAsync(companyId, manageRequired: false, cancellationToken);

    public Task<Result> EnsureCanManageAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureAuthorizedAsync(companyId, manageRequired: true, cancellationToken);

    // §PS1: one auth/tenant/entitlement check + claim short-circuit + at most one
    // permission DB probe (manage ⊂ read, so a manage grant implies read). Replaces the
    // EnsureCanReadAsync + EnsureCanManageAsync double round-trip on the read path.
    public async Task<Result<PositionSlotAccess>> EvaluateAccessAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || !tenantContext.TenantId.HasValue || string.IsNullOrWhiteSpace(currentUserService.UserId))
        {
            return Result<PositionSlotAccess>.Failure(AuthorizationErrors.Unauthenticated);
        }

        if (tenantContext.TenantId.Value != companyId)
        {
            return Result<PositionSlotAccess>.Failure(PositionSlotErrors.TenantMismatch(RbacPermissionAction.Read));
        }

        if (!await planEntitlementService.IsModuleEnabledAsync(companyId, CommercialModuleKeys.PositionSlots, cancellationToken))
        {
            return Result<PositionSlotAccess>.Failure(PositionSlotErrors.Forbidden);
        }

        var normalizedClaims = currentUserService.Permissions
            .Select(static permission => permission.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var claimManage = normalizedClaims.Contains(AdminCode) || normalizedClaims.Contains(ManageAdminCode);
        var claimRead = claimManage || normalizedClaims.Contains(ReadCode);

        // Claims already grant manage ⇒ read too: zero permission DB round-trips (the
        // §PS1 amplifier path — includeAllowedActions on every listed page).
        if (claimManage)
        {
            return Result<PositionSlotAccess>.Success(new PositionSlotAccess(CanRead: true, CanManage: true));
        }

        if (!Guid.TryParse(currentUserService.UserId, out var currentUserPublicId))
        {
            return Result<PositionSlotAccess>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var dbManage = await TenantPermissionGrantEvaluator.HasAnyRequiredPermissionAsync(
            dbContext,
            companyId,
            currentUserPublicId,
            new[] { AdminCode, ManageAdminCode },
            cancellationToken);

        if (dbManage)
        {
            return Result<PositionSlotAccess>.Success(new PositionSlotAccess(CanRead: true, CanManage: true));
        }

        if (claimRead)
        {
            return Result<PositionSlotAccess>.Success(new PositionSlotAccess(CanRead: true, CanManage: false));
        }

        var dbRead = await TenantPermissionGrantEvaluator.HasAnyRequiredPermissionAsync(
            dbContext,
            companyId,
            currentUserPublicId,
            new[] { ReadCode },
            cancellationToken);

        return dbRead
            ? Result<PositionSlotAccess>.Success(new PositionSlotAccess(CanRead: true, CanManage: false))
            : Result<PositionSlotAccess>.Failure(PositionSlotErrors.Forbidden);
    }

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

        if (!await planEntitlementService.IsModuleEnabledAsync(companyId, CommercialModuleKeys.PositionSlots, cancellationToken))
        {
            return Result.Failure(PositionSlotErrors.Forbidden);
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

        var isAuthorized = await TenantPermissionGrantEvaluator.HasAnyRequiredPermissionAsync(
            dbContext,
            companyId,
            currentUserPublicId,
            requiredClaims,
            cancellationToken);

        return isAuthorized
            ? Result.Success()
            : Result.Failure(PositionSlotErrors.Forbidden);
    }
}
