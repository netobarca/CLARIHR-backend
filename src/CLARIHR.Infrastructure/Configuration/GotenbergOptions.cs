namespace CLARIHR.Infrastructure.Configuration;

/// <summary>
/// Gotenberg HTTP renderer settings (technical-debt doc 01 §4.2), used when
/// <c>Reporting:Pdf:Engine = Gotenberg</c>. Gotenberg is an Apache-2.0 service
/// that renders HTML → PDF via Chromium, so no PDF-library license applies.
/// </summary>
public sealed class GotenbergOptions
{
    public const string SectionName = "Reporting:Pdf:Gotenberg";

    /// <summary>Base URL of the Gotenberg service (e.g. <c>http://localhost:3000</c>).</summary>
    public string BaseUrl { get; init; } = "http://localhost:3000";

    /// <summary>HTTP timeout (seconds) for a single render request.</summary>
    public int TimeoutSeconds { get; init; } = 60;

    public string NormalizedBaseUrl =>
        string.IsNullOrWhiteSpace(BaseUrl) ? "http://localhost:3000" : BaseUrl.Trim();

    public TimeSpan NormalizedTimeout => TimeSpan.FromSeconds(Math.Max(5, TimeoutSeconds));
}
