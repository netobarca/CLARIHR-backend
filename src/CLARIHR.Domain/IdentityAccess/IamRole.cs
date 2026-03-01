using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.IdentityAccess;

public sealed class IamRole : TenantEntity
{
    private readonly List<IamRolePermissionAssignment> _permissionAssignments = [];
    private readonly List<IamUserRoleAssignment> _userAssignments = [];

    private IamRole()
    {
    }

    private IamRole(Guid publicId, string name, string? description, bool isSystemRole)
    {
        PublicId = publicId;
        Name = IdentityNormalization.Clean(name, nameof(name));
        NormalizedName = IdentityNormalization.Normalize(name);
        Description = IdentityNormalization.CleanOptional(description);
        IsSystemRole = isSystemRole;
    }

    public Guid PublicId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsSystemRole { get; private set; }

    public IReadOnlyCollection<IamRolePermissionAssignment> PermissionAssignments => _permissionAssignments;

    public IReadOnlyCollection<IamUserRoleAssignment> UserAssignments => _userAssignments;

    public static IamRole Create(string name, string? description, bool isSystemRole = false) =>
        new(Guid.NewGuid(), name, description, isSystemRole);

    public IamRole Clone(string name, string? description) =>
        Create(name, description ?? Description, isSystemRole: false);

    public void UpdateDetails(string name, string? description)
    {
        Name = IdentityNormalization.Clean(name, nameof(name));
        NormalizedName = IdentityNormalization.Normalize(name);
        Description = IdentityNormalization.CleanOptional(description);
    }

    public void SyncPermissions(IEnumerable<IamPermission> permissions)
    {
        var desiredPermissionIds = permissions
            .Select(static permission => permission.Id)
            .ToHashSet();

        _permissionAssignments.RemoveAll(assignment => !desiredPermissionIds.Contains(assignment.PermissionId));

        foreach (var permissionId in desiredPermissionIds)
        {
            if (_permissionAssignments.Any(assignment => assignment.PermissionId == permissionId))
            {
                continue;
            }

            _permissionAssignments.Add(IamRolePermissionAssignment.Create(permissionId));
        }
    }
}
