using System.Text.Json;
using System.Xml.Linq;
using CLARIHR.Api.Common;
using CLARIHR.Application.Features.PositionSlots;
using CLARIHR.Domain.PositionSlots;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// PS-E: unit coverage for the diagram serializer extracted out of <c>PositionSlotsController</c>.
/// Before the extraction this presentation logic was only reachable through integration; now each format
/// (GraphML/DOT/JSON) is exercised in isolation, including the injection-hardening (XML auto-escape +
/// DOT label escaping of <c>\</c> and <c>"</c> — the latter a real defect in the previous in-controller
/// <c>EscapeDot</c>, which escaped only the quote).
/// </summary>
public sealed class PositionSlotDiagramWriterTests
{
    private static readonly PositionSlotDiagramWriter Writer = new();

    private static readonly Guid RootId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ChildId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static PositionSlotGraphResponse SampleGraph(string rootLabel = "Headquarters") =>
        new(
            Nodes: new[]
            {
                new PositionSlotGraphNodeResponse(RootId, "PS-A", rootLabel, PositionSlotStatus.Vacant, Guid.NewGuid(), Guid.NewGuid(), null, null, null, IsActive: true),
                new PositionSlotGraphNodeResponse(ChildId, "PS-B", "Sales", PositionSlotStatus.Occupied, Guid.NewGuid(), Guid.NewGuid(), null, null, null, IsActive: true),
            },
            Edges: new[] { new PositionSlotGraphEdgeResponse(RootId, ChildId, PositionSlotDependencyRelationType.Direct) });

    [Fact]
    public void WriteGraphMl_ProducesWellFormedGraphmlWithNodesAndEdges()
    {
        var xml = Writer.WriteGraphMl(SampleGraph());

        var document = XDocument.Parse(xml); // throws if malformed
        XNamespace ns = "http://graphml.graphdrawing.org/xmlns";

        Assert.Equal("graphml", document.Root!.Name.LocalName);
        Assert.Equal(2, document.Descendants(ns + "node").Count());
        Assert.Single(document.Descendants(ns + "edge"));
        Assert.Contains(document.Descendants(ns + "data"), data => (string?)data == "PS-A - Headquarters");
    }

    [Fact]
    public void WriteGraphMl_EscapesXmlSpecialCharactersInLabels()
    {
        var xml = Writer.WriteGraphMl(SampleGraph(rootLabel: "R&D <Core>"));

        // XmlWriter must have encoded the entities — the raw string must not leak unescaped.
        Assert.DoesNotContain("<Core>", xml, StringComparison.Ordinal);
        Assert.Contains("R&amp;D", xml, StringComparison.Ordinal);

        var document = XDocument.Parse(xml);
        XNamespace ns = "http://graphml.graphdrawing.org/xmlns";
        Assert.Contains(document.Descendants(ns + "data"), data => (string?)data == "PS-A - R&D <Core>");
    }

    [Fact]
    public void WriteDot_ProducesDigraphWithNodesAndEdges()
    {
        var dot = Writer.WriteDot(SampleGraph());

        Assert.StartsWith("digraph PositionSlots {", dot, StringComparison.Ordinal);
        Assert.Contains($"\"{RootId:D}\" [label=\"PS-A - Headquarters\"];", dot, StringComparison.Ordinal);
        Assert.Contains($"\"{RootId:D}\" -> \"{ChildId:D}\" [label=\"Direct\"];", dot, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteDot_EscapesBackslashAndQuoteInLabels()
    {
        var dot = Writer.WriteDot(SampleGraph(rootLabel: "A\\B\"C"));

        // backslash → \\ , quote → \" so the label cannot break out of the DOT string literal.
        Assert.Contains("label=\"PS-A - A\\\\B\\\"C\"", dot, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteJson_RoundTripsNodesAndEdges()
    {
        var json = Writer.WriteJson(SampleGraph());

        var roundTripped = JsonSerializer.Deserialize<PositionSlotGraphResponse>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(2, roundTripped!.Nodes.Count);
        Assert.Single(roundTripped.Edges);
    }
}
