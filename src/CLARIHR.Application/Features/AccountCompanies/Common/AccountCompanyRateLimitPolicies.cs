namespace CLARIHR.Application.Features.AccountCompanies.Common;

/// <summary>
/// AC-8: canonical rate-limit policy name for the account-company session-switch endpoint. Single source of
/// truth shared by the <c>AddRateLimiter</c> registration in <c>Program.cs</c>, the
/// <c>[EnableRateLimiting]</c> attribute on <c>AccountCompaniesController.Switch</c>, and the
/// <c>AccountCompanyRateLimitingGovernanceTests</c> guardrail. <c>POST .../switch</c> mints a fresh
/// access+refresh token pair (the functional equivalent of login, which is rate-limited), so it gets an
/// auth-style limiter; the rest of the family stays unlimited (ownership-gated, no abuse profile).
/// </summary>
public static class AccountCompanyRateLimitPolicies
{
    /// <summary>Per-user+tenant limiter for the token-minting active-company switch.</summary>
    public const string Switch = "account-company-switch";
}
