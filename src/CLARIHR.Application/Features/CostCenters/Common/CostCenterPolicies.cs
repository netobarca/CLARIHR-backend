namespace CLARIHR.Application.Features.CostCenters.Common;

/// <summary>
/// Authorization policy names for the Cost Centers domain, referenced by
/// <c>[AuthorizationPolicySet(CostCenterPolicies.Read, CostCenterPolicies.Manage)]</c> on
/// <c>CostCentersController</c>. These are policy identifiers, not RBAC permission strings —
/// the permission codes they assert live in <see cref="CostCenterPermissionCodes"/> and are
/// wired to these policies in <c>Program.cs</c>. Kept a superset of the precise
/// <c>CostCenterAuthorizationService</c> handler gate (Read ⊇ EnsureCanReadAsync, Manage ⊇
/// EnsureCanManageAsync) so a legitimate caller is never falsely 403'd.
/// </summary>
public static class CostCenterPolicies
{
    public const string Read = "CostCenters.Read";
    public const string Manage = "CostCenters.Manage";
}
