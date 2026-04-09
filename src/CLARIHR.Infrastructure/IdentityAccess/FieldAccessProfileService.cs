using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.IdentityAccess;

internal sealed class FieldAccessProfileService(
    ApplicationDbContext dbContext,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext,
    IFieldPermissionOverrideCache fieldPermissionOverrideCache) : IFieldAccessProfileService
{
    public async Task<Result<FieldAccessProfile>> GetCurrentUserAccessProfileAsync(
        string resourceKey,
        CancellationToken cancellationToken)
    {
        if (!PermissionMatrixCatalog.TryGet(resourceKey, out var definition))
        {
            return Result<FieldAccessProfile>.Failure(CreateResourceValidationError(resourceKey));
        }

        var catalog = await GetCatalogAsync(definition.ResourceKey, cancellationToken);
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<FieldAccessProfile>.Failure(IdentityAccessErrors.TenantContextRequired);
        }

        var actorUserId = TryParseCurrentUserId(currentUserService.UserId);
        if (!actorUserId.HasValue)
        {
            return Result<FieldAccessProfile>.Failure(IdentityAccessErrors.InvalidCurrentUser);
        }

        var actor = await dbContext.IamUsers
            .AsNoTracking()
            .Include(user => user.RoleAssignments)
            .ThenInclude(assignment => assignment.Role)
            .ThenInclude(role => role.PermissionAssignments)
            .ThenInclude(assignment => assignment.Permission)
            .SingleOrDefaultAsync(user => user.LinkedUserPublicId == actorUserId.Value && user.IsActive, cancellationToken);
        if (actor is null || actor.RoleAssignments.Count == 0)
        {
            var claimBasedProfile = FieldPermissionEvaluator.BuildProfile(
                definition.ResourceKey,
                catalog,
                new Dictionary<string, FieldPermissionOverrideState>(StringComparer.OrdinalIgnoreCase),
                CaptureScreenState(currentUserService.Permissions, definition.ScreenKey));
            return Result<FieldAccessProfile>.Success(claimBasedProfile);
        }

        var profiles = new List<FieldAccessProfile>();
        foreach (var assignment in actor.RoleAssignments)
        {
            var screenState = CaptureScreenState(
                assignment.Role.PermissionAssignments.Select(static permissionAssignment => permissionAssignment.Permission),
                definition.ScreenKey);
            var overrides = await GetRoleOverridesAsync(assignment.RoleId, definition.ResourceKey, cancellationToken);
            profiles.Add(FieldPermissionEvaluator.BuildProfile(definition.ResourceKey, catalog, overrides, screenState));
        }

        return Result<FieldAccessProfile>.Success(FieldPermissionEvaluator.Merge(definition.ResourceKey, catalog, profiles));
    }

    private async Task<IReadOnlyCollection<FieldCatalogDefinition>> GetCatalogAsync(
        string resourceKey,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return FieldCatalogRegistry.GetResourceFields(resourceKey);
    }

    private async Task<IReadOnlyDictionary<string, FieldPermissionOverrideState>> GetRoleOverridesAsync(
        long roleId,
        string resourceKey,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return new Dictionary<string, FieldPermissionOverrideState>(StringComparer.OrdinalIgnoreCase);
        }

        var cached = await fieldPermissionOverrideCache.GetRoleOverridesAsync(
            tenantContext.TenantId.Value,
            roleId,
            resourceKey,
            cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var normalizedResourcePrefix = $"{resourceKey.Trim().ToUpperInvariant()}.";
        var permissions = await dbContext.RoleFieldPermissions
            .AsNoTracking()
            .Where(permission => permission.RoleId == roleId && permission.NormalizedFieldKey.StartsWith(normalizedResourcePrefix))
            .ToDictionaryAsync(
                permission => permission.FieldKey,
                permission => new FieldPermissionOverrideState(
                    permission.IsVisible,
                    permission.IsEditable,
                    permission.IsRequired,
                    permission.IsMasked),
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);

        await fieldPermissionOverrideCache.SetRoleOverridesAsync(
            tenantContext.TenantId.Value,
            roleId,
            resourceKey,
            permissions,
            cancellationToken);
        return permissions;
    }

    private static RbacPermissionState CaptureScreenState(IEnumerable<IamPermission> permissions, RbacPermissionScreen screen) =>
        CaptureScreenState(
            permissions.Select(static permission => permission.NormalizedCode),
            screen);

    private static RbacPermissionState CaptureScreenState(IEnumerable<string> grantedCodes, RbacPermissionScreen screen)
    {
        var normalizedCodes = grantedCodes.ToArray();

        return new RbacPermissionState(
            HasAccess: RbacAuthorizationEvaluator.IsAllowed(normalizedCodes, screen, RbacPermissionAction.Access),
            CanRead: RbacAuthorizationEvaluator.IsAllowed(normalizedCodes, screen, RbacPermissionAction.Read),
            CanCreate: RbacAuthorizationEvaluator.IsAllowed(normalizedCodes, screen, RbacPermissionAction.Create),
            CanUpdate: RbacAuthorizationEvaluator.IsAllowed(normalizedCodes, screen, RbacPermissionAction.Update),
            CanDelete: RbacAuthorizationEvaluator.IsAllowed(normalizedCodes, screen, RbacPermissionAction.Delete));
    }

    private static Error CreateResourceValidationError(string resourceKey) =>
        ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [nameof(resourceKey)] = [$"Unknown resource key '{resourceKey}'."]
        });

    private static Guid? TryParseCurrentUserId(string? currentUserId) =>
        Guid.TryParse(currentUserId, out var actorUserId) ? actorUserId : null;
}
