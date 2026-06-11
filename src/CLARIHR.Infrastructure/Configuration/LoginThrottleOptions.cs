namespace CLARIHR.Infrastructure.Configuration;

public sealed class LoginThrottleOptions
{
    public const string SectionName = "Authentication:LoginThrottle";

    public int MaxFailedAttempts { get; init; } = 10;

    public int WindowMinutes { get; init; } = 15;

    public int LockoutMinutes { get; init; } = 15;
}
