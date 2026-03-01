using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.IdentityAccess;

public sealed class RbacResource : AuditableEntity
{
    private RbacResource()
    {
    }

    private RbacResource(string resourceKey, string displayName, bool isActive)
    {
        ResourceKey = IdentityNormalization.Clean(resourceKey, nameof(resourceKey));
        NormalizedResourceKey = IdentityNormalization.Normalize(resourceKey);
        DisplayName = IdentityNormalization.Clean(displayName, nameof(displayName));
        IsActive = isActive;
    }

    public string ResourceKey { get; private set; } = string.Empty;

    public string NormalizedResourceKey { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public static RbacResource Create(string resourceKey, string displayName, bool isActive = true) =>
        new(resourceKey, displayName, isActive);

    public void Update(string displayName, bool isActive)
    {
        DisplayName = IdentityNormalization.Clean(displayName, nameof(displayName));
        IsActive = isActive;
    }
}
