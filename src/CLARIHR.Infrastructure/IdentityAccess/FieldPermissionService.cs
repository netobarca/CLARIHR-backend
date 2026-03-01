using System.Text.Json;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.IdentityAccess.Contracts;
using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CLARIHR.Infrastructure.IdentityAccess;

internal sealed class FieldPermissionService(
    ApplicationDbContext dbContext,
    IIamAdministrationAuthorizationService authorizationService,
    IFieldAccessProfileService fieldAccessProfileService,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext,
    IDateTimeProvider dateTimeProvider,
    IMemoryCache memoryCache,
    IHttpContextAccessor httpContextAccessor,
    IAuditService auditService) : IFieldPermissionService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<ResourceFieldsResponse>> GetResourceFieldsAsync(
        string resourceKey,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(
            RbacPermissionScreen.Permissions,
            RbacPermissionAction.Read,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<ResourceFieldsResponse>.Failure(authorizationResult.Error);
        }

        if (!TryResolveResource(resourceKey, out var definition))
        {
            return Result<ResourceFieldsResponse>.Failure(CreateResourceValidationError(resourceKey));
        }

        var fields = await GetCatalogAsync(definition.ResourceKey, cancellationToken);
        var response = new ResourceFieldsResponse(
            definition.ResourceKey,
            fields
                .Where(static field => field.IsConfigurable)
                .Select(static field => new ResourceFieldResponse(
                    field.FieldKey,
                    field.PropertyName,
                    field.DisplayName,
                    field.DataType,
                    field.IsConfigurable,
                    field.IsSensitive))
                .ToArray());

        return Result<ResourceFieldsResponse>.Success(response);
    }

    public async Task<Result<RoleFieldPermissionsResponse>> GetRoleFieldPermissionsAsync(
        Guid roleId,
        string resourceKey,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(
            RbacPermissionScreen.Permissions,
            RbacPermissionAction.Read,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<RoleFieldPermissionsResponse>.Failure(authorizationResult.Error);
        }

        if (!TryResolveResource(resourceKey, out var definition))
        {
            return Result<RoleFieldPermissionsResponse>.Failure(CreateResourceValidationError(resourceKey));
        }

        var role = await dbContext.IamRoles
            .AsNoTracking()
            .Include(candidate => candidate.PermissionAssignments)
            .ThenInclude(assignment => assignment.Permission)
            .SingleOrDefaultAsync(candidate => candidate.PublicId == roleId, cancellationToken);
        if (role is null)
        {
            return Result<RoleFieldPermissionsResponse>.Failure(
                await dbContext.IamRoles
                    .IgnoreQueryFilters()
                    .AnyAsync(candidate => candidate.PublicId == roleId, cancellationToken)
                    ? AuthorizationErrors.TenantMismatch(definition.ResourceKey, RbacPermissionAction.Read, GetEndpoint())
                    : IdentityAccessErrors.RoleNotFound);
        }

        var screenState = CaptureScreenState(role.PermissionAssignments.Select(static assignment => assignment.Permission), definition.ScreenKey);
        if (!screenState.HasAccess)
        {
            return Result<RoleFieldPermissionsResponse>.Failure(FieldPermissionErrors.ResourceAccessRequired);
        }

        var catalog = await GetCatalogAsync(definition.ResourceKey, cancellationToken);
        var overrides = await GetRoleOverridesAsync(role.Id, definition.ResourceKey, cancellationToken);
        var profile = FieldPermissionEvaluator.BuildProfile(definition.ResourceKey, catalog, overrides, screenState);

        return Result<RoleFieldPermissionsResponse>.Success(Map(role.PublicId, role.Name, profile));
    }

    public async Task<Result<RoleFieldPermissionsResponse>> UpsertRoleFieldPermissionsAsync(
        Guid roleId,
        string resourceKey,
        IReadOnlyCollection<RoleFieldPermissionUpdateModel> fields,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(
            RbacPermissionScreen.Permissions,
            RbacPermissionAction.Update,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<RoleFieldPermissionsResponse>.Failure(authorizationResult.Error);
        }

        if (!tenantContext.TenantId.HasValue)
        {
            return Result<RoleFieldPermissionsResponse>.Failure(IdentityAccessErrors.TenantContextRequired);
        }

        if (!TryResolveResource(resourceKey, out var definition))
        {
            return Result<RoleFieldPermissionsResponse>.Failure(CreateResourceValidationError(resourceKey));
        }

        var role = await dbContext.IamRoles
            .Include(candidate => candidate.PermissionAssignments)
            .ThenInclude(assignment => assignment.Permission)
            .SingleOrDefaultAsync(candidate => candidate.PublicId == roleId, cancellationToken);
        if (role is null)
        {
            return Result<RoleFieldPermissionsResponse>.Failure(
                await dbContext.IamRoles
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

        var actorUserId = TryParseCurrentUserId(currentUserService.UserId);
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
        var existingPermissions = await dbContext.RoleFieldPermissions
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
                    dateTimeProvider.UtcNow);
                existing.SetTenantId(tenantContext.TenantId.Value);
                dbContext.RoleFieldPermissions.Add(existing);
            }
            else
            {
                existing.Update(
                    normalizedState.IsVisible,
                    normalizedState.IsEditable,
                    normalizedState.IsRequired,
                    normalizedState.IsMasked,
                    actorUserId.Value,
                    dateTimeProvider.UtcNow);
            }

            var auditLog = FieldPermissionAuditLog.Create(
                role.PublicId,
                field.FieldKey,
                actorUserId.Value,
                before is null ? null : JsonSerializer.Serialize(before, SerializerOptions),
                JsonSerializer.Serialize(normalizedState, SerializerOptions),
                dateTimeProvider.UtcNow);
            auditLog.SetTenantId(tenantContext.TenantId.Value);
            dbContext.FieldPermissionAuditLogs.Add(auditLog);
        }

        if (beforeStates.Count > 0)
        {
            await auditService.LogAsync(
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

        _ = await dbContext.SaveChangesAsync(cancellationToken);
        memoryCache.Remove(BuildCacheKey(tenantContext.TenantId.Value, role.Id, definition.ResourceKey));

        var refreshedOverrides = await GetRoleOverridesAsync(role.Id, definition.ResourceKey, cancellationToken);
        var profile = FieldPermissionEvaluator.BuildProfile(definition.ResourceKey, catalog, refreshedOverrides, screenState);
        return Result<RoleFieldPermissionsResponse>.Success(Map(role.PublicId, role.Name, profile));
    }

    public Task<Result<FieldAccessProfile>> GetCurrentUserAccessProfileAsync(
        string resourceKey,
        CancellationToken cancellationToken) =>
        fieldAccessProfileService.GetCurrentUserAccessProfileAsync(resourceKey, cancellationToken);

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
        var persisted = await dbContext.FieldCatalogEntries
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
        if (!tenantContext.TenantId.HasValue)
        {
            return new Dictionary<string, FieldPermissionOverrideState>(StringComparer.OrdinalIgnoreCase);
        }

        var cacheKey = BuildCacheKey(tenantContext.TenantId.Value, roleId, resourceKey);
        if (memoryCache.TryGetValue(cacheKey, out IReadOnlyDictionary<string, FieldPermissionOverrideState>? cached) &&
            cached is not null)
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

        memoryCache.Set(cacheKey, permissions, TimeSpan.FromMinutes(10));
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

    private string? GetEndpoint() => httpContextAccessor.HttpContext?.Request.Path.Value;

    private static string BuildCacheKey(Guid tenantId, long roleId, string resourceKey) =>
        $"field-permissions:{tenantId:N}:{roleId}:{resourceKey.Trim().ToUpperInvariant()}";
}
