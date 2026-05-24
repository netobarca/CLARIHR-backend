using CLARIHR.Application.Abstractions.Reports.Documents;
using CLARIHR.Infrastructure.Reports.Documents;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// §4.2 (Gotenberg): the AST→HTML serializer must reproduce the document
/// structure/order and HTML-encode all text (no injection from profile fields).
/// </summary>
public sealed class DocumentModelHtmlSerializerTests
{
    [Fact]
    public void Serialize_RendersTitleHeaderFieldsAndSections()
    {
        var model = new DocumentModel(
            Title: "Gerente de Desarrollo",
            HeaderFields: new[] { new DocumentField("Código", "MGR-001"), new DocumentField("Estado", "Publicado") },
            GeneratedText: "9 may 2026",
            Sections: new[]
            {
                new DocumentSection("Objetivo", new DocumentBlock[] { new ParagraphBlock("Liderar el área.") }),
            });

        var html = DocumentModelHtmlSerializer.Serialize(model);

        Assert.StartsWith("<!DOCTYPE html>", html, StringComparison.Ordinal);
        Assert.Contains("<h1 class=\"doc-title\">Gerente de Desarrollo</h1>", html, StringComparison.Ordinal);
        Assert.Contains("MGR-001", html, StringComparison.Ordinal);
        Assert.Contains("<h2 class=\"section-title\">Objetivo</h2>", html, StringComparison.Ordinal);
        Assert.Contains("<p>Liderar el área.</p>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_EncodesHtmlSpecialCharacters()
    {
        var model = new DocumentModel(
            Title: "A & B <script>",
            HeaderFields: Array.Empty<DocumentField>(),
            GeneratedText: "x",
            Sections: new[]
            {
                new DocumentSection("Func", new DocumentBlock[] { new ParagraphBlock("1 < 2 & \"q\"") }),
            });

        var html = DocumentModelHtmlSerializer.Serialize(model);

        Assert.Contains("A &amp; B &lt;script&gt;", html, StringComparison.Ordinal);
        Assert.Contains("1 &lt; 2 &amp; &quot;q&quot;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_RendersKeyValueTableAndBullets()
    {
        var model = new DocumentModel(
            "T", Array.Empty<DocumentField>(), "g",
            new[]
            {
                new DocumentSection("Comp", new DocumentBlock[]
                {
                    new KeyValueBlock(new[] { new DocumentField("Base", "120,000.00") }),
                    new TableBlock(
                        new[] { DocumentTableColumn.Relative("Competencia"), DocumentTableColumn.Constant("Nivel", 80) },
                        new IReadOnlyList<string>[] { new[] { "Pensamiento", "Avanzado" } }),
                    new BulletListBlock(new[] { new BulletItem("Define visión", "a 3 años") }),
                }),
            });

        var html = DocumentModelHtmlSerializer.Serialize(model);

        Assert.Contains("<td class=\"kv-label\">Base</td><td>120,000.00</td>", html, StringComparison.Ordinal);
        Assert.Contains("<th>Competencia</th>", html, StringComparison.Ordinal);
        Assert.Contains("width:80px", html, StringComparison.Ordinal);
        Assert.Contains("<td>Pensamiento</td><td>Avanzado</td>", html, StringComparison.Ordinal);
        Assert.Contains("Define visión", html, StringComparison.Ordinal);
        Assert.Contains("a 3 años", html, StringComparison.Ordinal);
    }
}
