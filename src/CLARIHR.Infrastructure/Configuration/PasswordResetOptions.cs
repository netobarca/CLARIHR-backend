namespace CLARIHR.Infrastructure.Configuration;

public sealed class PasswordResetOptions
{
    public const string SectionName = "Authentication:PasswordReset";

    public string FrontendResetUrl { get; init; } = "http://localhost:3000/reset-password";

    public int TokenLifetimeMinutes { get; init; } = 15;

    public int RequestCooldownMinutes { get; init; } = 2;
}
