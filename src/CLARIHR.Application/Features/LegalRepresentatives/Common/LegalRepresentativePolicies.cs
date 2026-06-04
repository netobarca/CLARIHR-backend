namespace CLARIHR.Application.Features.LegalRepresentatives.Common;

/// <summary>
/// Authorization policy names for the Legal Representatives domain, referenced by
/// <c>[AuthorizationPolicySet(LegalRepresentativePolicies.Read, LegalRepresentativePolicies.Manage)]</c>
/// on <c>LegalRepresentativesController</c>. These are policy identifiers, not RBAC permission
/// strings — the permission codes they assert live in <see cref="LegalRepresentativePermissionCodes"/>
/// and are wired to these policies in <c>Program.cs</c>. Kept a superset of the precise
/// <c>LegalRepresentativeAuthorizationService</c> handler gate (Read ⊇ EnsureCanReadAsync, Manage ⊇
/// EnsureCanManageAsync) so a legitimate caller is never falsely 403'd.
/// </summary>
public static class LegalRepresentativePolicies
{
    public const string Read = "LegalRepresentatives.Read";
    public const string Manage = "LegalRepresentatives.Manage";
}
