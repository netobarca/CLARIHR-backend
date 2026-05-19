using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.PositionSlots;
using CLARIHR.Application.Features.PositionSlots.Common;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.PositionSlots;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
[AuthorizationPolicySet(PositionSlotPolicies.Read, PositionSlotPolicies.Manage)]
public sealed class PositionSlotsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [HttpGet("api/v1/companies/{companyId:guid}/position-slots")]
    [ProducesResponseType<PagedResponse<PositionSlotListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<PositionSlotListItemResponse>>> Search(
        Guid companyId,
        [FromQuery] PositionSlotStatus? status,
        [FromQuery] Guid? jobProfileId,
        [FromQuery] Guid? orgUnitId,
        [FromQuery] Guid? workCenterId,
        [FromQuery] Guid? contractTypeId,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PositionSlotValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchPositionSlotsQuery(
                companyId,
                status,
                jobProfileId,
                orgUnitId,
                workCenterId,
                contractTypeId,
                search,
                page,
                pageSize,
                includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/position-slots/{id:guid}")]
    [ProducesResponseType<PositionSlotResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PositionSlotResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPositionSlotByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/position-slots/graph")]
    [ProducesResponseType<PositionSlotGraphResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PositionSlotGraphResponse>> Graph(
        Guid companyId,
        [FromQuery] Guid? rootId,
        [FromQuery] int? depth,
        [FromQuery] bool includeFunctional = true,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPositionSlotGraphQuery(companyId, rootId, depth, includeFunctional),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/position-slots/diagram-export")]
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
        [FromQuery] bool includeFunctional = true,
        CancellationToken cancellationToken = default)
    {
        var graphResult = await queryDispatcher.SendAsync(
            new GetPositionSlotGraphQuery(companyId, rootId, depth, includeFunctional),
            cancellationToken);

        if (graphResult.IsFailure)
        {
            return this.ToActionResult(Result<PositionSlotGraphResponse>.Failure(graphResult.Error)).Result!;
        }

        if (graphResult.Value.Nodes.Count > reportExportDeliveryService.MaxDiagramNodes)
        {
            return CLARIHR.Api.Common.ProblemDetailsFactory.Create(HttpContext, ReportPolicyErrors.ExportLimitExceeded);
        }

        if (string.Equals(format, "graphml", StringComparison.OrdinalIgnoreCase))
        {
            await reportExportDeliveryService.LogExportAsync(
                AuditEntityTypes.PositionSlot,
                ReportExportResources.PositionSlots,
                "Exported position slots diagram.",
                "graphml",
                new { rootId, depth, includeFunctional },
                graphResult.Value.Nodes.Count,
                cancellationToken);

            var graphml = BuildGraphMl(graphResult.Value);
            return File(Encoding.UTF8.GetBytes(graphml), "application/graphml+xml", "position-slots-diagram.graphml");
        }

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            await reportExportDeliveryService.LogExportAsync(
                AuditEntityTypes.PositionSlot,
                ReportExportResources.PositionSlots,
                "Exported position slots diagram.",
                "json",
                new { rootId, depth, includeFunctional },
                graphResult.Value.Nodes.Count,
                cancellationToken);

            var json = JsonSerializer.Serialize(graphResult.Value);
            return File(Encoding.UTF8.GetBytes(json), "application/json", "position-slots-diagram.json");
        }

        if (string.Equals(format, "dot", StringComparison.OrdinalIgnoreCase))
        {
            await reportExportDeliveryService.LogExportAsync(
                AuditEntityTypes.PositionSlot,
                ReportExportResources.PositionSlots,
                "Exported position slots diagram.",
                "dot",
                new { rootId, depth, includeFunctional },
                graphResult.Value.Nodes.Count,
                cancellationToken);

            var dot = BuildDot(graphResult.Value);
            return File(Encoding.UTF8.GetBytes(dot), "text/vnd.graphviz", "position-slots-diagram.dot");
        }

        return this.ToActionResult(Result<PositionSlotGraphResponse>.Failure(PositionSlotErrors.DiagramFormatInvalid)).Result!;
    }

    [HttpGet("api/v1/companies/{companyId:guid}/position-slots/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> Export(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] PositionSlotStatus? status = null,
        [FromQuery] Guid? jobProfileId = null,
        [FromQuery] Guid? orgUnitId = null,
        [FromQuery] Guid? workCenterId = null,
        [FromQuery] Guid? contractTypeId = null,
        [FromQuery(Name = "q")] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPositionSlotExportRowsQuery(
                companyId,
                status,
                jobProfileId,
                orgUnitId,
                workCenterId,
                contractTypeId,
                search,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<PositionSlotExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "position-slots",
            "PositionSlots",
            AuditEntityTypes.PositionSlot,
            ReportExportResources.PositionSlots,
            "Exported position slots report.",
            new { status, jobProfileId, orgUnitId, workCenterId, contractTypeId, q = search },
            PositionSlotErrors.ExportFormatInvalid,
            cancellationToken);
    }

    [HttpPost("api/v1/companies/{companyId:guid}/position-slots")]
    [ProducesResponseType<PositionSlotResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PositionSlotResponse>> Create(
        Guid companyId,
        [FromBody] CreatePositionSlotRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreatePositionSlotCommand(
                companyId,
                request.Code,
                request.Title,
                request.JobProfilePublicId,
                request.RolePublicId,
                request.WorkCenterPublicId,
                request.DirectDependencyPositionSlotPublicId,
                request.FunctionalDependencyPositionSlotPublicId,
                request.Status,
                request.MaxEmployees,
                request.OccupiedEmployees,
                request.EffectiveFromUtc,
                request.EffectiveToUtc,
                request.Notes),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<PositionSlotResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/position-slots/{id:guid}")]
    [ProducesResponseType<PositionSlotResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PositionSlotResponse>> Update(
        Guid id,
        [FromBody] UpdatePositionSlotRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePositionSlotCommand(
                id,
                request.Code,
                request.Title,
                request.JobProfilePublicId,
                request.RolePublicId,
                request.WorkCenterPublicId,
                request.MaxEmployees,
                request.EffectiveFromUtc,
                request.EffectiveToUtc,
                request.Notes,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/position-slots/{id:guid}/status")]
    [ProducesResponseType<PositionSlotResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PositionSlotResponse>> UpdateStatus(
        Guid id,
        [FromBody] UpdatePositionSlotStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePositionSlotStatusCommand(id, request.Status, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/position-slots/{id:guid}/dependencies")]
    [ProducesResponseType<PositionSlotResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PositionSlotResponse>> UpdateDependencies(
        Guid id,
        [FromBody] UpdatePositionSlotDependenciesRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePositionSlotDependenciesCommand(
                id,
                request.DirectDependencyPositionSlotPublicId,
                request.FunctionalDependencyPositionSlotPublicId,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/position-slots/{id:guid}/occupancy")]
    [ProducesResponseType<PositionSlotResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PositionSlotResponse>> UpdateOccupancy(
        Guid id,
        [FromBody] UpdatePositionSlotOccupancyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePositionSlotOccupancyCommand(id, request.OccupiedEmployees, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    private static string BuildDot(PositionSlotGraphResponse graph)
    {
        var builder = new StringBuilder();
        builder.AppendLine("digraph PositionSlots {");

        foreach (var node in graph.Nodes.OrderBy(static node => node.Code))
        {
            var label = EscapeDot($"{node.Code} - {node.Label}");
            builder.AppendLine($"  \"{node.Id:D}\" [label=\"{label}\"];\n");
        }

        foreach (var edge in graph.Edges)
        {
            builder.AppendLine(
                $"  \"{edge.FromId:D}\" -> \"{edge.ToId:D}\" [label=\"{edge.RelationType}\"];\n");
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string BuildGraphMl(PositionSlotGraphResponse graph)
    {
        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = false,
            Encoding = Encoding.UTF8,
            Indent = true
        };

        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, settings))
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

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string EscapeDot(string value) => value.Replace("\"", "\\\"");

    public sealed record CreatePositionSlotRequest(
        string Code,
        string? Title,
        Guid JobProfilePublicId,
        Guid? RolePublicId,
        Guid? WorkCenterPublicId,
        Guid? DirectDependencyPositionSlotPublicId,
        Guid? FunctionalDependencyPositionSlotPublicId,
        PositionSlotStatus Status,
        int MaxEmployees,
        int OccupiedEmployees,
        DateTime EffectiveFromUtc,
        DateTime? EffectiveToUtc,
        string? Notes);

    public sealed record UpdatePositionSlotRequest(
        string Code,
        string? Title,
        Guid JobProfilePublicId,
        Guid? RolePublicId,
        Guid? WorkCenterPublicId,
        int MaxEmployees,
        DateTime EffectiveFromUtc,
        DateTime? EffectiveToUtc,
        string? Notes,
        Guid ConcurrencyToken);

    public sealed record UpdatePositionSlotStatusRequest(PositionSlotStatus Status, Guid ConcurrencyToken);

    public sealed record UpdatePositionSlotDependenciesRequest(
        Guid? DirectDependencyPositionSlotPublicId,
        Guid? FunctionalDependencyPositionSlotPublicId,
        Guid ConcurrencyToken);

    public sealed record UpdatePositionSlotOccupancyRequest(int OccupiedEmployees, Guid ConcurrencyToken);
}
