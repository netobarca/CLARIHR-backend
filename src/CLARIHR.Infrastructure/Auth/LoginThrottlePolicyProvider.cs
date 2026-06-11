using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CLARIHR.Infrastructure.Auth;

internal sealed class LoginThrottlePolicyProvider(IOptions<LoginThrottleOptions> options) : ILoginThrottlePolicyProvider
{
    public int GetMaxFailedAttempts() => Math.Max(1, options.Value.MaxFailedAttempts);

    public TimeSpan GetWindow() => TimeSpan.FromMinutes(Math.Max(1, options.Value.WindowMinutes));

    public TimeSpan GetLockoutDuration() => TimeSpan.FromMinutes(Math.Max(1, options.Value.LockoutMinutes));
}
