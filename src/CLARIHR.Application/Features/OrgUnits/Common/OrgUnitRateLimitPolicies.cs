namespace CLARIHR.Application.Features.OrgUnits.Common;

/// <summary>
/// Canonical rate-limit policy names for the Org Units endpoints. Single source of truth shared by the
/// <c>AddRateLimiter</c> registration in <c>Program.cs</c>, the <c>[EnableRateLimiting]</c> attributes on
/// <c>OrganizationUnitsController</c>, and the <c>OrgUnitRateLimitingGovernanceTests</c> guardrail — so the
/// limiter cannot drift from the costly endpoints it protects (mirrors
/// <see cref="CLARIHR.Application.Features.Locations.Common.LocationRateLimitPolicies"/> and
/// <see cref="CLARIHR.Application.Features.CostCenters.Common.CostCenterRateLimitPolicies"/>).
/// All partitioned per user + tenant.
/// </summary>
public static class OrgUnitRateLimitPolicies
{
    /// <summary>Generous per-user+tenant limiter for the paged free-text org-unit search / list.</summary>
    public const string Search = "org-units-search";

    /// <summary>
    /// Tighter per-user+tenant limiter for the unpaginated full-hierarchy projections (<c>/tree</c> and
    /// <c>/graph</c>) — graph reads that load the whole company hierarchy in one shot.
    /// </summary>
    public const string Tree = "org-units-tree";

    /// <summary>
    /// Tightest per-user+tenant limiter for the downloadable export artifacts (tabular <c>/export</c> and
    /// the <c>/diagram-export</c> GraphML/DOT/JSON rendering) — the most expensive reads per request.
    /// </summary>
    public const string Export = "org-units-export";
}
