namespace CLARIHR.Application.Features.OrgUnits.Common;

/// <summary>
/// Authorization policy names for the Org Units domain, referenced by
/// <c>[AuthorizationPolicySet(OrgUnitPolicies.Read, OrgUnitPolicies.Manage)]</c> on
/// <c>OrganizationUnitsController</c>. These are policy identifiers, not RBAC permission strings — the
/// permission codes they assert live in <see cref="OrgUnitPermissionCodes"/> and are wired to these
/// policies in <c>Program.cs</c>. Kept a superset of the precise <c>OrgUnitAuthorizationService</c>
/// handler gate (Read ⊇ EnsureCanReadAsync, Manage ⊇ EnsureCanManageAsync) so a legitimate caller is
/// never falsely 403'd.
/// </summary>
public static class OrgUnitPolicies
{
    public const string Read = "OrgUnits.Read";
    public const string Manage = "OrgUnits.Manage";
}
