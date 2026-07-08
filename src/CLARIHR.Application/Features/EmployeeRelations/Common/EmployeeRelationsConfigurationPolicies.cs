namespace CLARIHR.Application.Features.EmployeeRelations.Common;

/// <summary>
/// Authorization policy names for the employee-relations configuration masters (recognition types,
/// disciplinary-action types, disciplinary-action causes — REQ-003), referenced by
/// <c>[AuthorizationPolicySet(EmployeeRelationsConfigurationPolicies.Read, EmployeeRelationsConfigurationPolicies.Manage)]</c>
/// on the governed controllers. These are policy identifiers, not RBAC permission strings — the
/// permission codes they assert live in <see cref="EmployeeRelationsConfigurationPermissionCodes"/>
/// and are wired to these policies in <c>Program.cs</c>. Kept a superset of the precise
/// <c>EmployeeRelationsConfigurationAuthorizationService</c> handler gate (Read ⊇ EnsureCanReadAsync,
/// Manage ⊇ EnsureCanManageAsync) so a legitimate caller is never falsely 403'd. Mirrors
/// <c>LeaveConfigurationPolicies</c>.
/// </summary>
public static class EmployeeRelationsConfigurationPolicies
{
    public const string Read = "EmployeeRelationsConfiguration.Read";
    public const string Manage = "EmployeeRelationsConfiguration.Manage";
}
