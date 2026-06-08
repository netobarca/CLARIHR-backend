namespace CLARIHR.Application.Features.Reports.Common;

/// <summary>
/// REX-E: canonical rate-limit policy names for the report-export-jobs endpoints. Single source of
/// truth shared by the <c>AddRateLimiter</c> registration in <c>Program.cs</c>, the
/// <c>[EnableRateLimiting]</c> attributes on <c>ReportExportJobsController</c> and the
/// <c>ReportExportJobRateLimitingGovernanceTests</c> guardrail — so the limiter cannot drift from the
/// costly endpoints it protects (mirrors the other family policy classes). All partitioned per
/// user + tenant.
/// </summary>
public static class ReportExportJobRateLimitPolicies
{
    /// <summary>Generous per-user+tenant limiter for the paged export-jobs search / list.</summary>
    public const string Search = "report-export-jobs-search";

    /// <summary>
    /// Tighter per-user+tenant limiter for the artifact download (streams a stored blob) — the most
    /// expensive read per request.
    /// </summary>
    public const string Download = "report-export-jobs-download";
}
