namespace CLARIHR.Infrastructure.Email.Providers.Brevo;

/// <summary>
/// Brevo transport settings — connection and identity only. There are deliberately **no template
/// ids** here: the templates are ours (see <c>Email/Templates</c>), so this section has the same
/// shape any other provider would need, and switching provider is a config change plus one class.
///
/// <para><see cref="ApiKey"/> is a secret and must arrive as the environment variable
/// <c>Email__Brevo__ApiKey</c> (Application Setting in Azure), never committed.</para>
/// </summary>
public sealed class BrevoOptions
{
    public const string SectionName = "Email:Brevo";

    public string ApiKey { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = "https://api.brevo.com/";

    /// <summary>Display name of the sender. Must belong to a domain verified in Brevo.</summary>
    public string SenderName { get; init; } = "CLARIHR";

    /// <summary>Sender address. Brevo rejects the send if it is not a verified sender.</summary>
    public string SenderEmail { get; init; } = string.Empty;

    public int TimeoutSeconds { get; init; } = 10;

    /// <summary>Retries for transient failures (5xx, timeout, 429). Other 4xx are never retried.</summary>
    public int MaxRetries { get; init; } = 2;

    public TimeSpan NormalizedTimeout => TimeSpan.FromSeconds(Math.Clamp(TimeoutSeconds, 1, 120));

    public int NormalizedMaxRetries => Math.Clamp(MaxRetries, 0, 5);
}
