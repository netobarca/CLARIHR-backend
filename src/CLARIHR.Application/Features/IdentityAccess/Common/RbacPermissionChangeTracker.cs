using System.Text.Json;
using CLARIHR.Domain.IdentityAccess;

namespace CLARIHR.Application.Features.IdentityAccess.Common;

public sealed record RbacPermissionState(
    bool HasAccess,
    bool CanRead,
    bool CanCreate,
    bool CanUpdate,
    bool CanDelete);

public static class RbacPermissionChangeTracker
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyDictionary<RbacPermissionScreen, RbacPermissionState> CaptureStates(IEnumerable<IamPermission> permissions)
    {
        var grantedCodes = permissions
            .Select(static permission => permission.NormalizedCode)
            .ToArray();

        return PermissionMatrixCatalog.Screens.ToDictionary(
            static definition => definition.ScreenKey,
            definition => new RbacPermissionState(
                HasAccess: RbacAuthorizationEvaluator.IsAllowed(grantedCodes, definition.ScreenKey, RbacPermissionAction.Access),
                CanRead: definition.Supports(RbacPermissionAction.Read) &&
                         RbacAuthorizationEvaluator.IsAllowed(grantedCodes, definition.ScreenKey, RbacPermissionAction.Read),
                CanCreate: definition.Supports(RbacPermissionAction.Create) &&
                           RbacAuthorizationEvaluator.IsAllowed(grantedCodes, definition.ScreenKey, RbacPermissionAction.Create),
                CanUpdate: definition.Supports(RbacPermissionAction.Update) &&
                           RbacAuthorizationEvaluator.IsAllowed(grantedCodes, definition.ScreenKey, RbacPermissionAction.Update),
                CanDelete: definition.Supports(RbacPermissionAction.Delete) &&
                           RbacAuthorizationEvaluator.IsAllowed(grantedCodes, definition.ScreenKey, RbacPermissionAction.Delete)));
    }

    public static IReadOnlyList<RbacPermissionAuditLog> CreateAuditLogs(
        Guid rolePublicId,
        Guid changedByUserId,
        DateTime changedAtUtc,
        IEnumerable<RbacPermissionScreen> affectedScreens,
        IReadOnlyDictionary<RbacPermissionScreen, RbacPermissionState> beforeStates,
        IReadOnlyDictionary<RbacPermissionScreen, RbacPermissionState> afterStates)
    {
        var logs = new List<RbacPermissionAuditLog>();

        foreach (var screen in affectedScreens.Distinct())
        {
            var before = beforeStates[screen];
            var after = afterStates[screen];
            if (before == after)
            {
                continue;
            }

            var definition = PermissionMatrixCatalog.Get(screen);
            var changeType = HasAnyGrant(before) && !HasAnyGrant(after)
                ? RbacPermissionAuditChangeType.Disable
                : RbacPermissionAuditChangeType.Upsert;

            logs.Add(RbacPermissionAuditLog.Create(
                rolePublicId,
                definition.ResourceKey,
                changedByUserId,
                changeType,
                JsonSerializer.Serialize(before, SerializerOptions),
                JsonSerializer.Serialize(after, SerializerOptions),
                changedAtUtc));
        }

        return logs;
    }

    public static RbacPermissionState Deserialize(string json) =>
        JsonSerializer.Deserialize<RbacPermissionState>(json, SerializerOptions)
        ?? new RbacPermissionState(false, false, false, false, false);

    private static bool HasAnyGrant(RbacPermissionState state) =>
        state.HasAccess || state.CanRead || state.CanCreate || state.CanUpdate || state.CanDelete;
}
