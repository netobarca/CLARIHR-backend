using System.Text.Json;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.IdentityAccess.Contracts;
using CLARIHR.Domain.IdentityAccess;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.IdentityAccess;

internal sealed partial class FieldPermissionService
{
    public async Task<Result<RoleFieldPermissionsResponse>> UpsertRoleFieldPermissionsAsync(
        Guid roleId,
        string resourceKey,
        IReadOnlyCollection<RoleFieldPermissionUpdateModel> fields,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await _authorizationService.EnsureAuthorizedAsync(
            RbacPermissionScreen.Permissions,
            RbacPermissionAction.Update,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<RoleFieldPermissionsResponse>.Failure(authorizationResult.Error);
        }

        if (!_tenantContext.TenantId.HasValue)
        {
            return Result<RoleFieldPermissionsResponse>.Failure(IdentityAccessErrors.TenantContextRequired);
        }

        if (!TryResolveResource(resourceKey, out var definition))
        {
            return Result<RoleFieldPermissionsResponse>.Failure(CreateResourceValidationError(resourceKey));
        }

        var role = await _dbContext.IamRoles
            .Include(candidate => candidate.PermissionAssignments)
            .ThenInclude(assignment => assignment.Permission)
            .SingleOrDefaultAsync(candidate => candidate.PublicId == roleId, cancellationToken);
        if (role is null)
        {
            return Result<RoleFieldPermissionsResponse>.Failure(
                await _dbContext.IamRoles
                    .IgnoreQueryFilters()
                    .AnyAsync(candidate => candidate.PublicId == roleId, cancellationToken)
                    ? AuthorizationErrors.TenantMismatch(definition.ResourceKey, RbacPermissionAction.Update, GetEndpoint())
                    : IdentityAccessErrors.RoleNotFound);
        }

        if (role.IsSystemRole)
        {
            return Result<RoleFieldPermissionsResponse>.Failure(IdentityAccessErrors.ProtectedRoleModificationForbidden);
        }

        var screenState = CaptureScreenState(role.PermissionAssignments.Select(static assignment => assignment.Permission), definition.ScreenKey);
        if (!screenState.HasAccess)
        {
            return Result<RoleFieldPermissionsResponse>.Failure(FieldPermissionErrors.ResourceAccessRequired);
        }

        var actorUserId = TryParseCurrentUserId(_currentUserService.UserId);
        if (!actorUserId.HasValue)
        {
            return Result<RoleFieldPermissionsResponse>.Failure(IdentityAccessErrors.InvalidCurrentUser);
        }

        var catalog = await GetCatalogAsync(definition.ResourceKey, cancellationToken);
        var validationResult = ValidateUpdates(definition.ResourceKey, catalog, fields);
        if (validationResult is not null)
        {
            return Result<RoleFieldPermissionsResponse>.Failure(validationResult);
        }

        var normalizedResourcePrefix = $"{definition.ResourceKey.Trim().ToUpperInvariant()}.";
        var existingPermissions = await _dbContext.RoleFieldPermissions
            .Where(permission => permission.RoleId == role.Id && permission.NormalizedFieldKey.StartsWith(normalizedResourcePrefix))
            .ToListAsync(cancellationToken);

        var existingByFieldKey = existingPermissions.ToDictionary(
            static permission => permission.FieldKey,
            StringComparer.OrdinalIgnoreCase);
        var beforeStates = new Dictionary<string, FieldPermissionOverrideState>(StringComparer.OrdinalIgnoreCase);
        var afterStates = new Dictionary<string, FieldPermissionOverrideState>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in fields)
        {
            var normalizedState = FieldPermissionEvaluator.NormalizeOverride(
                field.IsVisible,
                field.IsEditable,
                field.IsRequired,
                field.IsMasked);

            existingByFieldKey.TryGetValue(field.FieldKey, out var existing);
            var before = existing is null
                ? null
                : new FieldPermissionOverrideState(
                    existing.IsVisible,
                    existing.IsEditable,
                    existing.IsRequired,
                    existing.IsMasked);
            var effectiveBefore = before ?? FieldPermissionEvaluator.NormalizeOverride(
                isVisible: true,
                isEditable: true,
                isRequired: false,
                isMasked: false);

            if (before == normalizedState)
            {
                continue;
            }

            beforeStates[field.FieldKey] = effectiveBefore;
            afterStates[field.FieldKey] = normalizedState;

            if (existing is null)
            {
                existing = RoleFieldPermission.Create(
                    role.Id,
                    field.FieldKey,
                    normalizedState.IsVisible,
                    normalizedState.IsEditable,
                    normalizedState.IsRequired,
                    normalizedState.IsMasked,
                    actorUserId.Value,
                    _dateTimeProvider.UtcNow);
                existing.SetTenantId(_tenantContext.TenantId.Value);
                _dbContext.RoleFieldPermissions.Add(existing);
            }
            else
            {
                existing.Update(
                    normalizedState.IsVisible,
                    normalizedState.IsEditable,
                    normalizedState.IsRequired,
                    normalizedState.IsMasked,
                    actorUserId.Value,
                    _dateTimeProvider.UtcNow);
            }

            var auditLog = FieldPermissionAuditLog.Create(
                role.PublicId,
                field.FieldKey,
                actorUserId.Value,
                before is null ? null : JsonSerializer.Serialize(before, SerializerOptions),
                JsonSerializer.Serialize(normalizedState, SerializerOptions),
                _dateTimeProvider.UtcNow);
            auditLog.SetTenantId(_tenantContext.TenantId.Value);
            _dbContext.FieldPermissionAuditLogs.Add(auditLog);
        }

        if (beforeStates.Count > 0)
        {
            await _auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.RoleFieldPermissionsUpdated,
                    AuditEntityTypes.Permission,
                    role.PublicId,
                    EntityKey: definition.ResourceKey,
                    AuditActions.PermissionChange,
                    $"Updated field permissions for role {role.Name} on {definition.ResourceKey}.",
                    IdentityAccessAuditMapper.CreateFieldPermissionSnapshot(beforeStates),
                    IdentityAccessAuditMapper.CreateFieldPermissionSnapshot(afterStates),
                    IdentityAccessAuditMapper.CreateFieldPermissionDiff(beforeStates, afterStates)),
                cancellationToken);
        }

        _ = await _dbContext.SaveChangesAsync(cancellationToken);
        await _fieldPermissionOverrideCache.RemoveRoleOverridesAsync(
            _tenantContext.TenantId.Value,
            role.Id,
            definition.ResourceKey,
            cancellationToken);

        var refreshedOverrides = await GetRoleOverridesAsync(role.Id, definition.ResourceKey, cancellationToken);
        var profile = FieldPermissionEvaluator.BuildProfile(definition.ResourceKey, catalog, refreshedOverrides, screenState);
        return Result<RoleFieldPermissionsResponse>.Success(Map(role.PublicId, role.Name, profile));
    }
}
