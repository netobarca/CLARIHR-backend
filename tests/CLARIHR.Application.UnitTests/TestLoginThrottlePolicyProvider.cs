using CLARIHR.Application.Abstractions.Auth;

namespace CLARIHR.Application.UnitTests;

internal sealed class TestLoginThrottlePolicyProvider(int maxAttempts = 3, int windowMinutes = 15, int lockoutMinutes = 15)
    : ILoginThrottlePolicyProvider
{
    public int GetMaxFailedAttempts() => maxAttempts;

    public TimeSpan GetWindow() => TimeSpan.FromMinutes(windowMinutes);

    public TimeSpan GetLockoutDuration() => TimeSpan.FromMinutes(lockoutMinutes);
}
