using System.Text.Json;
using System.Xml.Linq;
using CLARIHR.Api.Common;
using CLARIHR.Application.Features.OrgUnits;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// OU-006: unit coverage for the diagram serializer extracted out of <c>OrganizationUnitsController</c>.
/// Before the extraction this presentation logic was only reachable through integration; now each format
/// (GraphML/DOT/JSON) is exercised in isolation, including the injection-hardening (XML auto-escape +
/// DOT label escaping of <c>\</c> and <c>"</c>).
/// </summary>
public sealed class OrgUnitDiagramWriterTests
{
    private static readonly OrgUnitDiagramWriter Writer = new();

    private static readonly Guid RootId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ChildId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static OrgUnitGraphResponse SampleGraph(string rootLabel = "Headquarters") =>
        new(
            Nodes: new[]
            {
                new OrgUnitGraphNodeResponse(RootId, rootLabel, Guid.NewGuid(), "DIV", "Division", IsActive: true),
                new OrgUnitGraphNodeResponse(ChildId, "Sales", Guid.NewGuid(), "DEP", "Department", IsActive: false),
            },
            Edges: new[] { new OrgUnitGraphEdgeResponse(RootId, ChildId) });

    [Fact]
    public void WriteGraphMl_ProducesWellFormedGraphmlWithNodesAndEdges()
    {
        var xml = Writer.WriteGraphMl(SampleGraph());

        var document = XDocument.Parse(xml); // throws if malformed
        XNamespace ns = "http://graphml.graphdrawing.org/xmlns";

        Assert.Equal("graphml", document.Root!.Name.LocalName);
        Assert.Equal(2, document.Descendants(ns + "node").Count());
        Assert.Single(document.Descendants(ns + "edge"));
        Assert.Contains(document.Descendants(ns + "data"), data => (string?)data == "Headquarters");
    }

    [Fact]
    public void WriteGraphMl_EscapesXmlSpecialCharactersInLabels()
    {
        var xml = Writer.WriteGraphMl(SampleGraph(rootLabel: "R&D <Core>"));

        // XmlWriter must have encoded the entities — the raw string must not leak unescaped.
        Assert.DoesNotContain("<Core>", xml, StringComparison.Ordinal);
        Assert.Contains("R&amp;D", xml, StringComparison.Ordinal);

        // And it must still parse back to the original value.
        var document = XDocument.Parse(xml);
        XNamespace ns = "http://graphml.graphdrawing.org/xmlns";
        Assert.Contains(document.Descendants(ns + "data"), data => (string?)data == "R&D <Core>");
    }

    [Fact]
    public void WriteDot_ProducesDigraphWithNodesAndEdges()
    {
        var dot = Writer.WriteDot(SampleGraph());

        Assert.StartsWith("digraph OrgUnits {", dot, StringComparison.Ordinal);
        Assert.Contains($"\"{RootId}\" [label=\"Headquarters\"", dot, StringComparison.Ordinal);
        Assert.Contains("color=\"gray\"", dot, StringComparison.Ordinal); // inactive child
        Assert.Contains($"\"{RootId}\" -> \"{ChildId}\";", dot, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteDot_EscapesBackslashAndQuoteInLabels()
    {
        var dot = Writer.WriteDot(SampleGraph(rootLabel: "A\\B\"C"));

        // backslash → \\ , quote → \" so the label cannot break out of the DOT string literal.
        Assert.Contains("label=\"A\\\\B\\\"C\"", dot, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteJson_RoundTripsNodesAndEdges()
    {
        var json = Writer.WriteJson(SampleGraph());

        // Deserialize with default options, mirroring the writer's default JsonSerializer.Serialize.
        var roundTripped = JsonSerializer.Deserialize<OrgUnitGraphResponse>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(2, roundTripped!.Nodes.Count);
        Assert.Single(roundTripped.Edges);
    }
}
