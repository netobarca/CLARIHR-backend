namespace CLARIHR.Application.Features.Auth.Common;

/// <summary>
/// Canonical rate-limit policy names for the anonymous authentication endpoints. Single source of truth
/// shared by the <c>AddRateLimiter</c> registration in <c>Program.cs</c>, the <c>[EnableRateLimiting]</c>
/// attributes on <c>AuthController</c> and the <c>AuthRateLimitingGovernanceTests</c> guardrail — so a
/// limiter cannot drift from the endpoint it protects (AU-2; mirrors the per-feature <c>*RateLimitPolicies</c>
/// classes). All are fixed-window limiters partitioned by remote IP (the caller is unauthenticated here).
/// </summary>
public static class AuthRateLimitPolicies
{
    /// <summary>Login (email + password). 5/min per IP — brute-force / credential-stuffing backstop.</summary>
    public const string Login = "auth-login";

    /// <summary>Local + external (Google) registration / sign-in. 5/min per IP.</summary>
    public const string Register = "auth-register";

    /// <summary>Company-user invitation acceptance. 5/min per IP.</summary>
    public const string InviteAccept = "auth-invite-accept";

    /// <summary>Password-reset email request. 5/min per IP (paired with the uniform 202 anti-enumeration).</summary>
    public const string PasswordResetRequest = "auth-password-reset-request";

    /// <summary>
    /// Password-reset token validate + redeem (the submit side of the flow). 5/min per IP (AU-2): caps token
    /// probing / redemption abuse. Resets are rare, so a tight per-IP limit does not clip legitimate use.
    /// </summary>
    public const string PasswordResetSubmit = "auth-password-reset-submit";

    /// <summary>
    /// Refresh-token exchange. 60/min per IP — deliberately generous: refresh is a frequent legitimate
    /// operation (every ~15 min per active session) and the limiter is IP-partitioned, so a tight cap would
    /// clip users behind shared NAT. The real anti-abuse for refresh is the 512-bit token + rotation /
    /// reuse-detection; this is a per-IP DoS backstop (AU-2).
    /// </summary>
    public const string Refresh = "auth-refresh";

    /// <summary>Email-verification confirm (redeem the registration verification link). 5/min per IP (AU-1).</summary>
    public const string EmailVerificationSubmit = "auth-email-verification-submit";

    /// <summary>Resend the email-verification link. 5/min per IP (paired with the uniform 202 anti-enumeration).</summary>
    public const string EmailVerificationResend = "auth-email-verification-resend";
}
