namespace CLARIHR.Infrastructure.Configuration;

public sealed class EmailVerificationOptions
{
    public const string SectionName = "Authentication:EmailVerification";

    public string FrontendVerifyUrl { get; init; } = "http://localhost:3000/verify-email";

    public int TokenLifetimeMinutes { get; init; } = 60;

    public int RequestCooldownMinutes { get; init; } = 2;
}
