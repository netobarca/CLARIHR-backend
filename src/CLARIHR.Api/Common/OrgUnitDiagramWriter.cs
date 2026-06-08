using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using CLARIHR.Application.Features.OrgUnits;

namespace CLARIHR.Api.Common;

/// <summary>
/// OU-006: stateless serializer for the organization-unit diagram export, extracted out of
/// <c>OrganizationUnitsController</c> so the GraphML/DOT/JSON presentation logic is unit-testable in
/// isolation (parity with <see cref="ReportExportDeliveryService"/>). Pure: no I/O, no state — given an
/// <see cref="OrgUnitGraphResponse"/> it returns the serialized document text. The controller keeps the
/// format dispatch, content-type/filename mapping and audit logging; this writer only formats.
/// </summary>
public sealed class OrgUnitDiagramWriter
{
    /// <summary>
    /// Serializes the graph as GraphML. Node labels/types are written via <see cref="XmlWriter"/>, which
    /// auto-escapes the XML payload (no injection).
    /// </summary>
    public string WriteGraphMl(OrgUnitGraphResponse graph)
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
            writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
            writer.WriteAttributeString(
                "xsi",
                "schemaLocation",
                "http://www.w3.org/2001/XMLSchema-instance",
                "http://graphml.graphdrawing.org/xmlns http://graphml.graphdrawing.org/xmlns/1.0/graphml.xsd");

            writer.WriteStartElement("key");
            writer.WriteAttributeString("id", "label");
            writer.WriteAttributeString("for", "node");
            writer.WriteAttributeString("attr.name", "label");
            writer.WriteAttributeString("attr.type", "string");
            writer.WriteEndElement();

            writer.WriteStartElement("key");
            writer.WriteAttributeString("id", "type");
            writer.WriteAttributeString("for", "node");
            writer.WriteAttributeString("attr.name", "type");
            writer.WriteAttributeString("attr.type", "string");
            writer.WriteEndElement();

            writer.WriteStartElement("key");
            writer.WriteAttributeString("id", "isActive");
            writer.WriteAttributeString("for", "node");
            writer.WriteAttributeString("attr.name", "isActive");
            writer.WriteAttributeString("attr.type", "boolean");
            writer.WriteEndElement();

            writer.WriteStartElement("graph");
            writer.WriteAttributeString("id", "G");
            writer.WriteAttributeString("edgedefault", "directed");

            foreach (var node in graph.Nodes)
            {
                writer.WriteStartElement("node");
                writer.WriteAttributeString("id", node.Id.ToString());

                writer.WriteStartElement("data");
                writer.WriteAttributeString("key", "label");
                writer.WriteString(node.Label);
                writer.WriteEndElement();

                writer.WriteStartElement("data");
                writer.WriteAttributeString("key", "type");
                writer.WriteString(node.OrgUnitTypeCode);
                writer.WriteEndElement();

                writer.WriteStartElement("data");
                writer.WriteAttributeString("key", "isActive");
                writer.WriteString(node.IsActive ? "true" : "false");
                writer.WriteEndElement();

                writer.WriteEndElement();
            }

            foreach (var edge in graph.Edges)
            {
                writer.WriteStartElement("edge");
                writer.WriteAttributeString("source", edge.FromId.ToString());
                writer.WriteAttributeString("target", edge.ToId.ToString());
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return stringWriter.ToString();
    }

    /// <summary>Serializes the graph as Graphviz DOT. Labels are escaped via <see cref="EscapeDot"/>.</summary>
    public string WriteDot(OrgUnitGraphResponse graph)
    {
        var builder = new StringBuilder();
        builder.AppendLine("digraph OrgUnits {");
        builder.AppendLine("  rankdir=TB;");

        foreach (var node in graph.Nodes)
        {
            var label = EscapeDot(node.Label);
            var color = node.IsActive ? "black" : "gray";
            builder.AppendLine($"  \"{node.Id}\" [label=\"{label}\", color=\"{color}\"];");
        }

        foreach (var edge in graph.Edges)
        {
            builder.AppendLine($"  \"{edge.FromId}\" -> \"{edge.ToId}\";");
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    /// <summary>Serializes the graph as JSON (the raw nodes/edges projection).</summary>
    public string WriteJson(OrgUnitGraphResponse graph) =>
        JsonSerializer.Serialize(graph);

    // Neutralizes backslash (first, so the escaping is not double-applied) and double-quote so a node
    // label cannot break out of the DOT string literal.
    private static string EscapeDot(string? value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
