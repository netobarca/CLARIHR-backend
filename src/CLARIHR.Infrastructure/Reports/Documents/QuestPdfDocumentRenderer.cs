using CLARIHR.Application.Abstractions.Reports.Documents;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CLARIHR.Infrastructure.Reports.Documents;

/// <summary>
/// QuestPDF adapter for the format-agnostic <see cref="DocumentModel"/> AST
/// (technical-debt doc 01 §2.2). Owns layout/styling only; all content and
/// formatting decisions are made by the payload mapper. A future DOCX/HTML
/// format is a sibling <see cref="IDocumentModelRenderer"/>, not a rewrite.
/// </summary>
internal sealed class QuestPdfDocumentRenderer : IDocumentModelRenderer
{
    private const string AccentColorHex = "#1F3A8A";
    private const string MutedColorHex = "#6B7280";

    // §4.1 (technical-debt doc 01): render with a font that ships *embedded* with
    // QuestPDF instead of a Microsoft font like Calibri. Calibri is absent on Linux
    // container base images, so QuestPDF would fall back silently to a different
    // face — yielding different layout metrics dev↔prod. Lato is bundled with the
    // QuestPDF package (and copied into the publish output) and auto-registered, so
    // it renders identically on every OS without installing any system font. The
    // Linux container still needs the native libfontconfig1 library (installed in
    // the repo Dockerfile); the typeface itself no longer depends on the host OS.
    private const string DefaultFontFamily = Fonts.Lato;

    public void Render(DocumentModel document, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.Letter);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(text => text.FontSize(10).FontFamily(DefaultFontFamily));

                page.Header().Element(header => ComposeHeader(header, document));
                page.Content().Element(content => ComposeContent(content, document));
                page.Footer().Element(ComposeFooter);
            });
        });

        pdf.GeneratePdf(destination);
    }

    private static void ComposeHeader(IContainer container, DocumentModel document)
    {
        container.Column(column =>
        {
            column.Spacing(2);
            column.Item().Text(document.Title)
                .FontSize(20).Bold().FontColor(AccentColorHex);

            column.Item().Row(row =>
            {
                foreach (var field in document.HeaderFields)
                {
                    row.RelativeItem().Text(text =>
                    {
                        text.Span($"{field.Label}: ").SemiBold();
                        text.Span(field.Value);
                    });
                }
            });

            column.Item().Text(text =>
            {
                text.Span("Generado: ").SemiBold().FontColor(MutedColorHex);
                text.Span(document.GeneratedText).FontColor(MutedColorHex);
            });

            column.Item().PaddingTop(6).LineHorizontal(0.6f).LineColor(AccentColorHex);
        });
    }

    private static void ComposeContent(IContainer container, DocumentModel document)
    {
        container.PaddingVertical(8).Column(column =>
        {
            column.Spacing(10);

            foreach (var section in document.Sections)
            {
                ComposeSectionTitle(column, section.Title);
                foreach (var block in section.Blocks)
                {
                    ComposeBlock(column, block);
                }
            }
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.DefaultTextStyle(style => style.FontSize(8).FontColor(MutedColorHex));
            text.Span("Generado por CLARIHR · Página ");
            text.CurrentPageNumber();
            text.Span(" de ");
            text.TotalPages();
        });
    }

    private static void ComposeBlock(ColumnDescriptor column, DocumentBlock block)
    {
        switch (block)
        {
            case ParagraphBlock paragraph:
                column.Item().Text(paragraph.Text);
                break;

            case MutedTextBlock muted:
                column.Item().Text(muted.Text).Italic().FontColor(MutedColorHex);
                break;

            case LabeledParagraphBlock labeled:
                column.Item().PaddingTop(4).Text(text =>
                {
                    text.Span(labeled.Label).SemiBold();
                    text.Span(labeled.Text);
                });
                break;

            case KeyValueBlock keyValue:
                ComposeKeyValue(column, keyValue);
                break;

            case TableBlock table:
                ComposeTable(column, table);
                break;

            case BulletListBlock bullets:
                ComposeBullets(column, bullets);
                break;

            default:
                throw new NotSupportedException($"Unsupported document block '{block.GetType().Name}'.");
        }
    }

    private static void ComposeKeyValue(ColumnDescriptor column, KeyValueBlock block)
    {
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(definition =>
            {
                definition.RelativeColumn(2);
                definition.RelativeColumn(5);
            });

            foreach (var item in block.Items)
            {
                table.Cell().Element(BodyCell).Text(item.Label).SemiBold().FontColor(MutedColorHex);
                table.Cell().Element(BodyCell).Text(item.Value);
            }
        });
    }

    private static void ComposeTable(ColumnDescriptor column, TableBlock block)
    {
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(definition =>
            {
                foreach (var col in block.Columns)
                {
                    if (col.ConstantWidth.HasValue)
                    {
                        definition.ConstantColumn(col.ConstantWidth.Value);
                    }
                    else
                    {
                        definition.RelativeColumn(col.RelativeWidth ?? 1f);
                    }
                }
            });

            table.Header(header =>
            {
                foreach (var col in block.Columns)
                {
                    header.Cell().Element(HeaderCell).Text(col.Header).SemiBold();
                }
            });

            foreach (var row in block.Rows)
            {
                foreach (var cell in row)
                {
                    table.Cell().Element(BodyCell).Text(cell);
                }
            }
        });
    }

    private static void ComposeBullets(ColumnDescriptor column, BulletListBlock block)
    {
        column.Item().Column(items =>
        {
            items.Spacing(2);
            foreach (var item in block.Items)
            {
                items.Item().Text(text =>
                {
                    text.Span("• ");
                    text.Span(item.Text).SemiBold();
                    if (!string.IsNullOrWhiteSpace(item.Notes))
                    {
                        text.Span(" — ");
                        text.Span(item.Notes);
                    }
                });
            }
        });
    }

    private static void ComposeSectionTitle(ColumnDescriptor column, string title)
    {
        column.Item().PaddingTop(4).Text(title)
            .FontSize(12).Bold().FontColor(AccentColorHex);
    }

    private static IContainer HeaderCell(IContainer container) =>
        container
            .Background(Colors.Grey.Lighten3)
            .PaddingVertical(4)
            .PaddingHorizontal(6);

    private static IContainer BodyCell(IContainer container) =>
        container
            .PaddingVertical(3)
            .PaddingHorizontal(6)
            .BorderBottom(0.5f)
            .BorderColor(Colors.Grey.Lighten2);
}
