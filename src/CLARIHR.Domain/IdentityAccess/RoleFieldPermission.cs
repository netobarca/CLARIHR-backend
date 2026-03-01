using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.IdentityAccess;

public sealed class RoleFieldPermission : TenantEntity
{
    private RoleFieldPermission()
    {
    }

    private RoleFieldPermission(
        long roleId,
        string fieldKey,
        bool isVisible,
        bool isEditable,
        bool isRequired,
        bool isMasked,
        Guid updatedByUserId,
        DateTime updatedAtUtc)
    {
        RoleId = roleId;
        FieldKey = IdentityNormalization.Clean(fieldKey, nameof(fieldKey));
        NormalizedFieldKey = IdentityNormalization.Normalize(fieldKey);
        Apply(isVisible, isEditable, isRequired, isMasked, updatedByUserId, updatedAtUtc);
    }

    public long RoleId { get; private set; }

    public string FieldKey { get; private set; } = string.Empty;

    public string NormalizedFieldKey { get; private set; } = string.Empty;

    public bool IsVisible { get; private set; }

    public bool IsEditable { get; private set; }

    public bool IsRequired { get; private set; }

    public bool IsMasked { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public static RoleFieldPermission Create(
        long roleId,
        string fieldKey,
        bool isVisible,
        bool isEditable,
        bool isRequired,
        bool isMasked,
        Guid updatedByUserId,
        DateTime updatedAtUtc) =>
        new(roleId, fieldKey, isVisible, isEditable, isRequired, isMasked, updatedByUserId, updatedAtUtc);

    public void Update(
        bool isVisible,
        bool isEditable,
        bool isRequired,
        bool isMasked,
        Guid updatedByUserId,
        DateTime updatedAtUtc) =>
        Apply(isVisible, isEditable, isRequired, isMasked, updatedByUserId, updatedAtUtc);

    private void Apply(
        bool isVisible,
        bool isEditable,
        bool isRequired,
        bool isMasked,
        Guid updatedByUserId,
        DateTime updatedAtUtc)
    {
        IsVisible = isVisible;
        IsEditable = isVisible && isEditable;
        IsRequired = isVisible && isRequired;
        IsMasked = isVisible && isMasked;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = updatedAtUtc;
    }
}
