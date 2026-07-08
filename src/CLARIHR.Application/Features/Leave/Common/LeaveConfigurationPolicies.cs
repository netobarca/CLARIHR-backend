namespace CLARIHR.Application.Features.Leave.Common;

/// <summary>
/// Authorization policy names for the leave-configuration masters, referenced by
/// <c>[AuthorizationPolicySet(LeaveConfigurationPolicies.Read, LeaveConfigurationPolicies.Manage)]</c>
/// on the leave-configuration controllers. These are policy identifiers, not RBAC permission
/// strings — the permission codes they assert live in <see cref="LeaveConfigurationPermissionCodes"/>
/// and are wired to these policies in <c>Program.cs</c>. Kept a superset of the precise
/// <c>LeaveConfigurationAuthorizationService</c> handler gate (Read ⊇ EnsureCanReadAsync,
/// Manage ⊇ EnsureCanManageAsync) so a legitimate caller is never falsely 403'd.
/// </summary>
public static class LeaveConfigurationPolicies
{
    public const string Read = "LeaveConfiguration.Read";
    public const string Manage = "LeaveConfiguration.Manage";
}
