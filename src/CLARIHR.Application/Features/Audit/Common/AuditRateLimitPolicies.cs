namespace CLARIHR.Application.Features.Audit.Common;

/// <summary>
/// Canonical rate-limit policy names for the audit-log endpoints. Single source of truth shared by the
/// <c>AddRateLimiter</c> registration in <c>Program.cs</c>, the <c>[EnableRateLimiting]</c> attribute on
/// <c>AuditController</c> and the <c>AuditRateLimitingGovernanceTests</c> guardrail — so the limiter
/// cannot drift from the costly endpoint it protects (mirrors the other family policy classes).
/// Partitioned per user + tenant.
/// </summary>
public static class AuditRateLimitPolicies
{
    /// <summary>
    /// Generous per-user+tenant limiter for the paged free-text audit-log search/list (scan +
    /// <c>LIKE '%x%'</c> over the append-only audit trail). Same paged-search abuse class as the other
    /// read surfaces; mirrors the 120/min family default.
    /// </summary>
    public const string Search = "audit-logs-search";
}
