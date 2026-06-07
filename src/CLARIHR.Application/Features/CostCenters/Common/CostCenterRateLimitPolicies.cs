namespace CLARIHR.Application.Features.CostCenters.Common;

/// <summary>
/// Canonical rate-limit policy names for the Cost Centers endpoints. Single source of truth shared
/// by the <c>AddRateLimiter</c> registration in <c>Program.cs</c>, the <c>[EnableRateLimiting]</c>
/// attributes on <c>CostCentersController</c>, and the <c>CostCenterRateLimitingGovernanceTests</c>
/// guardrail — so the limiter cannot drift from the endpoints it protects (mirrors the
/// <see cref="CLARIHR.Application.Features.LegalRepresentatives.Common.LegalRepresentativeRateLimitPolicies"/>
/// §X-RATE pattern). Both partitioned per user + tenant.
/// </summary>
public static class CostCenterRateLimitPolicies
{
    /// <summary>Generous per-user+tenant limiter for the paged search / list reads.</summary>
    public const string Search = "cost-centers-search";

    /// <summary>Tight per-user+tenant limiter for the unbounded-cost cost-centers export.</summary>
    public const string Export = "cost-centers-export";
}
