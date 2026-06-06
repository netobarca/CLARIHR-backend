namespace CLARIHR.Application.Features.CompetencyFramework.Common;

/// <summary>
/// Canonical rate-limit policy names for the Competency Framework endpoints. Single source of
/// truth shared by the <c>AddRateLimiter</c> registration in <c>Program.cs</c>, the
/// <c>[EnableRateLimiting]</c> attributes on the Competency Framework controllers, and the
/// <c>CompetencyFrameworkRateLimitingGovernanceTests</c> guardrail — so the limiter cannot drift
/// from the endpoints it protects (mirrors the
/// <see cref="CLARIHR.Application.Features.PositionSlots.Common.PositionSlotRateLimitPolicies"/>
/// §X-RATE pattern). Both partitioned per user + tenant.
/// </summary>
public static class CompetencyFrameworkRateLimitPolicies
{
    /// <summary>Generous per-user+tenant limiter for the paged search / list reads.</summary>
    public const string Search = "competency-framework-search";

    /// <summary>Tight per-user+tenant limiter for the unbounded-cost competency-matrix export.</summary>
    public const string Export = "competency-framework-export";
}
