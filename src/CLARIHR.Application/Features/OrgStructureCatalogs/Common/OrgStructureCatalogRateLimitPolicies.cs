namespace CLARIHR.Application.Features.OrgStructureCatalogs.Common;

/// <summary>
/// Canonical rate-limit policy names for the Organization Structure Catalogs endpoints. Single source of
/// truth shared by the <c>AddRateLimiter</c> registration in <c>Program.cs</c>, the
/// <c>[EnableRateLimiting]</c> attributes on <c>OrganizationStructureCatalogsController</c>, and the
/// <c>OrgStructureCatalogRateLimitingGovernanceTests</c> guardrail — so the limiter cannot drift from the
/// paged search endpoints it protects (mirrors
/// <see cref="CLARIHR.Application.Features.OrgUnits.Common.OrgUnitRateLimitPolicies"/>).
/// Partitioned per user + tenant.
/// </summary>
public static class OrgStructureCatalogRateLimitPolicies
{
    /// <summary>
    /// Generous per-user+tenant limiter for the paged free-text unit-types / functional-areas search.
    /// </summary>
    public const string Search = "org-structure-catalogs-search";
}
