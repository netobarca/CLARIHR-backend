using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.IdentityAccess;

public sealed class FieldPermissionAuditLog : TenantEntity
{
    private FieldPermissionAuditLog()
    {
    }

    private FieldPermissionAuditLog(
        Guid rolePublicId,
        string fieldKey,
        Guid changedByUserId,
        string? beforeJson,
        string afterJson,
        DateTime changedAtUtc)
    {
        RolePublicId = rolePublicId;
        FieldKey = IdentityNormalization.Clean(fieldKey, nameof(fieldKey));
        NormalizedFieldKey = IdentityNormalization.Normalize(fieldKey);
        ChangedByUserId = changedByUserId;
        BeforeJson = IdentityNormalization.CleanOptional(beforeJson);
        AfterJson = IdentityNormalization.Clean(afterJson, nameof(afterJson));
        ChangedAtUtc = changedAtUtc;
    }

    public Guid RolePublicId { get; private set; }

    public string FieldKey { get; private set; } = string.Empty;

    public string NormalizedFieldKey { get; private set; } = string.Empty;

    public Guid ChangedByUserId { get; private set; }

    public string? BeforeJson { get; private set; }

    public string AfterJson { get; private set; } = string.Empty;

    public DateTime ChangedAtUtc { get; private set; }

    public static FieldPermissionAuditLog Create(
        Guid rolePublicId,
        string fieldKey,
        Guid changedByUserId,
        string? beforeJson,
        string afterJson,
        DateTime changedAtUtc) =>
        new(rolePublicId, fieldKey, changedByUserId, beforeJson, afterJson, changedAtUtc);
}
