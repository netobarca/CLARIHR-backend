namespace CLARIHR.Infrastructure.Configuration;

public sealed class JwtTokenOptions
{
    public const string SectionName = "Authentication:Jwt";

    public string? Issuer { get; init; }

    public string? Audience { get; init; }

    public string? PlatformAudience { get; init; }

    public string? SigningKey { get; init; }

    public int AccessTokenExpirationMinutes { get; init; } = 15;

    public int RefreshTokenExpirationDays { get; init; } = 14;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Issuer) &&
        !string.IsNullOrWhiteSpace(Audience) &&
        !string.IsNullOrWhiteSpace(PlatformAudience) &&
        !string.IsNullOrWhiteSpace(SigningKey);
}
