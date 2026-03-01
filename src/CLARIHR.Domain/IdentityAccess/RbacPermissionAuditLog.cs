using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.IdentityAccess;

public enum RbacPermissionAuditChangeType
{
    Upsert = 1,
    Delete = 2,
    Disable = 3
}

public sealed class RbacPermissionAuditLog : TenantEntity
{
    private RbacPermissionAuditLog()
    {
    }

    private RbacPermissionAuditLog(
        Guid rolePublicId,
        string resourceKey,
        Guid changedByUserId,
        RbacPermissionAuditChangeType changeType,
        string beforeJson,
        string afterJson,
        DateTime changedAtUtc)
    {
        RolePublicId = rolePublicId;
        ResourceKey = IdentityNormalization.Clean(resourceKey, nameof(resourceKey));
        NormalizedResourceKey = IdentityNormalization.Normalize(resourceKey);
        ChangedByUserId = changedByUserId;
        ChangeType = changeType;
        BeforeJson = beforeJson ?? throw new ArgumentNullException(nameof(beforeJson));
        AfterJson = afterJson ?? throw new ArgumentNullException(nameof(afterJson));
        ChangedAtUtc = changedAtUtc;
    }

    public Guid RolePublicId { get; private set; }

    public string ResourceKey { get; private set; } = string.Empty;

    public string NormalizedResourceKey { get; private set; } = string.Empty;

    public Guid ChangedByUserId { get; private set; }

    public RbacPermissionAuditChangeType ChangeType { get; private set; }

    public string BeforeJson { get; private set; } = string.Empty;

    public string AfterJson { get; private set; } = string.Empty;

    public DateTime ChangedAtUtc { get; private set; }

    public static RbacPermissionAuditLog Create(
        Guid rolePublicId,
        string resourceKey,
        Guid changedByUserId,
        RbacPermissionAuditChangeType changeType,
        string beforeJson,
        string afterJson,
        DateTime changedAtUtc) =>
        new(rolePublicId, resourceKey, changedByUserId, changeType, beforeJson, afterJson, changedAtUtc);
}
