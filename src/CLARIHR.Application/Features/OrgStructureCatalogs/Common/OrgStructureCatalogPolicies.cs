namespace CLARIHR.Application.Features.OrgStructureCatalogs.Common;

/// <summary>
/// Authorization policy names for the Organization Structure Catalogs domain, referenced by
/// <c>[AuthorizationPolicySet(OrgStructureCatalogPolicies.Read, OrgStructureCatalogPolicies.Manage)]</c>
/// on <c>OrganizationStructureCatalogsController</c>. These are policy identifiers, not RBAC permission
/// strings — the permission codes they assert live in <see cref="OrgStructureCatalogPermissionCodes"/>
/// and are wired to these policies in <c>Program.cs</c>. Kept a superset of the precise
/// <c>OrgStructureCatalogAuthorizationService</c> handler gate (Read ⊇ EnsureCanReadTenantAsync,
/// Manage ⊇ EnsureCanManageTenantAsync) so a legitimate caller is never falsely 403'd — including the
/// OrgUnits.* fallback (whoever administers org units administers their catalogs).
/// </summary>
public static class OrgStructureCatalogPolicies
{
    public const string Read = "OrgStructureCatalogs.Read";
    public const string Manage = "OrgStructureCatalogs.Manage";
}
