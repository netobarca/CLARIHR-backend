using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.IdentityAccess;

public sealed class IamUserRoleAssignment : TenantEntity
{
    private IamUserRoleAssignment()
    {
    }

    private IamUserRoleAssignment(long roleId)
    {
        if (roleId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(roleId), "Role id must be greater than zero.");
        }

        RoleId = roleId;
    }

    public long UserId { get; private set; }

    public IamUser User { get; private set; } = null!;

    public long RoleId { get; private set; }

    public IamRole Role { get; private set; } = null!;

    public static IamUserRoleAssignment Create(long roleId) => new(roleId);
}
