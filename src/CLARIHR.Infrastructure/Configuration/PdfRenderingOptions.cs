namespace CLARIHR.Infrastructure.Configuration;

/// <summary>
/// Selects the PDF rendering engine behind the format-agnostic
/// <c>IDocumentModelRenderer</c> seam (technical-debt doc 01 §4.2). The engine is
/// chosen at startup from configuration so QuestPDF can be swapped for another
/// generator (e.g. Gotenberg, iText) without touching the document/mapping logic.
/// </summary>
public sealed class PdfRenderingOptions
{
    public const string SectionName = "Reporting:Pdf";

    /// <summary>
    /// PDF engine key. Must be one of <see cref="PdfEngines"/>. The code default is
    /// <see cref="PdfEngines.QuestPdf"/> (safe in-process fallback when config is
    /// absent — e.g. tests); production <c>appsettings.json</c> selects
    /// <see cref="PdfEngines.Gotenberg"/>. An unknown value fails fast at startup.
    /// </summary>
    public string Engine { get; init; } = PdfEngines.QuestPdf;

    public string NormalizedEngine =>
        string.IsNullOrWhiteSpace(Engine) ? PdfEngines.QuestPdf : Engine.Trim();
}

/// <summary>
/// Known PDF engine keys. Add a constant here when wiring a new
/// <c>IDocumentModelRenderer</c> implementation (see
/// <c>DocumentPdfRenderingRegistration</c>).
/// </summary>
public static class PdfEngines
{
    /// <summary>QuestPDF (Lato embedded, cross-platform), in-process. Fallback engine.</summary>
    public const string QuestPdf = "QuestPdf";

    /// <summary>Gotenberg (Apache-2.0): HTML→PDF over HTTP via Chromium. No PDF-library license. Default in production.</summary>
    public const string Gotenberg = "Gotenberg";

    // To add another (e.g. iText — ⚠ AGPL/commercial license): implement
    // IDocumentModelRenderer, add a key here, and register it in
    // DocumentPdfRenderingRegistration.
}
