using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Domain.IdentityAccess;

namespace CLARIHR.Application.Features.IdentityAccess.Common;

public static class IdentityAccessAuditMapper
{
    public static object CreateIamUserSnapshot(IamUser user, IEnumerable<IamRole> roles) =>
        new
        {
            userId = user.PublicId,
            email = user.Email,
            firstName = user.FirstName,
            lastName = user.LastName,
            isActive = user.IsActive,
            roleIds = roles.Select(static role => role.PublicId).OrderBy(static roleId => roleId).ToArray(),
            roleNames = roles.Select(static role => role.Name).OrderBy(static name => name).ToArray()
        };

    public static IReadOnlyDictionary<string, object> CreateIamUserRolesDiff(
        IEnumerable<IamRole> beforeRoles,
        IEnumerable<IamRole> afterRoles)
    {
        var beforeRoleIds = beforeRoles.Select(static role => role.PublicId).OrderBy(static roleId => roleId).ToArray();
        var afterRoleIds = afterRoles.Select(static role => role.PublicId).OrderBy(static roleId => roleId).ToArray();
        var beforeRoleNames = beforeRoles.Select(static role => role.Name).OrderBy(static name => name).ToArray();
        var afterRoleNames = afterRoles.Select(static role => role.Name).OrderBy(static name => name).ToArray();

        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["roleIds"] = AuditPayloads.Change(beforeRoleIds, afterRoleIds),
            ["roleNames"] = AuditPayloads.Change(beforeRoleNames, afterRoleNames)
        };
    }

    public static object CreateRoleSnapshot(IamRole role, IEnumerable<IamPermission>? permissions = null) =>
        CreateRoleSnapshot(
            role.PublicId,
            role.Name,
            role.Description,
            role.IsSystemRole,
            permissions);

    public static object CreateRoleSnapshot(
        Guid roleId,
        string name,
        string? description,
        bool isSystemRole,
        IEnumerable<IamPermission>? permissions = null) =>
        new
        {
            roleId,
            name,
            description,
            isSystemRole,
            permissionCodes = permissions?
                .Select(static permission => permission.Code)
                .OrderBy(static code => code)
                .ToArray()
        };

    public static IReadOnlyDictionary<string, object> CreateRoleDiff(
        string beforeName,
        string afterName,
        string? beforeDescription,
        string? afterDescription)
    {
        var diff = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (!string.Equals(beforeName, afterName, StringComparison.Ordinal))
        {
            diff["name"] = AuditPayloads.Change(beforeName, afterName);
        }

        if (!string.Equals(beforeDescription, afterDescription, StringComparison.Ordinal))
        {
            diff["description"] = AuditPayloads.Change(beforeDescription, afterDescription);
        }

        return diff;
    }

    public static IReadOnlyDictionary<string, object> CreatePermissionMatrixSnapshot(
        IReadOnlyDictionary<RbacPermissionScreen, RbacPermissionState> states,
        IEnumerable<RbacPermissionScreen> screens)
    {
        return screens
            .Distinct()
            .ToDictionary(
                screen => PermissionMatrixCatalog.Get(screen).ResourceKey,
                screen => (object)new
                {
                    hasAccess = states[screen].HasAccess,
                    canRead = states[screen].CanRead,
                    canCreate = states[screen].CanCreate,
                    canUpdate = states[screen].CanUpdate,
                    canDelete = states[screen].CanDelete
                },
                StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyDictionary<string, object> CreatePermissionMatrixDiff(
        IReadOnlyDictionary<RbacPermissionScreen, RbacPermissionState> beforeStates,
        IReadOnlyDictionary<RbacPermissionScreen, RbacPermissionState> afterStates,
        IEnumerable<RbacPermissionScreen> screens)
    {
        var diff = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var screen in screens.Distinct())
        {
            var before = beforeStates[screen];
            var after = afterStates[screen];
            if (before == after)
            {
                continue;
            }

            var changes = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            AddPermissionChange(changes, "hasAccess", before.HasAccess, after.HasAccess);
            AddPermissionChange(changes, "canRead", before.CanRead, after.CanRead);
            AddPermissionChange(changes, "canCreate", before.CanCreate, after.CanCreate);
            AddPermissionChange(changes, "canUpdate", before.CanUpdate, after.CanUpdate);
            AddPermissionChange(changes, "canDelete", before.CanDelete, after.CanDelete);

            diff[PermissionMatrixCatalog.Get(screen).ResourceKey] = changes;
        }

        return diff;
    }

    public static IReadOnlyDictionary<string, object> CreateFieldPermissionSnapshot(
        IReadOnlyDictionary<string, FieldPermissionOverrideState> states)
    {
        return states.ToDictionary(
            static pair => pair.Key,
            static pair => (object)new
            {
                isVisible = pair.Value.IsVisible,
                isEditable = pair.Value.IsEditable,
                isRequired = pair.Value.IsRequired,
                isMasked = pair.Value.IsMasked
            },
            StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyDictionary<string, object> CreateFieldPermissionDiff(
        IReadOnlyDictionary<string, FieldPermissionOverrideState> beforeStates,
        IReadOnlyDictionary<string, FieldPermissionOverrideState> afterStates)
    {
        var diff = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var fieldKey in afterStates.Keys.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var before = beforeStates[fieldKey];
            var after = afterStates[fieldKey];
            if (before == after)
            {
                continue;
            }

            var changes = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            AddPermissionChange(changes, "isVisible", before.IsVisible, after.IsVisible);
            AddPermissionChange(changes, "isEditable", before.IsEditable, after.IsEditable);
            AddPermissionChange(changes, "isRequired", before.IsRequired, after.IsRequired);
            AddPermissionChange(changes, "isMasked", before.IsMasked, after.IsMasked);

            diff[fieldKey] = changes;
        }

        return diff;
    }

    private static void AddPermissionChange(
        IDictionary<string, object> changes,
        string key,
        bool before,
        bool after)
    {
        if (before != after)
        {
            changes[key] = AuditPayloads.Change(before, after);
        }
    }
}
