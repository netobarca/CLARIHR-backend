using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.IdentityAccess.Contracts;
using CLARIHR.Domain.IdentityAccess;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.IdentityAccess;

internal sealed partial class FieldPermissionService
{
    private async Task<IReadOnlyCollection<FieldCatalogDefinition>> GetCatalogAsync(
        string resourceKey,
        CancellationToken cancellationToken)
    {
        var inCode = FieldCatalogRegistry.GetResourceFields(resourceKey);
        if (inCode.Count > 0)
        {
            return inCode;
        }

        var normalizedResourceKey = resourceKey.Trim().ToUpperInvariant();
        var persisted = await _dbContext.FieldCatalogEntries
            .AsNoTracking()
            .Where(entry => entry.NormalizedResourceKey == normalizedResourceKey)
            .OrderBy(entry => entry.DisplayName)
            .Select(entry => new FieldCatalogDefinition(
                entry.FieldKey,
                entry.ResourceKey,
                entry.PropertyName,
                entry.DisplayName,
                entry.DataType,
                entry.IsConfigurable,
                entry.IsSensitive))
            .ToListAsync(cancellationToken);

        return persisted;
    }

    private async Task<IReadOnlyDictionary<string, FieldPermissionOverrideState>> GetRoleOverridesAsync(
        long roleId,
        string resourceKey,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.TenantId.HasValue)
        {
            return new Dictionary<string, FieldPermissionOverrideState>(StringComparer.OrdinalIgnoreCase);
        }

        var cached = await _fieldPermissionOverrideCache.GetRoleOverridesAsync(
            _tenantContext.TenantId.Value,
            roleId,
            resourceKey,
            cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var normalizedResourcePrefix = $"{resourceKey.Trim().ToUpperInvariant()}.";
        var permissions = await _dbContext.RoleFieldPermissions
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

        await _fieldPermissionOverrideCache.SetRoleOverridesAsync(
            _tenantContext.TenantId.Value,
            roleId,
            resourceKey,
            permissions,
            cancellationToken);
        return permissions;
    }

    private static RoleFieldPermissionsResponse Map(
        Guid roleId,
        string roleName,
        FieldAccessProfile profile)
    {
        var fields = profile.Rules
            .Where(static rule => rule.IsConfigurable)
            .OrderBy(static rule => rule.DisplayName)
            .Select(static rule => new RoleFieldPermissionResponse(
                rule.FieldKey,
                rule.PropertyName,
                rule.DisplayName,
                rule.DataType,
                rule.IsSensitive,
                rule.IsVisible,
                rule.IsEditable,
                rule.IsRequired,
                rule.IsMasked,
                IsReadOnly: rule.IsVisible && !rule.IsEditable))
            .ToArray();

        return new RoleFieldPermissionsResponse(roleId, roleName, profile.ResourceKey, fields);
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

    private static Error? ValidateUpdates(
        string resourceKey,
        IReadOnlyCollection<FieldCatalogDefinition> catalog,
        IReadOnlyCollection<RoleFieldPermissionUpdateModel> fields)
    {
        var errors = new Dictionary<string, string[]>();
        var configurableByFieldKey = catalog
            .Where(static definition => definition.IsConfigurable)
            .ToDictionary(static definition => definition.FieldKey, StringComparer.OrdinalIgnoreCase);

        var duplicates = fields
            .GroupBy(static field => field.FieldKey, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();
        if (duplicates.Length > 0)
        {
            errors[nameof(RoleFieldPermissionUpdateModel.FieldKey)] =
                [$"Duplicate field keys are not allowed: {string.Join(", ", duplicates)}."];
        }

        foreach (var field in fields)
        {
            if (!configurableByFieldKey.ContainsKey(field.FieldKey))
            {
                errors[nameof(RoleFieldPermissionUpdateModel.FieldKey)] =
                    [$"Field '{field.FieldKey}' is not configurable for resource '{resourceKey}'."];
            }
        }

        return errors.Count > 0 ? ErrorCatalog.Validation(errors) : null;
    }

    private static bool TryResolveResource(string resourceKey, out PermissionMatrixScreenDefinition definition) =>
        PermissionMatrixCatalog.TryGet(resourceKey, out definition!);

    private static Error CreateResourceValidationError(string resourceKey) =>
        ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [nameof(resourceKey)] = [$"Unknown resource key '{resourceKey}'."]
        });

    private static Guid? TryParseCurrentUserId(string? currentUserId) =>
        Guid.TryParse(currentUserId, out var actorUserId) ? actorUserId : null;

    private string? GetEndpoint() => _httpContextAccessor.HttpContext?.Request.Path.Value;
}
