namespace CLARIHR.Application.Abstractions.Auth;

// AU-4: configuration for the per-account login throttle (max failed attempts within a sliding window
// before a temporary lockout). Mirrors IPasswordResetPolicyProvider — abstraction here, options-backed
// implementation in Infrastructure.
public interface ILoginThrottlePolicyProvider
{
    int GetMaxFailedAttempts();

    TimeSpan GetWindow();

    TimeSpan GetLockoutDuration();
}
