using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Xml;
using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PositionSlots;
using CLARIHR.Application.Features.PositionSlots.Common;
using CLARIHR.Domain.PositionSlots;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class PositionSlotsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
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
        [FromQuery] bool? isFixedTerm,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PositionSlotValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchPositionSlotsQuery(companyId, status, jobProfileId, orgUnitId, workCenterId, isFixedTerm, search, page, pageSize),
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

        if (string.Equals(format, "graphml", StringComparison.OrdinalIgnoreCase))
        {
            var graphml = BuildGraphMl(graphResult.Value);
            return File(Encoding.UTF8.GetBytes(graphml), "application/graphml+xml", "position-slots-diagram.graphml");
        }

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            var json = JsonSerializer.Serialize(graphResult.Value);
            return File(Encoding.UTF8.GetBytes(json), "application/json", "position-slots-diagram.json");
        }

        if (string.Equals(format, "dot", StringComparison.OrdinalIgnoreCase))
        {
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
    public async Task<IActionResult> Export(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] PositionSlotStatus? status = null,
        [FromQuery] Guid? jobProfileId = null,
        [FromQuery] Guid? orgUnitId = null,
        [FromQuery] Guid? workCenterId = null,
        [FromQuery] bool? isFixedTerm = null,
        [FromQuery(Name = "q")] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPositionSlotExportRowsQuery(companyId, status, jobProfileId, orgUnitId, workCenterId, isFixedTerm, search),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<PositionSlotExportRow>>.Failure(result.Error)).Result!;
        }

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var csv = BuildCsv(result.Value);
            return File(Encoding.UTF8.GetBytes(csv), "text/csv", "position-slots.csv");
        }

        if (string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            var xlsx = BuildXlsx(result.Value);
            return File(
                xlsx,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "position-slots.xlsx");
        }

        return this.ToActionResult(Result<IReadOnlyCollection<PositionSlotExportRow>>.Failure(PositionSlotErrors.ExportFormatInvalid)).Result!;
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
                request.JobProfileId,
                request.OrgUnitId,
                request.WorkCenterId,
                request.CostCenterCode,
                request.DirectDependencyPositionSlotId,
                request.FunctionalDependencyPositionSlotId,
                request.Status,
                request.MaxEmployees,
                request.OccupiedEmployees,
                request.IsFixedTerm,
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
                request.JobProfileId,
                request.OrgUnitId,
                request.WorkCenterId,
                request.CostCenterCode,
                request.MaxEmployees,
                request.IsFixedTerm,
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
                request.DirectDependencyPositionSlotId,
                request.FunctionalDependencyPositionSlotId,
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

    private static string BuildCsv(IReadOnlyCollection<PositionSlotExportRow> rows)
    {
        var lines = new List<string>
        {
            "Id,Code,Title,Status,JobProfileCode,JobProfileTitle,OrgUnitCode,OrgUnitName,WorkCenterCode,WorkCenterName,CostCenterCode,DirectDependencyCode,FunctionalDependencyCode,MaxEmployees,OccupiedEmployees,IsFixedTerm,EffectiveFromUtc,EffectiveToUtc,IsActive,CreatedAtUtc,ModifiedAtUtc"
        };

        lines.AddRange(rows.Select(row => string.Join(",",
            EscapeCsv(row.Id.ToString()),
            EscapeCsv(row.Code),
            EscapeCsv(row.Title),
            EscapeCsv(row.Status.ToString()),
            EscapeCsv(row.JobProfileCode),
            EscapeCsv(row.JobProfileTitle),
            EscapeCsv(row.OrgUnitCode),
            EscapeCsv(row.OrgUnitName),
            EscapeCsv(row.WorkCenterCode),
            EscapeCsv(row.WorkCenterName),
            EscapeCsv(row.CostCenterCode),
            EscapeCsv(row.DirectDependencyCode),
            EscapeCsv(row.FunctionalDependencyCode),
            row.MaxEmployees.ToString(CultureInfo.InvariantCulture),
            row.OccupiedEmployees.ToString(CultureInfo.InvariantCulture),
            row.IsFixedTerm ? "true" : "false",
            EscapeCsv(row.EffectiveFromUtc.ToString("O", CultureInfo.InvariantCulture)),
            EscapeCsv(row.EffectiveToUtc?.ToString("O", CultureInfo.InvariantCulture)),
            row.IsActive ? "true" : "false",
            EscapeCsv(row.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
            EscapeCsv(row.ModifiedAtUtc?.ToString("O", CultureInfo.InvariantCulture)))));

        return string.Join("\n", lines);
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

    private static byte[] BuildXlsx(IReadOnlyCollection<PositionSlotExportRow> rows)
    {
        static string Cell(string? value) =>
            $"<c t=\"inlineStr\"><is><t>{EscapeXml(value)}</t></is></c>";

        var sheetRows = new StringBuilder();
        var headers = new[]
        {
            "Id",
            "Code",
            "Title",
            "Status",
            "JobProfileCode",
            "JobProfileTitle",
            "OrgUnitCode",
            "OrgUnitName",
            "WorkCenterCode",
            "WorkCenterName",
            "CostCenterCode",
            "DirectDependencyCode",
            "FunctionalDependencyCode",
            "MaxEmployees",
            "OccupiedEmployees",
            "IsFixedTerm",
            "EffectiveFromUtc",
            "EffectiveToUtc",
            "IsActive",
            "CreatedAtUtc",
            "ModifiedAtUtc"
        };

        sheetRows.Append("<row r=\"1\">");
        foreach (var header in headers)
        {
            sheetRows.Append(Cell(header));
        }

        sheetRows.Append("</row>");

        var rowIndex = 2;
        foreach (var row in rows)
        {
            sheetRows.Append($"<row r=\"{rowIndex++}\">");
            sheetRows.Append(Cell(row.Id.ToString()));
            sheetRows.Append(Cell(row.Code));
            sheetRows.Append(Cell(row.Title));
            sheetRows.Append(Cell(row.Status.ToString()));
            sheetRows.Append(Cell(row.JobProfileCode));
            sheetRows.Append(Cell(row.JobProfileTitle));
            sheetRows.Append(Cell(row.OrgUnitCode));
            sheetRows.Append(Cell(row.OrgUnitName));
            sheetRows.Append(Cell(row.WorkCenterCode));
            sheetRows.Append(Cell(row.WorkCenterName));
            sheetRows.Append(Cell(row.CostCenterCode));
            sheetRows.Append(Cell(row.DirectDependencyCode));
            sheetRows.Append(Cell(row.FunctionalDependencyCode));
            sheetRows.Append(Cell(row.MaxEmployees.ToString(CultureInfo.InvariantCulture)));
            sheetRows.Append(Cell(row.OccupiedEmployees.ToString(CultureInfo.InvariantCulture)));
            sheetRows.Append(Cell(row.IsFixedTerm ? "true" : "false"));
            sheetRows.Append(Cell(row.EffectiveFromUtc.ToString("O", CultureInfo.InvariantCulture)));
            sheetRows.Append(Cell(row.EffectiveToUtc?.ToString("O", CultureInfo.InvariantCulture)));
            sheetRows.Append(Cell(row.IsActive ? "true" : "false"));
            sheetRows.Append(Cell(row.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)));
            sheetRows.Append(Cell(row.ModifiedAtUtc?.ToString("O", CultureInfo.InvariantCulture)));
            sheetRows.Append("</row>");
        }

        var sheetXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<sheetData>" + sheetRows + "</sheetData>" +
            "</worksheet>";

        var contentTypesXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
            "</Types>";

        var relsXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>";

        var workbookXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            "<sheets><sheet name=\"PositionSlots\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
            "</workbook>";

        var workbookRelsXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
            "</Relationships>";

        var stylesXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
            "<fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills>" +
            "<borders count=\"1\"><border/></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/></cellXfs>" +
            "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
            "</styleSheet>";

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", contentTypesXml);
            WriteEntry(archive, "_rels/.rels", relsXml);
            WriteEntry(archive, "xl/workbook.xml", workbookXml);
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", workbookRelsXml);
            WriteEntry(archive, "xl/worksheets/sheet1.xml", sheetXml);
            WriteEntry(archive, "xl/styles.xml", stylesXml);
        }

        return stream.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        var escaped = value.Replace("\"", "\"\"");
        return needsQuotes ? $"\"{escaped}\"" : escaped;
    }

    private static string EscapeXml(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return SecurityElement.Escape(value) ?? string.Empty;
    }

    private static string EscapeDot(string value) => value.Replace("\"", "\\\"");

    public sealed record CreatePositionSlotRequest(
        string Code,
        string? Title,
        Guid JobProfileId,
        Guid OrgUnitId,
        Guid? WorkCenterId,
        string? CostCenterCode,
        Guid? DirectDependencyPositionSlotId,
        Guid? FunctionalDependencyPositionSlotId,
        PositionSlotStatus Status,
        int MaxEmployees,
        int OccupiedEmployees,
        bool IsFixedTerm,
        DateTime EffectiveFromUtc,
        DateTime? EffectiveToUtc,
        string? Notes);

    public sealed record UpdatePositionSlotRequest(
        string Code,
        string? Title,
        Guid JobProfileId,
        Guid OrgUnitId,
        Guid? WorkCenterId,
        string? CostCenterCode,
        int MaxEmployees,
        bool IsFixedTerm,
        DateTime EffectiveFromUtc,
        DateTime? EffectiveToUtc,
        string? Notes,
        Guid ConcurrencyToken);

    public sealed record UpdatePositionSlotStatusRequest(PositionSlotStatus Status, Guid ConcurrencyToken);

    public sealed record UpdatePositionSlotDependenciesRequest(
        Guid? DirectDependencyPositionSlotId,
        Guid? FunctionalDependencyPositionSlotId,
        Guid ConcurrencyToken);

    public sealed record UpdatePositionSlotOccupancyRequest(int OccupiedEmployees, Guid ConcurrencyToken);
}
