using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.IdentityAccess;

namespace CLARIHR.Application.Features.CompanyUsers.Common;

internal static class CompanyUserAuditMapper
{
    public static object CreateSnapshot(User user, UserCompanyMembership membership, IamRole role) =>
        CreateSnapshot(
            user.PublicId,
            user.Email,
            user.FirstName,
            user.LastName,
            role.PublicId,
            role.Name,
            user.Status.ToString(),
            membership.Status.ToString());

    public static object CreateSnapshot(
        Guid userId,
        string email,
        string firstName,
        string lastName,
        Guid roleId,
        string roleName,
        string status,
        string membershipStatus) =>
        new
        {
            userId,
            email,
            firstName,
            lastName,
            roleId,
            roleName,
            status,
            membershipStatus
        };

    public static object CreateInvitationSnapshot(
        User user,
        UserCompanyMembership membership,
        IamRole role,
        DateTime invitationExpiresUtc) =>
        CreateInvitationSnapshot(
            user.PublicId,
            user.Email,
            user.FirstName,
            user.LastName,
            role.PublicId,
            role.Name,
            user.Status.ToString(),
            membership.Status.ToString(),
            invitationExpiresUtc);

    public static object CreateInvitationSnapshot(
        Guid userId,
        string email,
        string firstName,
        string lastName,
        Guid roleId,
        string roleName,
        string status,
        string membershipStatus,
        DateTime invitationExpiresUtc) =>
        new
        {
            userId,
            email,
            firstName,
            lastName,
            roleId,
            roleName,
            status,
            membershipStatus,
            invitationExpiresUtc
        };

    public static IReadOnlyDictionary<string, object> CreateUpdateDiff(
        string beforeFirstName,
        string afterFirstName,
        string beforeLastName,
        string afterLastName,
        Guid beforeRoleId,
        Guid afterRoleId,
        string beforeRoleName,
        string afterRoleName)
    {
        var diff = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (!string.Equals(beforeFirstName, afterFirstName, StringComparison.Ordinal))
        {
            diff["firstName"] = AuditPayloads.Change(beforeFirstName, afterFirstName);
        }

        if (!string.Equals(beforeLastName, afterLastName, StringComparison.Ordinal))
        {
            diff["lastName"] = AuditPayloads.Change(beforeLastName, afterLastName);
        }

        if (beforeRoleId != afterRoleId)
        {
            diff["roleId"] = AuditPayloads.Change(beforeRoleId, afterRoleId);
            diff["roleName"] = AuditPayloads.Change(beforeRoleName, afterRoleName);
        }

        return diff;
    }

    public static IReadOnlyDictionary<string, object> CreateStatusDiff(
        string beforeStatus,
        string afterStatus,
        string beforeMembershipStatus,
        string afterMembershipStatus)
    {
        var diff = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["status"] = AuditPayloads.Change(beforeStatus, afterStatus),
            ["membershipStatus"] = AuditPayloads.Change(beforeMembershipStatus, afterMembershipStatus)
        };

        return diff;
    }
}
