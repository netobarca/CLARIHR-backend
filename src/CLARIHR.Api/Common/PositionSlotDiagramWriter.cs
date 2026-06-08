using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using CLARIHR.Application.Features.PositionSlots;

namespace CLARIHR.Api.Common;

/// <summary>
/// PS-E: stateless serializer for the position-slots diagram export, extracted out of
/// <c>PositionSlotsController</c> so the GraphML/DOT/JSON presentation logic is unit-testable in
/// isolation (parity with <see cref="OrgUnitDiagramWriter"/>). Pure: no I/O, no state — given a
/// <see cref="PositionSlotGraphResponse"/> it returns the serialized document text. The controller
/// keeps the format dispatch, content-type/filename mapping and audit logging; this writer only formats.
/// </summary>
public sealed class PositionSlotDiagramWriter
{
    /// <summary>
    /// Serializes the graph as GraphML. Node labels/status are written via <see cref="XmlWriter"/>,
    /// which auto-escapes the XML payload (no injection).
    /// </summary>
    public string WriteGraphMl(PositionSlotGraphResponse graph)
    {
        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = false,
            Indent = true,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        };

        using var stringWriter = new StringWriter(CultureInfo.InvariantCulture);
        using (var writer = XmlWriter.Create(stringWriter, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("graphml", "http://graphml.graphdrawing.org/xmlns");

            writer.WriteStartElement("key");
            writer.WriteAttributeString("id", "d0");
            writer.WriteAttributeString("for", "node");
            writer.WriteAttributeString("attr.name", "label");
            writer.WriteAttributeString("attr.type", "string");
            writer.WriteEndElement();

            writer.WriteStartElement("key");
            writer.WriteAttributeString("id", "d1");
            writer.WriteAttributeString("for", "node");
            writer.WriteAttributeString("attr.name", "status");
            writer.WriteAttributeString("attr.type", "string");
            writer.WriteEndElement();

            writer.WriteStartElement("key");
            writer.WriteAttributeString("id", "d2");
            writer.WriteAttributeString("for", "edge");
            writer.WriteAttributeString("attr.name", "relationType");
            writer.WriteAttributeString("attr.type", "string");
            writer.WriteEndElement();

            writer.WriteStartElement("graph");
            writer.WriteAttributeString("id", "G");
            writer.WriteAttributeString("edgedefault", "directed");

            foreach (var node in graph.Nodes)
            {
                writer.WriteStartElement("node");
                writer.WriteAttributeString("id", node.Id.ToString("D", CultureInfo.InvariantCulture));

                writer.WriteStartElement("data");
                writer.WriteAttributeString("key", "d0");
                writer.WriteString($"{node.Code} - {node.Label}");
                writer.WriteEndElement();

                writer.WriteStartElement("data");
                writer.WriteAttributeString("key", "d1");
                writer.WriteString(node.Status.ToString());
                writer.WriteEndElement();

                writer.WriteEndElement();
            }

            var index = 0;
            foreach (var edge in graph.Edges)
            {
                writer.WriteStartElement("edge");
                writer.WriteAttributeString("id", $"e{index++}");
                writer.WriteAttributeString("source", edge.FromId.ToString("D", CultureInfo.InvariantCulture));
                writer.WriteAttributeString("target", edge.ToId.ToString("D", CultureInfo.InvariantCulture));

                writer.WriteStartElement("data");
                writer.WriteAttributeString("key", "d2");
                writer.WriteString(edge.RelationType.ToString());
                writer.WriteEndElement();

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return stringWriter.ToString();
    }

    /// <summary>Serializes the graph as Graphviz DOT. Labels are escaped via <see cref="EscapeDot"/>.</summary>
    public string WriteDot(PositionSlotGraphResponse graph)
    {
        var builder = new StringBuilder();
        builder.AppendLine("digraph PositionSlots {");

        foreach (var node in graph.Nodes.OrderBy(static node => node.Code))
        {
            var label = EscapeDot($"{node.Code} - {node.Label}");
            builder.AppendLine($"  \"{node.Id:D}\" [label=\"{label}\"];");
        }

        foreach (var edge in graph.Edges)
        {
            builder.AppendLine($"  \"{edge.FromId:D}\" -> \"{edge.ToId:D}\" [label=\"{edge.RelationType}\"];");
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    /// <summary>Serializes the graph as JSON (the raw nodes/edges projection).</summary>
    public string WriteJson(PositionSlotGraphResponse graph) =>
        JsonSerializer.Serialize(graph);

    // PS-E: neutralizes backslash (FIRST, so the escaping is not double-applied) and double-quote so a
    // node label cannot break out of the DOT string literal. The previous implementation escaped only
    // the quote, leaving a trailing `\` able to malform the document.
    private static string EscapeDot(string? value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
