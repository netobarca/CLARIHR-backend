using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Companies;

public sealed class UserCompanyMembership : AuditableEntity
{
    private UserCompanyMembership()
    {
    }

    private UserCompanyMembership(long userId, long companyId, long roleId, bool isPrimary, UserCompanyStatus status)
    {
        if (userId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId), "User id must be greater than zero.");
        }

        if (companyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyId), "Company id must be greater than zero.");
        }

        if (roleId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(roleId), "Role id must be greater than zero.");
        }

        UserId = userId;
        CompanyId = companyId;
        RoleId = roleId;
        IsPrimary = isPrimary;
        Status = status;
    }

    public long UserId { get; private set; }

    public long CompanyId { get; private set; }

    public long RoleId { get; private set; }

    public bool IsPrimary { get; private set; }

    public UserCompanyStatus Status { get; private set; }

    public static UserCompanyMembership Create(long userId, long companyId, long roleId, bool isPrimary) =>
        new(userId, companyId, roleId, isPrimary, UserCompanyStatus.Active);

    public void ChangeRole(long roleId)
    {
        if (roleId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(roleId), "Role id must be greater than zero.");
        }

        RoleId = roleId;
    }

    public void Deactivate() => Status = UserCompanyStatus.Inactive;

    public void Reactivate() => Status = UserCompanyStatus.Active;

    public void MarkPrimary() => IsPrimary = true;

    public void ClearPrimary() => IsPrimary = false;
}
