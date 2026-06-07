namespace CLARIHR.Application.Features.LegalRepresentatives.Common;

/// <summary>
/// Canonical rate-limit policy names for the Legal Representatives endpoints. Single source of
/// truth shared by the <c>AddRateLimiter</c> registration in <c>Program.cs</c>, the
/// <c>[EnableRateLimiting]</c> attributes on <c>LegalRepresentativesController</c>, and the
/// <c>LegalRepresentativeRateLimitingGovernanceTests</c> guardrail — so the limiter cannot drift
/// from the endpoints it protects (mirrors the
/// <see cref="CLARIHR.Application.Features.CompetencyFramework.Common.CompetencyFrameworkRateLimitPolicies"/>
/// §X-RATE pattern). Both partitioned per user + tenant.
/// </summary>
public static class LegalRepresentativeRateLimitPolicies
{
    /// <summary>Generous per-user+tenant limiter for the paged search / list reads.</summary>
    public const string Search = "legal-representatives-search";

    /// <summary>Tight per-user+tenant limiter for the unbounded-cost legal-representatives export.</summary>
    public const string Export = "legal-representatives-export";
}
