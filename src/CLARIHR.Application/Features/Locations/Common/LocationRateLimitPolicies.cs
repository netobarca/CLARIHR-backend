namespace CLARIHR.Application.Features.Locations.Common;

/// <summary>
/// Canonical rate-limit policy names for the Locations endpoints. Single source of truth shared by the
/// <c>AddRateLimiter</c> registration in <c>Program.cs</c>, the <c>[EnableRateLimiting]</c> attributes on
/// the Locations controllers, and the <c>LocationRateLimitingGovernanceTests</c> guardrail — so the
/// limiter cannot drift from the endpoints it protects (mirrors
/// <see cref="CLARIHR.Application.Features.CompetencyFramework.Common.CompetencyFrameworkRateLimitPolicies"/>).
/// Both partitioned per user + tenant.
/// </summary>
public static class LocationRateLimitPolicies
{
    /// <summary>Generous per-user+tenant limiter for the paged free-text location-group search / list.</summary>
    public const string Search = "locations-search";

    /// <summary>
    /// Tighter per-user+tenant limiter for the unpaginated full-hierarchy projection (`/tree`) — a
    /// graph read that returns the whole company hierarchy in one shot (§17.3 "graph" target).
    /// </summary>
    public const string Tree = "locations-tree";
}
