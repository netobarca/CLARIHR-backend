using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.OrgUnits;
using CLARIHR.Application.Features.OrgUnits.Common;
using CLARIHR.Application.Features.Reports.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class OrgUnitsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [HttpGet("api/v1/companies/{companyId:guid}/org-units")]
    [ProducesResponseType<PagedResponse<OrgUnitResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<OrgUnitResponse>>> Search(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery] Guid? orgUnitTypeId,
        [FromQuery] Guid? functionalAreaId,
        [FromQuery] Guid? parentId,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchOrgUnitsQuery(companyId, isActive, q, orgUnitTypeId, functionalAreaId, parentId, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/org-units/{id:guid}")]
    [ProducesResponseType<OrgUnitResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrgUnitResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetOrgUnitByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/org-units/tree")]
    [ProducesResponseType<IReadOnlyCollection<OrgUnitTreeNodeResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<OrgUnitTreeNodeResponse>>> Tree(
        Guid companyId,
        [FromQuery] Guid? rootId,
        [FromQuery] int? depth,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetOrgUnitTreeQuery(companyId, rootId, depth),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/org-units/graph")]
    [ProducesResponseType<OrgUnitGraphResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrgUnitGraphResponse>> Graph(
        Guid companyId,
        [FromQuery] Guid? rootId,
        [FromQuery] int? depth,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetOrgUnitGraphQuery(companyId, rootId, depth),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/org-units/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> Export(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] bool? isActive = null,
        [FromQuery] Guid? orgUnitTypeId = null,
        [FromQuery] Guid? functionalAreaId = null,
        [FromQuery] Guid? parentId = null,
        [FromQuery(Name = "q")] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetOrgUnitExportRowsQuery(
                companyId,
                isActive,
                search,
                orgUnitTypeId,
                functionalAreaId,
                parentId,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<OrgUnitExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "org-units",
            "OrgUnits",
            AuditEntityTypes.OrgUnit,
            ReportExportResources.OrgUnits,
            "Exported org units report.",
            new { isActive, orgUnitTypeId, functionalAreaId, parentId, q = search },
            ReportPolicyErrors.FormatNotSupported,
            cancellationToken);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/org-units/diagram-export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> DiagramExport(
        Guid companyId,
        [FromQuery] string format = "graphml",
        [FromQuery] Guid? rootId = null,
        [FromQuery] int? depth = null,
        CancellationToken cancellationToken = default)
    {
        var graphResult = await queryDispatcher.SendAsync(
            new GetOrgUnitGraphQuery(companyId, rootId, depth),
            cancellationToken);

        if (graphResult.IsFailure)
        {
            return this.ToActionResult(Result<OrgUnitGraphResponse>.Failure(graphResult.Error)).Result!;
        }

        if (graphResult.Value.Nodes.Count > reportExportDeliveryService.MaxDiagramNodes)
        {
            return CLARIHR.Api.Common.ProblemDetailsFactory.Create(HttpContext, ReportPolicyErrors.ExportLimitExceeded);
        }

        if (string.Equals(format, "graphml", StringComparison.OrdinalIgnoreCase))
        {
            await reportExportDeliveryService.LogExportAsync(
                AuditEntityTypes.OrgUnit,
                ReportExportResources.OrgUnits,
                "Exported org units diagram.",
                "graphml",
                new { rootId, depth },
                graphResult.Value.Nodes.Count,
                cancellationToken);

            var graphml = BuildGraphMl(graphResult.Value);
            return File(Encoding.UTF8.GetBytes(graphml), "application/graphml+xml", "org-units-diagram.graphml");
        }

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            await reportExportDeliveryService.LogExportAsync(
                AuditEntityTypes.OrgUnit,
                ReportExportResources.OrgUnits,
                "Exported org units diagram.",
                "json",
                new { rootId, depth },
                graphResult.Value.Nodes.Count,
                cancellationToken);

            var json = JsonSerializer.Serialize(graphResult.Value);
            return File(Encoding.UTF8.GetBytes(json), "application/json", "org-units-diagram.json");
        }

        if (string.Equals(format, "dot", StringComparison.OrdinalIgnoreCase))
        {
            await reportExportDeliveryService.LogExportAsync(
                AuditEntityTypes.OrgUnit,
                ReportExportResources.OrgUnits,
                "Exported org units diagram.",
                "dot",
                new { rootId, depth },
                graphResult.Value.Nodes.Count,
                cancellationToken);

            var dot = BuildDot(graphResult.Value);
            return File(Encoding.UTF8.GetBytes(dot), "text/vnd.graphviz", "org-units-diagram.dot");
        }

        return this.ToActionResult(Result<OrgUnitGraphResponse>.Failure(ReportPolicyErrors.FormatNotSupported)).Result!;
    }

    [HttpPost("api/v1/companies/{companyId:guid}/org-units")]
    [ProducesResponseType<OrgUnitResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrgUnitResponse>> Create(
        Guid companyId,
        [FromBody] CreateOrgUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateOrgUnitCommand(
                companyId,
                request.Code,
                request.Name,
                request.OrgUnitTypePublicId,
                request.FunctionalAreaPublicId,
                request.ParentPublicId,
                request.SortOrder,
                request.Description,
                request.CostCenterCode,
                request.ManagerEmployeePublicId),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<OrgUnitResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/org-units/{id:guid}")]
    [ProducesResponseType<OrgUnitResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrgUnitResponse>> Update(
        Guid id,
        [FromBody] UpdateOrgUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateOrgUnitCommand(
                id,
                request.Code,
                request.Name,
                request.OrgUnitTypePublicId,
                request.FunctionalAreaPublicId,
                request.SortOrder,
                request.Description,
                request.CostCenterCode,
                request.ManagerEmployeePublicId,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/org-units/{id:guid}/move")]
    [ProducesResponseType<OrgUnitResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrgUnitResponse>> Move(
        Guid id,
        [FromBody] MoveOrgUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new MoveOrgUnitCommand(id, request.NewParentPublicId, request.SortOrder, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/org-units/{id:guid}/activate")]
    [ProducesResponseType<OrgUnitResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrgUnitResponse>> Activate(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateOrgUnitCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/org-units/{id:guid}/inactivate")]
    [ProducesResponseType<OrgUnitResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrgUnitResponse>> Inactivate(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateOrgUnitCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    private static string BuildGraphMl(OrgUnitGraphResponse graph)
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

    private static string BuildDot(OrgUnitGraphResponse graph)
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

    private static string EscapeDot(string? value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    public sealed record CreateOrgUnitRequest(
        string Code,
        string Name,
        Guid OrgUnitTypePublicId,
        Guid? FunctionalAreaPublicId,
        Guid? ParentPublicId,
        int? SortOrder,
        string? Description,
        string? CostCenterCode,
        Guid? ManagerEmployeePublicId);

    public sealed record UpdateOrgUnitRequest(
        string Code,
        string Name,
        Guid OrgUnitTypePublicId,
        Guid? FunctionalAreaPublicId,
        int? SortOrder,
        string? Description,
        string? CostCenterCode,
        Guid? ManagerEmployeePublicId,
        Guid ConcurrencyToken);

    public sealed record MoveOrgUnitRequest(Guid? NewParentPublicId, int? SortOrder, Guid ConcurrencyToken);

    public sealed record ConcurrencyRequest(Guid ConcurrencyToken);
}
