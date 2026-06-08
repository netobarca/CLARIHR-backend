namespace CLARIHR.Application.Features.SalaryTabulator.Common;

/// <summary>
/// Canonical rate-limit policy names for the Salary Tabulator endpoints (ST-A). Single source of
/// truth shared by the <c>AddRateLimiter</c> registration in <c>Program.cs</c>, the
/// <c>[EnableRateLimiting]</c> attributes on <c>SalaryTabulatorController</c>, and the
/// <c>SalaryTabulatorRateLimitingGovernanceTests</c> guardrail — so the limiter cannot drift from
/// the endpoints it protects. Mirrors <see cref="CLARIHR.Application.Features.CostCenters.Common.CostCenterRateLimitPolicies"/>.
/// Prioritised because the export surfaces salary data (PII): a per-user+tenant guard blocks
/// scraping of the most sensitive data in the system.
/// </summary>
public static class SalaryTabulatorRateLimitPolicies
{
    /// <summary>Tight per-user+tenant limiter for the unbounded-cost PII export of salary lines.</summary>
    public const string Export = "salary-tabulator-export";

    /// <summary>Generous per-user+tenant limiter for the paged search/list endpoints
    /// (salary lines and change requests).</summary>
    public const string Search = "salary-tabulator-search";
}
