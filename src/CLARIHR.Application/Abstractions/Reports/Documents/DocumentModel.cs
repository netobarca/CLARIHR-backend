namespace CLARIHR.Application.Abstractions.Reports.Documents;

/// <summary>
/// Format-agnostic document AST (technical-debt doc 01 §2.2). A payload mapper
/// produces a <see cref="DocumentModel"/>; one <see cref="IDocumentModelRenderer"/>
/// per output format (PDF today, DOCX/HTML later) consumes it. Adding a format no
/// longer means rewriting the JobProfile→document logic.
/// </summary>
public sealed record DocumentModel(
    string Title,
    IReadOnlyList<DocumentField> HeaderFields,
    string GeneratedText,
    IReadOnlyList<DocumentSection> Sections);

/// <summary>A label/value pair (header field or key-value row).</summary>
public sealed record DocumentField(string Label, string Value);

/// <summary>A titled section containing an ordered list of content blocks.</summary>
public sealed record DocumentSection(string Title, IReadOnlyList<DocumentBlock> Blocks);

/// <summary>Base type for the content blocks a section can hold.</summary>
public abstract record DocumentBlock;

/// <summary>Plain paragraph of text.</summary>
public sealed record ParagraphBlock(string Text) : DocumentBlock;

/// <summary>Secondary muted/italic text — empty-state notices and summaries.</summary>
public sealed record MutedTextBlock(string Text) : DocumentBlock;

/// <summary>Bold inline label followed by text, e.g. "Referencia de mercado: …".</summary>
public sealed record LabeledParagraphBlock(string Label, string Text) : DocumentBlock;

/// <summary>Two-column key/value list (label emphasized, value plain).</summary>
public sealed record KeyValueBlock(IReadOnlyList<DocumentField> Items) : DocumentBlock;

/// <summary>Tabular block with a header row and string cells.</summary>
public sealed record TableBlock(
    IReadOnlyList<DocumentTableColumn> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows) : DocumentBlock;

/// <summary>
/// A table column. Exactly one of <see cref="ConstantWidth"/> /
/// <see cref="RelativeWidth"/> is set; renderers map these to their own
/// fixed vs. proportional column primitives.
/// </summary>
public sealed record DocumentTableColumn(string Header, float? ConstantWidth, float? RelativeWidth)
{
    public static DocumentTableColumn Constant(string header, float width) => new(header, width, null);

    public static DocumentTableColumn Relative(string header, float weight = 1f) => new(header, null, weight);
}

/// <summary>Bulleted list; each item is emphasized text with optional trailing note.</summary>
public sealed record BulletListBlock(IReadOnlyList<BulletItem> Items) : DocumentBlock;

public sealed record BulletItem(string Text, string? Notes);

/// <summary>
/// Renders a <see cref="DocumentModel"/> into a concrete output format. One
/// implementation per engine/format keeps the document logic single-sourced and
/// the PDF engine swappable (technical-debt doc 01 §4.2). The contract is async
/// so that an out-of-process engine (e.g. Gotenberg over HTTP) fits without
/// blocking; a CPU-bound engine like QuestPDF offloads its synchronous render to
/// the thread pool inside its own implementation.
/// </summary>
public interface IDocumentModelRenderer
{
    Task RenderAsync(DocumentModel document, Stream destination, CancellationToken cancellationToken);
}
