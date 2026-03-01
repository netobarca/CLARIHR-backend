using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.IdentityAccess;

public sealed class IamRolePermissionAssignment : TenantEntity
{
    private IamRolePermissionAssignment()
    {
    }

    private IamRolePermissionAssignment(long permissionId)
    {
        if (permissionId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(permissionId), "Permission id must be greater than zero.");
        }

        PermissionId = permissionId;
    }

    public long RoleId { get; private set; }

    public IamRole Role { get; private set; } = null!;

    public long PermissionId { get; private set; }

    public IamPermission Permission { get; private set; } = null!;

    public static IamRolePermissionAssignment Create(long permissionId) => new(permissionId);
}
