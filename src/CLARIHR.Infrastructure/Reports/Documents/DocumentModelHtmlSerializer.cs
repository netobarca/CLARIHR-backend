using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using CLARIHR.Application.Abstractions.Reports.Documents;

namespace CLARIHR.Infrastructure.Reports.Documents;

/// <summary>
/// Serializes the format-agnostic <see cref="DocumentModel"/> AST to an HTML
/// document for Chromium rendering (Gotenberg). Mirrors the structure the QuestPDF
/// adapter lays out — title, header fields, sections, key/value lists, tables,
/// bullets, in the same order — so both engines produce the same document content
/// (not pixel-identical) (technical-debt doc 01 §4.2). All text is HTML-encoded.
/// </summary>
internal static class DocumentModelHtmlSerializer
{
    // Escapes only HTML-significant characters (< > & " '); accented/Unicode text
    // stays literal under <meta charset="utf-8"> instead of bloating into &#NNN;.
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    public static string Serialize(DocumentModel document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var sb = new StringBuilder(4096);
        sb.Append("<!DOCTYPE html><html lang=\"es\"><head><meta charset=\"utf-8\"><style>")
          .Append(Css)
          .Append("</style></head><body>");

        sb.Append("<h1 class=\"doc-title\">").Append(Encode(document.Title)).Append("</h1>");

        if (document.HeaderFields.Count > 0)
        {
            sb.Append("<div class=\"header-fields\">");
            foreach (var field in document.HeaderFields)
            {
                sb.Append("<span class=\"header-field\"><span class=\"label\">")
                  .Append(Encode(field.Label)).Append(": </span>")
                  .Append(Encode(field.Value)).Append("</span>");
            }

            sb.Append("</div>");
        }

        sb.Append("<div class=\"generated\"><span class=\"label\">Generado: </span>")
          .Append(Encode(document.GeneratedText)).Append("</div>");
        sb.Append("<hr class=\"rule\">");

        foreach (var section in document.Sections)
        {
            sb.Append("<h2 class=\"section-title\">").Append(Encode(section.Title)).Append("</h2>");
            foreach (var block in section.Blocks)
            {
                AppendBlock(sb, block);
            }
        }

        return sb.Append("</body></html>").ToString();
    }

    private static void AppendBlock(StringBuilder sb, DocumentBlock block)
    {
        switch (block)
        {
            case ParagraphBlock paragraph:
                sb.Append("<p>").Append(Encode(paragraph.Text)).Append("</p>");
                break;

            case MutedTextBlock muted:
                sb.Append("<p class=\"muted\">").Append(Encode(muted.Text)).Append("</p>");
                break;

            case LabeledParagraphBlock labeled:
                sb.Append("<p><span class=\"label\">").Append(Encode(labeled.Label)).Append("</span>")
                  .Append(Encode(labeled.Text)).Append("</p>");
                break;

            case KeyValueBlock keyValue:
                sb.Append("<table class=\"kv\">");
                foreach (var item in keyValue.Items)
                {
                    sb.Append("<tr><td class=\"kv-label\">").Append(Encode(item.Label))
                      .Append("</td><td>").Append(Encode(item.Value)).Append("</td></tr>");
                }

                sb.Append("</table>");
                break;

            case TableBlock table:
                AppendTable(sb, table);
                break;

            case BulletListBlock bullets:
                sb.Append("<ul>");
                foreach (var item in bullets.Items)
                {
                    sb.Append("<li><span class=\"bullet-text\">").Append(Encode(item.Text)).Append("</span>");
                    if (!string.IsNullOrWhiteSpace(item.Notes))
                    {
                        sb.Append(" — ").Append(Encode(item.Notes));
                    }

                    sb.Append("</li>");
                }

                sb.Append("</ul>");
                break;

            default:
                throw new NotSupportedException($"Unsupported document block '{block.GetType().Name}'.");
        }
    }

    private static void AppendTable(StringBuilder sb, TableBlock table)
    {
        sb.Append("<table class=\"data\"><thead><tr>");
        foreach (var column in table.Columns)
        {
            var style = column.ConstantWidth.HasValue
                ? $" style=\"width:{column.ConstantWidth.Value.ToString(CultureInfo.InvariantCulture)}px\""
                : string.Empty;
            sb.Append("<th").Append(style).Append('>').Append(Encode(column.Header)).Append("</th>");
        }

        sb.Append("</tr></thead><tbody>");
        foreach (var row in table.Rows)
        {
            sb.Append("<tr>");
            foreach (var cell in row)
            {
                sb.Append("<td>").Append(Encode(cell)).Append("</td>");
            }

            sb.Append("</tr>");
        }

        sb.Append("</tbody></table>");
    }

    private static string Encode(string? value) => Encoder.Encode(value ?? string.Empty);

    // Mirrors QuestPdfDocumentRenderer's palette/sizing. Lato falls back to a sans
    // stack if absent in Chromium (output is functionally equivalent, not pixel-identical).
    private const string Css = """
        * { box-sizing: border-box; }
        body { font-family: 'Lato', 'Helvetica Neue', Arial, sans-serif; font-size: 10pt; color: #111111; margin: 0; }
        .doc-title { font-size: 20pt; font-weight: 700; color: #1F3A8A; margin: 0 0 4px; }
        .header-fields { display: flex; flex-wrap: wrap; gap: 12px; margin-bottom: 2px; }
        .header-field .label, .generated .label, p .label, .kv-label { font-weight: 600; }
        .generated, .generated .label { color: #6B7280; }
        .rule { border: none; border-top: 1.5px solid #1F3A8A; margin: 6px 0 10px; }
        .section-title { font-size: 12pt; font-weight: 700; color: #1F3A8A; margin: 12px 0 4px; }
        p { margin: 4px 0; }
        p.muted { color: #6B7280; font-style: italic; }
        table { width: 100%; border-collapse: collapse; margin: 4px 0; }
        table.data th { background: #f0f0f0; text-align: left; padding: 4px 6px; font-weight: 600; }
        table.data td { padding: 3px 6px; border-bottom: 0.5px solid #dddddd; }
        table.kv td { padding: 3px 6px; }
        table.kv .kv-label { color: #6B7280; width: 28%; }
        ul { margin: 4px 0; padding-left: 18px; }
        li { margin: 2px 0; }
        .bullet-text { font-weight: 600; }
        """;
}
