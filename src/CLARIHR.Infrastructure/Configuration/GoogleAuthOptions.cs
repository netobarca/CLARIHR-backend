namespace CLARIHR.Infrastructure.Configuration;

public sealed class GoogleAuthOptions
{
    public const string SectionName = "Authentication:Google";

    public string? ClientId { get; init; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);
}
