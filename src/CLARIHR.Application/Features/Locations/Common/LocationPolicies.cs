namespace CLARIHR.Application.Features.Locations.Common;

/// <summary>
/// Authorization policy names for the shared Locations domain (work centers, work center types,
/// location groups, location levels, location hierarchy), referenced by
/// <c>[AuthorizationPolicySet(LocationPolicies.Read, LocationPolicies.Manage)]</c> on the Locations
/// controllers. These are policy identifiers, not RBAC permission strings — the permission codes
/// they assert live in <see cref="LocationPermissionCodes"/> and are wired to these policies in
/// <c>Program.cs</c>. Kept a superset of the precise <c>ILocationAuthorizationService</c> handler
/// gate (Read ⊇ EnsureCanReadAsync, Manage ⊇ EnsureCanManageAsync) so a legitimate caller is never
/// falsely 403'd. Shared across every Locations controller (one policy pair for the whole domain).
/// </summary>
public static class LocationPolicies
{
    public const string Read = "Locations.Read";
    public const string Manage = "Locations.Manage";
}
