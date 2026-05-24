using System.Net.Http.Headers;
using CLARIHR.Application.Abstractions.Reports.Documents;

namespace CLARIHR.Infrastructure.Reports.Documents;

/// <summary>
/// <see cref="IDocumentModelRenderer"/> backed by Gotenberg (Apache-2.0): serializes
/// the AST to HTML and POSTs it to Gotenberg's Chromium HTML→PDF route. No PDF
/// library license is involved — the §4.2 alternative to QuestPDF. Requires a
/// reachable Gotenberg service (configured via <c>Reporting:Pdf:Gotenberg:BaseUrl</c>
/// on the injected typed <see cref="HttpClient"/>).
/// </summary>
internal sealed class GotenbergDocumentRenderer : IDocumentModelRenderer
{
    private const string ConvertHtmlPath = "forms/chromium/convert/html";

    // Chromium renders the footer from a separate file; the special spans are
    // filled by Chromium. Mirrors the QuestPDF footer ("Generado por CLARIHR · Página X de Y").
    private const string FooterHtml =
        "<!DOCTYPE html><html><head><style>" +
        "body{font-size:8px;color:#6B7280;margin:0;width:100%;-webkit-print-color-adjust:exact;}" +
        ".f{width:100%;text-align:center;}</style></head><body><div class=\"f\">" +
        "Generado por CLARIHR · Página <span class=\"pageNumber\"></span> de <span class=\"totalPages\"></span>" +
        "</div></body></html>";

    private readonly HttpClient _httpClient;

    public GotenbergDocumentRenderer(HttpClient httpClient) => _httpClient = httpClient;

    public async Task RenderAsync(DocumentModel document, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);

        var html = DocumentModelHtmlSerializer.Serialize(document);

        using var form = new MultipartFormDataContent();
        AddHtmlFile(form, "index.html", html);
        AddHtmlFile(form, "footer.html", FooterHtml);

        // US Letter (inches) + margins; bottom margin leaves room for the footer.
        // printBackground keeps the accent rule/table header shading.
        AddField(form, "paperWidth", "8.5");
        AddField(form, "paperHeight", "11");
        AddField(form, "marginTop", "0.5");
        AddField(form, "marginBottom", "0.6");
        AddField(form, "marginLeft", "0.5");
        AddField(form, "marginRight", "0.5");
        AddField(form, "printBackground", "true");

        using var response = await _httpClient.PostAsync(ConvertHtmlPath, form, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Gotenberg render failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}. " +
                $"Response: {Truncate(body, 500)}");
        }

        await response.Content.CopyToAsync(destination, cancellationToken);
    }

    private static void AddHtmlFile(MultipartFormDataContent form, string fileName, string html)
    {
        var content = new StringContent(html);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/html") { CharSet = "utf-8" };
        // Gotenberg discovers files by the "files" field; the file name is the routing key.
        form.Add(content, "files", fileName);
    }

    private static void AddField(MultipartFormDataContent form, string name, string value) =>
        form.Add(new StringContent(value), name);

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
