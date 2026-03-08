using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Xml;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.OrgUnits;
using CLARIHR.Application.Features.OrgUnits.Common;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.OrgUnits;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class OrgUnitsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    IAuditService auditService,
    IUnitOfWork unitOfWork) : ControllerBase
{
    [HttpGet("api/v1/companies/{companyId:guid}/org-units")]
    [ProducesResponseType<PagedResponse<OrgUnitResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<OrgUnitResponse>>> Search(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery] OrgUnitType? type,
        [FromQuery] Guid? parentId,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchOrgUnitsQuery(companyId, isActive, q, type, parentId, page, pageSize, includeAllowedActions),
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
    public async Task<IActionResult> Export(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] bool? isActive = null,
        [FromQuery] OrgUnitType? type = null,
        [FromQuery] Guid? parentId = null,
        [FromQuery(Name = "q")] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetOrgUnitExportRowsQuery(companyId, isActive, search, type, parentId),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<OrgUnitExportRow>>.Failure(result.Error)).Result!;
        }

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.ReportExported,
                    AuditEntityTypes.OrgUnit,
                    null,
                    OrgUnitPermissionCodes.ResourceKey,
                    AuditActions.Export,
                    "Exported org units report.",
                    After: new
                    {
                        resourceKey = OrgUnitPermissionCodes.ResourceKey,
                        format = "csv",
                        filters = new { isActive, type, parentId, q = search },
                        rowCount = result.Value.Count
                    }),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var csv = BuildCsv(result.Value);
            return File(Encoding.UTF8.GetBytes(csv), "text/csv", "org-units.csv");
        }

        if (string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.ReportExported,
                    AuditEntityTypes.OrgUnit,
                    null,
                    OrgUnitPermissionCodes.ResourceKey,
                    AuditActions.Export,
                    "Exported org units report.",
                    After: new
                    {
                        resourceKey = OrgUnitPermissionCodes.ResourceKey,
                        format = "xlsx",
                        filters = new { isActive, type, parentId, q = search },
                        rowCount = result.Value.Count
                    }),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var xlsx = BuildXlsx(result.Value);
            return File(
                xlsx,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "org-units.xlsx");
        }

        return this.ToActionResult(Result<IReadOnlyCollection<OrgUnitExportRow>>.Failure(ReportPolicyErrors.FormatNotSupported)).Result!;
    }

    [HttpGet("api/v1/companies/{companyId:guid}/org-units/diagram-export")]
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
        CancellationToken cancellationToken = default)
    {
        var graphResult = await queryDispatcher.SendAsync(
            new GetOrgUnitGraphQuery(companyId, rootId, depth),
            cancellationToken);

        if (graphResult.IsFailure)
        {
            return this.ToActionResult(Result<OrgUnitGraphResponse>.Failure(graphResult.Error)).Result!;
        }

        if (string.Equals(format, "graphml", StringComparison.OrdinalIgnoreCase))
        {
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.ReportExported,
                    AuditEntityTypes.OrgUnit,
                    null,
                    OrgUnitPermissionCodes.ResourceKey,
                    AuditActions.Export,
                    "Exported org units diagram.",
                    After: new
                    {
                        resourceKey = OrgUnitPermissionCodes.ResourceKey,
                        format = "graphml",
                        filters = new { rootId, depth },
                        rowCount = graphResult.Value.Nodes.Count
                    }),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var graphml = BuildGraphMl(graphResult.Value);
            return File(Encoding.UTF8.GetBytes(graphml), "application/graphml+xml", "org-units-diagram.graphml");
        }

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.ReportExported,
                    AuditEntityTypes.OrgUnit,
                    null,
                    OrgUnitPermissionCodes.ResourceKey,
                    AuditActions.Export,
                    "Exported org units diagram.",
                    After: new
                    {
                        resourceKey = OrgUnitPermissionCodes.ResourceKey,
                        format = "json",
                        filters = new { rootId, depth },
                        rowCount = graphResult.Value.Nodes.Count
                    }),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var json = JsonSerializer.Serialize(graphResult.Value);
            return File(Encoding.UTF8.GetBytes(json), "application/json", "org-units-diagram.json");
        }

        if (string.Equals(format, "dot", StringComparison.OrdinalIgnoreCase))
        {
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.ReportExported,
                    AuditEntityTypes.OrgUnit,
                    null,
                    OrgUnitPermissionCodes.ResourceKey,
                    AuditActions.Export,
                    "Exported org units diagram.",
                    After: new
                    {
                        resourceKey = OrgUnitPermissionCodes.ResourceKey,
                        format = "dot",
                        filters = new { rootId, depth },
                        rowCount = graphResult.Value.Nodes.Count
                    }),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

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
                request.UnitType,
                request.ParentId,
                request.SortOrder,
                request.Description,
                request.CostCenterCode,
                request.ManagerEmployeeId),
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
                request.UnitType,
                request.SortOrder,
                request.Description,
                request.CostCenterCode,
                request.ManagerEmployeeId,
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
            new MoveOrgUnitCommand(id, request.NewParentId, request.SortOrder, request.ConcurrencyToken),
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

    private static string BuildCsv(IReadOnlyCollection<OrgUnitExportRow> rows)
    {
        var lines = new List<string>
        {
            "Id,Code,Name,UnitType,ParentCode,ParentName,SortOrder,Description,CostCenterCode,ManagerEmployeeId,IsActive,CreatedAtUtc,ModifiedAtUtc"
        };

        lines.AddRange(rows.Select(row => string.Join(",",
            EscapeCsv(row.Id.ToString()),
            EscapeCsv(row.Code),
            EscapeCsv(row.Name),
            EscapeCsv(row.UnitType.ToString()),
            EscapeCsv(row.ParentCode),
            EscapeCsv(row.ParentName),
            EscapeCsv(row.SortOrder?.ToString(CultureInfo.InvariantCulture)),
            EscapeCsv(row.Description),
            EscapeCsv(row.CostCenterCode),
            EscapeCsv(row.ManagerEmployeeId?.ToString()),
            row.IsActive ? "true" : "false",
            EscapeCsv(row.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
            EscapeCsv(row.ModifiedAtUtc?.ToString("O", CultureInfo.InvariantCulture)))));

        return string.Join("\n", lines);
    }

    private static byte[] BuildXlsx(IReadOnlyCollection<OrgUnitExportRow> rows)
    {
        static string Cell(string? value) =>
            $"<c t=\"inlineStr\"><is><t>{EscapeXml(value)}</t></is></c>";

        var headers = new[]
        {
            "Id",
            "Code",
            "Name",
            "UnitType",
            "ParentCode",
            "ParentName",
            "SortOrder",
            "Description",
            "CostCenterCode",
            "ManagerEmployeeId",
            "IsActive",
            "CreatedAtUtc",
            "ModifiedAtUtc"
        };

        var sheetRows = new StringBuilder();
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
            sheetRows.Append(Cell(row.Name));
            sheetRows.Append(Cell(row.UnitType.ToString()));
            sheetRows.Append(Cell(row.ParentCode));
            sheetRows.Append(Cell(row.ParentName));
            sheetRows.Append(Cell(row.SortOrder?.ToString(CultureInfo.InvariantCulture)));
            sheetRows.Append(Cell(row.Description));
            sheetRows.Append(Cell(row.CostCenterCode));
            sheetRows.Append(Cell(row.ManagerEmployeeId?.ToString()));
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
            "<sheets><sheet name=\"OrgUnits\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
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
                writer.WriteString(node.Type.ToString());
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

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return escaped.IndexOfAny([',', '\n', '\r', '"']) >= 0
            ? $"\"{escaped}\""
            : escaped;
    }

    private static string EscapeXml(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : SecurityElement.Escape(value)!;

    private static string EscapeDot(string? value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    public sealed record CreateOrgUnitRequest(
        string Code,
        string Name,
        OrgUnitType UnitType,
        Guid? ParentId,
        int? SortOrder,
        string? Description,
        string? CostCenterCode,
        Guid? ManagerEmployeeId);

    public sealed record UpdateOrgUnitRequest(
        string Code,
        string Name,
        OrgUnitType UnitType,
        int? SortOrder,
        string? Description,
        string? CostCenterCode,
        Guid? ManagerEmployeeId,
        Guid ConcurrencyToken);

    public sealed record MoveOrgUnitRequest(Guid? NewParentId, int? SortOrder, Guid ConcurrencyToken);

    public sealed record ConcurrencyRequest(Guid ConcurrencyToken);
}
