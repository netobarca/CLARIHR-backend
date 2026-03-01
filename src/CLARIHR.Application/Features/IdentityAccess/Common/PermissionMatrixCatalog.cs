using CLARIHR.Domain.IdentityAccess;

namespace CLARIHR.Application.Features.IdentityAccess.Common;

public enum RbacPermissionScreen
{
    Users,
    Roles,
    Permissions,
    AuditLogs
}

public enum RbacPermissionAction
{
    Access,
    Read,
    Create,
    Update,
    Delete
}

public sealed record PermissionMatrixScreenDefinition(
    RbacPermissionScreen ScreenKey,
    string ResourceKey,
    string DisplayName,
    string Module,
    string Screen,
    string PlanModuleKey,
    string CodePrefix,
    string ManagePermissionCode,
    IReadOnlyCollection<RbacPermissionAction> SupportedActions)
{
    private readonly HashSet<RbacPermissionAction> _supportedActions = SupportedActions.ToHashSet();

    public bool Supports(RbacPermissionAction action) => _supportedActions.Contains(action);
}

public static class PermissionMatrixCatalog
{
    public static readonly IReadOnlyCollection<PermissionMatrixScreenDefinition> Screens =
    [
        new(
            RbacPermissionScreen.Users,
            ResourceKey: "RBAC_USERS",
            DisplayName: "Users",
            Module: "RBAC",
            Screen: "Users",
            PlanModuleKey: "USERS",
            CodePrefix: "RBAC.USERS",
            ManagePermissionCode: IdentityPermissionCodes.ManageUsers,
            SupportedActions:
            [
                RbacPermissionAction.Access,
                RbacPermissionAction.Read,
                RbacPermissionAction.Create,
                RbacPermissionAction.Update,
                RbacPermissionAction.Delete
            ]),
        new(
            RbacPermissionScreen.Roles,
            ResourceKey: "RBAC_ROLES",
            DisplayName: "Roles",
            Module: "RBAC",
            Screen: "Roles",
            PlanModuleKey: "RBAC",
            CodePrefix: "RBAC.ROLES",
            ManagePermissionCode: IdentityPermissionCodes.ManageRoles,
            SupportedActions:
            [
                RbacPermissionAction.Access,
                RbacPermissionAction.Read,
                RbacPermissionAction.Create,
                RbacPermissionAction.Update,
                RbacPermissionAction.Delete
            ]),
        new(
            RbacPermissionScreen.Permissions,
            ResourceKey: "RBAC_PERMISSIONS",
            DisplayName: "Permissions",
            Module: "RBAC",
            Screen: "Permissions",
            PlanModuleKey: "RBAC",
            CodePrefix: "RBAC.PERMISSIONS",
            ManagePermissionCode: IdentityPermissionCodes.ManagePermissions,
            SupportedActions:
            [
                RbacPermissionAction.Access,
                RbacPermissionAction.Read,
                RbacPermissionAction.Create,
                RbacPermissionAction.Update
            ]),
        new(
            RbacPermissionScreen.AuditLogs,
            ResourceKey: "AUDIT_LOGS",
            DisplayName: "Audit Logs",
            Module: "RBAC",
            Screen: "AuditLogs",
            PlanModuleKey: "RBAC",
            CodePrefix: "RBAC.AUDIT",
            ManagePermissionCode: IdentityPermissionCodes.ManagePermissions,
            SupportedActions:
            [
                RbacPermissionAction.Access,
                RbacPermissionAction.Read
            ])
    ];

    private static readonly IReadOnlyDictionary<RbacPermissionScreen, PermissionMatrixScreenDefinition> ByScreenKey =
        Screens.ToDictionary(static screen => screen.ScreenKey);

    private static readonly IReadOnlyDictionary<string, PermissionMatrixScreenDefinition> ByScreenName =
        Screens.ToDictionary(
            static screen => screen.Screen,
            static screen => screen,
            StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, PermissionMatrixScreenDefinition> ByResourceKey =
        Screens.ToDictionary(
            static screen => screen.ResourceKey,
            static screen => screen,
            StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<string> AllMatrixCodes =>
        Screens.SelectMany(static screen => screen.SupportedActions.Select(action => BuildPermissionCode(screen.ScreenKey, action)))
            .ToArray();

    public static PermissionMatrixScreenDefinition Get(RbacPermissionScreen screen) => ByScreenKey[screen];

    public static bool TryGet(string screenOrResourceKey, out PermissionMatrixScreenDefinition definition) =>
        ByScreenName.TryGetValue(screenOrResourceKey, out definition!) ||
        ByResourceKey.TryGetValue(screenOrResourceKey, out definition!);

    public static bool TryGetByResourceKey(string resourceKey, out PermissionMatrixScreenDefinition definition) =>
        ByResourceKey.TryGetValue(resourceKey, out definition!);

    public static string BuildPermissionCode(RbacPermissionScreen screen, RbacPermissionAction action)
    {
        var definition = Get(screen);
        return $"{definition.CodePrefix}.{action.ToString().ToUpperInvariant()}";
    }

    public static bool IsMatrixPermissionCode(string normalizedPermissionCode) =>
        AllMatrixCodes.Contains(normalizedPermissionCode, StringComparer.OrdinalIgnoreCase);

    public static bool BelongsToScreen(string normalizedPermissionCode, RbacPermissionScreen screen) =>
        normalizedPermissionCode.StartsWith($"{Get(screen).CodePrefix}.", StringComparison.OrdinalIgnoreCase);

    public static IamPermission CreatePermission(RbacPermissionScreen screen, RbacPermissionAction action)
    {
        var definition = Get(screen);
        var actionName = action.ToString();

        return IamPermission.CreateScreenAction(
            BuildPermissionCode(screen, action),
            $"{definition.Screen} {actionName}",
            $"Allows {actionName.ToLowerInvariant()} operations on the {definition.Screen} screen.",
            definition.Module,
            definition.Screen,
            actionName);
    }
}
