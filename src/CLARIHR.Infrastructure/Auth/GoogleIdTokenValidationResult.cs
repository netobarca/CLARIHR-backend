namespace CLARIHR.Infrastructure.Auth;

internal sealed record GoogleIdTokenValidationResult(
    string Subject,
    string? Email,
    bool EmailVerified,
    string? GivenName,
    string? FamilyName,
    string? Name,
    string? HostedDomain);
