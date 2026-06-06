namespace CLARIHR.Application.Features.CompanyUsers.Common;

/// <summary>
/// Canonical rate-limit policy names for the Company Users endpoints. Single source of truth shared
/// by the <c>AddRateLimiter</c> registration in <c>Program.cs</c>, the <c>[EnableRateLimiting]</c>
/// attributes on <c>CompanyUsersController</c>, and the
/// <c>CompanyUserRateLimitingGovernanceTests</c> guardrail — so the limiter cannot drift from the
/// endpoints it protects (mirrors the
/// <see cref="CLARIHR.Application.Features.CompetencyFramework.Common.CompetencyFrameworkRateLimitPolicies"/>
/// pattern). Both partitioned per user + tenant.
/// </summary>
public static class CompanyUserRateLimitPolicies
{
    /// <summary>Generous per-user+tenant limiter for the paged company-users search / list.</summary>
    public const string Search = "company-users-search";

    /// <summary>
    /// Tight per-user+tenant limiter for the invitation e-mail senders (invite + reset-invitation) —
    /// same abuse class as <c>auth-invite-accept</c> (e-mail bomb / cross-tenant enumeration).
    /// </summary>
    public const string Invite = "company-users-invite";
}
