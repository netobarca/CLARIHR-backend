using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.IdentityAccess;

namespace CLARIHR.Application.Features.CompanyUsers.Common;

internal static class CompanyUserAuditMapper
{
    public static object CreateSnapshot(
        User user,
        UserCompanyMembership membership,
        IReadOnlyCollection<IamRole> roles) =>
        CreateSnapshot(
            user.PublicId,
            user.Email,
            user.FirstName,
            user.LastName,
            MapRoles(roles),
            user.Status.ToString(),
            membership.Status.ToString());

    public static object CreateSnapshot(
        Guid userId,
        string email,
        string firstName,
        string lastName,
        IReadOnlyCollection<CompanyUserRoleResponse> roles,
        string status,
        string membershipStatus) =>
        new
        {
            userId,
            email,
            firstName,
            lastName,
            roles,
            status,
            membershipStatus
        };

    public static object CreateInvitationSnapshot(
        User user,
        UserCompanyMembership membership,
        IReadOnlyCollection<IamRole> roles,
        DateTime invitationExpiresUtc) =>
        CreateInvitationSnapshot(
            user.PublicId,
            user.Email,
            user.FirstName,
            user.LastName,
            MapRoles(roles),
            user.Status.ToString(),
            membership.Status.ToString(),
            invitationExpiresUtc);

    public static object CreateInvitationSnapshot(
        Guid userId,
        string email,
        string firstName,
        string lastName,
        IReadOnlyCollection<CompanyUserRoleResponse> roles,
        string status,
        string membershipStatus,
        DateTime invitationExpiresUtc) =>
        new
        {
            userId,
            email,
            firstName,
            lastName,
            roles,
            status,
            membershipStatus,
            invitationExpiresUtc
        };

    public static IReadOnlyDictionary<string, object> CreateUpdateDiff(
        string beforeFirstName,
        string afterFirstName,
        string beforeLastName,
        string afterLastName,
        IReadOnlyCollection<CompanyUserRoleResponse> beforeRoles,
        IReadOnlyCollection<CompanyUserRoleResponse> afterRoles)
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

        var beforeRoleIds = beforeRoles
            .Select(static role => role.Id)
            .OrderBy(static roleId => roleId, Comparer<Guid>.Default)
            .ToArray();
        var afterRoleIds = afterRoles
            .Select(static role => role.Id)
            .OrderBy(static roleId => roleId, Comparer<Guid>.Default)
            .ToArray();

        if (!beforeRoleIds.SequenceEqual(afterRoleIds))
        {
            diff["roles"] = AuditPayloads.Change(
                beforeRoles.Select(static role => role.Name).ToArray(),
                afterRoles.Select(static role => role.Name).ToArray());
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

    public static IReadOnlyCollection<CompanyUserRoleResponse> MapRoles(IEnumerable<IamRole> roles) =>
        roles
            .OrderBy(static role => role.Name, StringComparer.Ordinal)
            .Select(static role => new CompanyUserRoleResponse(
                role.PublicId,
                role.Name,
                role.Description,
                role.IsSystemRole))
            .ToArray();
}
