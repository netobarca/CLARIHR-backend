using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.IdentityAccess;

public sealed class IamUser : TenantEntity
{
    private readonly List<IamUserRoleAssignment> _roleAssignments = [];

    private IamUser()
    {
    }

    private IamUser(Guid publicId, Guid? linkedUserPublicId, string firstName, string lastName, string email, bool isActive)
    {
        PublicId = publicId;
        LinkedUserPublicId = linkedUserPublicId;
        FirstName = IdentityNormalization.Clean(firstName, nameof(firstName));
        LastName = IdentityNormalization.Clean(lastName, nameof(lastName));
        Email = IdentityNormalization.Clean(email, nameof(email));
        NormalizedEmail = IdentityNormalization.Normalize(email);
        IsActive = isActive;
    }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public Guid? LinkedUserPublicId { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<IamUserRoleAssignment> RoleAssignments => _roleAssignments;

    public static IamUser CreateLinked(Guid linkedUserPublicId, string firstName, string lastName, string email, bool isActive) =>
        new(Guid.NewGuid(), linkedUserPublicId, firstName, lastName, email, isActive);

    public void UpdateProfile(string firstName, string lastName)
    {
        FirstName = IdentityNormalization.Clean(firstName, nameof(firstName));
        LastName = IdentityNormalization.Clean(lastName, nameof(lastName));
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    public void SyncRoles(IEnumerable<IamRole> roles)
    {
        var desiredRoleIds = roles
            .Select(static role => role.Id)
            .ToHashSet();

        _roleAssignments.RemoveAll(assignment => !desiredRoleIds.Contains(assignment.RoleId));

        foreach (var roleId in desiredRoleIds)
        {
            if (_roleAssignments.Any(assignment => assignment.RoleId == roleId))
            {
                continue;
            }

            _roleAssignments.Add(IamUserRoleAssignment.Create(roleId));
        }
    }

    public void EnsureRole(IamRole role)
    {
        if (role.Id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(role), "Role id must be greater than zero.");
        }

        if (_roleAssignments.Any(assignment => assignment.RoleId == role.Id))
        {
            return;
        }

        _roleAssignments.Add(IamUserRoleAssignment.Create(role.Id));
    }
}
