namespace CLARIHR.Application.Features.PositionSlots.Common;

/// <summary>
/// Canonical rate-limit policy names for the Position Slots endpoints. Single source of
/// truth shared by the <c>AddRateLimiter</c> registration in <c>Program.cs</c>, the
/// <c>[EnableRateLimiting]</c> attributes on <c>PositionSlotsController</c>, and the
/// <c>RateLimitingGovernanceTests</c> guardrail — so the limiter cannot drift from the
/// endpoints it protects (mirrors the <see cref="PositionSlotPolicies"/> §X-AUTHZ pattern).
/// </summary>
public static class PositionSlotRateLimitPolicies
{
    /// <summary>Tight per-user+tenant limiter for the unbounded-cost generators
    /// (export, diagram-export, full-tenant graph).</summary>
    public const string Export = "position-slots-export";

    /// <summary>Generous per-user+tenant limiter for the paged search/list endpoint
    /// (mirrors <c>personnel-files-search</c>).</summary>
    public const string Search = "position-slots-search";
}
