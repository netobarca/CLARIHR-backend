namespace CLARIHR.Domain.Auth;

public enum UserStatus
{
    Active = 1,
    Inactive = 2,
    PendingActivation = 3,

    // Self-registered local account whose email has not yet been proven. The account is NOT usable
    // (every auth gate checks `== Active`) until the verification link is redeemed (AU-1).
    PendingEmailVerification = 4
}
