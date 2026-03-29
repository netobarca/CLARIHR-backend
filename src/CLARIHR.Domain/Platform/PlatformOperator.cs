using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Platform;

public sealed class PlatformOperator : AuditableEntity
{
    private PlatformOperator()
    {
    }

    private PlatformOperator(long userId, PlatformOperatorRole role)
    {
        if (userId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId), "User id must be a persisted positive identifier.");
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), "Platform operator role is invalid.");
        }

        UserId = userId;
        Role = role;
        IsActive = true;
    }

    public long UserId { get; private set; }

    public PlatformOperatorRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public static PlatformOperator Create(long userId, PlatformOperatorRole role) =>
        new(userId, role);

    public void ChangeRole(PlatformOperatorRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), "Platform operator role is invalid.");
        }

        Role = role;
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}
