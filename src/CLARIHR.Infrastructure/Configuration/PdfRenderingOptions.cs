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
    /// PDF engine key. Must be one of <see cref="PdfEngines"/>. Defaults to
    /// <see cref="PdfEngines.QuestPdf"/> (the only engine implemented today).
    /// An unknown value fails fast at startup.
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
    /// <summary>QuestPDF (Lato embedded, cross-platform) — the default, implemented today.</summary>
    public const string QuestPdf = "QuestPdf";

    // Future engines — implement IDocumentModelRenderer + register in
    // DocumentPdfRenderingRegistration, then expose the key here:
    //   public const string Gotenberg = "Gotenberg"; // Apache-2.0, HTML→PDF over HTTP
    //   public const string IText = "IText";          // ⚠ AGPL/commercial license
}
