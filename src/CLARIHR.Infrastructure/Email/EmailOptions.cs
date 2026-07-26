namespace CLARIHR.Infrastructure.Email;

/// <summary>
/// Selects the e-mail transport, mirroring how <c>Reporting:Pdf:Engine</c> selects a PDF engine.
/// The default is deliberately <see cref="EmailProviders.Logging"/>: tests, CI and a freshly cloned
/// dev environment must not be able to send real mail by omission.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Provider { get; init; } = EmailProviders.Logging;
}

/// <summary>
/// Known transports. This list is the *only* place provider names live; the content pipeline
/// (templates, subjects, rendering) is shared by all of them.
/// </summary>
public static class EmailProviders
{
    /// <summary>Writes a masked log line instead of sending. Default.</summary>
    public const string Logging = "Logging";

    /// <summary>Brevo transactional e-mail over its HTTP API.</summary>
    public const string Brevo = "Brevo";

    public static IReadOnlyList<string> All => [Logging, Brevo];
}
