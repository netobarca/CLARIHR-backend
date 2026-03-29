using System.Globalization;
using System.IO.Compression;
using System.Text;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.SalaryTabulator;
using CLARIHR.Application.Features.SalaryTabulator.Common;
using CLARIHR.Domain.SalaryTabulator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class SalaryTabulatorController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    IAuditService auditService,
    IUnitOfWork unitOfWork) : ControllerBase
{
    [HttpGet("api/v1/companies/{companyId:guid}/salary-tabulator")]
    [ProducesResponseType<PagedResponse<SalaryTabulatorLineListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<SalaryTabulatorLineListItemResponse>>> SearchLines(
        Guid companyId,
        [FromQuery] Guid? salaryClassId,
        [FromQuery] string? salaryScale,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = SalaryTabulatorValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchSalaryTabulatorLinesQuery(
                companyId,
                salaryClassId,
                salaryScale,
                isActive,
                search,
                page,
                pageSize,
                includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/salary-tabulator/lines/{id:guid}")]
    [ProducesResponseType<SalaryTabulatorLineResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SalaryTabulatorLineResponse>> GetLineById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetSalaryTabulatorLineByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/salary-tabulator/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportLines(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] Guid? salaryClassId = null,
        [FromQuery] string? salaryScale = null,
        [FromQuery] bool? isActive = null,
        [FromQuery(Name = "q")] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportSalaryTabulatorLinesQuery(companyId, salaryClassId, salaryScale, isActive, search),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<SalaryTabulatorLineExportRow>>.Failure(result.Error)).Result!;
        }

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.ReportExported,
                    AuditEntityTypes.SalaryTabulatorLine,
                    null,
                    SalaryTabulatorPermissionCodes.ResourceKey,
                    AuditActions.Export,
                    "Exported salary tabulator lines report.",
                    After: new
                    {
                        resourceKey = SalaryTabulatorPermissionCodes.ResourceKey,
                        format = "csv",
                        filters = new { salaryClassId, salaryScale, isActive, q = search },
                        rowCount = result.Value.Count
                    }),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var csv = BuildCsv(result.Value);
            return File(Encoding.UTF8.GetBytes(csv), "text/csv", "salary-tabulator.csv");
        }

        if (string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.ReportExported,
                    AuditEntityTypes.SalaryTabulatorLine,
                    null,
                    SalaryTabulatorPermissionCodes.ResourceKey,
                    AuditActions.Export,
                    "Exported salary tabulator lines report.",
                    After: new
                    {
                        resourceKey = SalaryTabulatorPermissionCodes.ResourceKey,
                        format = "xlsx",
                        filters = new { salaryClassId, salaryScale, isActive, q = search },
                        rowCount = result.Value.Count
                    }),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var xlsx = BuildXlsx(result.Value);
            return File(
                xlsx,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "salary-tabulator.xlsx");
        }

        return this.ToActionResult(Result<IReadOnlyCollection<SalaryTabulatorLineExportRow>>.Failure(SalaryTabulatorErrors.ExportFormatInvalid)).Result!;
    }

    [HttpGet("api/v1/companies/{companyId:guid}/salary-tabulator/change-requests")]
    [ProducesResponseType<PagedResponse<SalaryTabulatorChangeRequestListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<SalaryTabulatorChangeRequestListItemResponse>>> SearchRequests(
        Guid companyId,
        [FromQuery] SalaryTabulatorChangeRequestStatus? status,
        [FromQuery] Guid? requestedBy,
        [FromQuery] DateTime? effectiveFrom,
        [FromQuery] DateTime? effectiveTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = SalaryTabulatorValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchSalaryTabulatorChangeRequestsQuery(
                companyId,
                status,
                requestedBy,
                effectiveFrom,
                effectiveTo,
                page,
                pageSize,
                includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/salary-tabulator/change-requests/{id:guid}")]
    [ProducesResponseType<SalaryTabulatorChangeRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SalaryTabulatorChangeRequestResponse>> GetRequestById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetSalaryTabulatorChangeRequestByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/salary-tabulator/change-requests/{id:guid}/impact")]
    [ProducesResponseType<SalaryTabulatorChangeRequestImpactResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SalaryTabulatorChangeRequestImpactResponse>> GetRequestImpact(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetSalaryTabulatorChangeRequestImpactQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/companies/{companyId:guid}/salary-tabulator/change-requests")]
    [ProducesResponseType<SalaryTabulatorChangeRequestResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SalaryTabulatorChangeRequestResponse>> CreateRequest(
        Guid companyId,
        [FromBody] CreateSalaryTabulatorChangeRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateSalaryTabulatorChangeRequestCommand(
                companyId,
                request.Reason,
                request.EffectiveFromUtc,
                MapItems(request.Items)),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<SalaryTabulatorChangeRequestResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/salary-tabulator/change-requests/{id:guid}")]
    [ProducesResponseType<SalaryTabulatorChangeRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SalaryTabulatorChangeRequestResponse>> UpdateRequest(
        Guid id,
        [FromBody] UpdateSalaryTabulatorChangeRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateSalaryTabulatorChangeRequestCommand(
                id,
                request.Reason,
                request.EffectiveFromUtc,
                MapItems(request.Items),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/salary-tabulator/change-requests/{id:guid}/submit")]
    [ProducesResponseType<SalaryTabulatorChangeRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SalaryTabulatorChangeRequestResponse>> SubmitRequest(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new SubmitSalaryTabulatorChangeRequestCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/salary-tabulator/change-requests/{id:guid}/approve")]
    [ProducesResponseType<SalaryTabulatorChangeRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SalaryTabulatorChangeRequestResponse>> ApproveRequest(
        Guid id,
        [FromBody] ApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ApproveSalaryTabulatorChangeRequestCommand(id, request.DecisionComment, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/salary-tabulator/change-requests/{id:guid}/reject")]
    [ProducesResponseType<SalaryTabulatorChangeRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SalaryTabulatorChangeRequestResponse>> RejectRequest(
        Guid id,
        [FromBody] ApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new RejectSalaryTabulatorChangeRequestCommand(id, request.DecisionComment, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/salary-tabulator/change-requests/{id:guid}/cancel")]
    [ProducesResponseType<SalaryTabulatorChangeRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SalaryTabulatorChangeRequestResponse>> CancelRequest(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CancelSalaryTabulatorChangeRequestCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    private static IReadOnlyCollection<SalaryTabulatorChangeRequestItemInput> MapItems(IReadOnlyCollection<SalaryTabulatorChangeRequestItemRequest> items) =>
        items.Select(item => new SalaryTabulatorChangeRequestItemInput(
                item.SalaryClassPublicId,
                item.SalaryScaleCode,
                item.CurrencyCode,
                item.ChangeType,
                item.ProposedBaseAmount,
                item.ProposedMinAmount,
                item.ProposedMaxAmount,
                item.Notes))
            .ToArray();

    private static string BuildCsv(IReadOnlyCollection<SalaryTabulatorLineExportRow> rows)
    {
        var lines = new List<string>
        {
            "PublicId,SalaryClassPublicId,SalaryScaleCode,CurrencyCode,BaseAmount,MinAmount,MaxAmount,EffectiveFromUtc,EffectiveToUtc,IsActive,Version,Notes,CreatedAtUtc,ModifiedAtUtc"
        };

        lines.AddRange(rows.Select(row => string.Join(",",
            EscapeCsv(row.Id.ToString()),
            EscapeCsv(row.SalaryClassId?.ToString()),
            EscapeCsv(row.SalaryScaleCode),
            EscapeCsv(row.CurrencyCode),
            row.BaseAmount.ToString(CultureInfo.InvariantCulture),
            EscapeCsv(row.MinAmount?.ToString(CultureInfo.InvariantCulture)),
            EscapeCsv(row.MaxAmount?.ToString(CultureInfo.InvariantCulture)),
            EscapeCsv(row.EffectiveFromUtc.ToString("O", CultureInfo.InvariantCulture)),
            EscapeCsv(row.EffectiveToUtc?.ToString("O", CultureInfo.InvariantCulture)),
            row.IsActive ? "true" : "false",
            row.Version.ToString(CultureInfo.InvariantCulture),
            EscapeCsv(row.Notes),
            EscapeCsv(row.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
            EscapeCsv(row.ModifiedAtUtc?.ToString("O", CultureInfo.InvariantCulture)))));

        return string.Join("\n", lines);
    }

    private static byte[] BuildXlsx(IReadOnlyCollection<SalaryTabulatorLineExportRow> rows)
    {
        static string Cell(string? value) =>
            $"<c t=\"inlineStr\"><is><t>{EscapeXml(value)}</t></is></c>";

        var headers = new[]
        {
            "PublicId",
            "SalaryClassPublicId",
            "SalaryScaleCode",
            "CurrencyCode",
            "BaseAmount",
            "MinAmount",
            "MaxAmount",
            "EffectiveFromUtc",
            "EffectiveToUtc",
            "IsActive",
            "Version",
            "Notes",
            "CreatedAtUtc",
            "ModifiedAtUtc"
        };

        var rowsBuilder = new StringBuilder();
        rowsBuilder.Append("<row r=\"1\">");
        foreach (var header in headers)
        {
            rowsBuilder.Append(Cell(header));
        }

        rowsBuilder.Append("</row>");

        var rowIndex = 2;
        foreach (var row in rows)
        {
            rowsBuilder.Append($"<row r=\"{rowIndex++}\">");
            rowsBuilder.Append(Cell(row.Id.ToString()));
            rowsBuilder.Append(Cell(row.SalaryClassId?.ToString()));
            rowsBuilder.Append(Cell(row.SalaryScaleCode));
            rowsBuilder.Append(Cell(row.CurrencyCode));
            rowsBuilder.Append(Cell(row.BaseAmount.ToString(CultureInfo.InvariantCulture)));
            rowsBuilder.Append(Cell(row.MinAmount?.ToString(CultureInfo.InvariantCulture)));
            rowsBuilder.Append(Cell(row.MaxAmount?.ToString(CultureInfo.InvariantCulture)));
            rowsBuilder.Append(Cell(row.EffectiveFromUtc.ToString("O", CultureInfo.InvariantCulture)));
            rowsBuilder.Append(Cell(row.EffectiveToUtc?.ToString("O", CultureInfo.InvariantCulture)));
            rowsBuilder.Append(Cell(row.IsActive ? "true" : "false"));
            rowsBuilder.Append(Cell(row.Version.ToString(CultureInfo.InvariantCulture)));
            rowsBuilder.Append(Cell(row.Notes));
            rowsBuilder.Append(Cell(row.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)));
            rowsBuilder.Append(Cell(row.ModifiedAtUtc?.ToString("O", CultureInfo.InvariantCulture)));
            rowsBuilder.Append("</row>");
        }

        var sheetXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<sheetData>" + rowsBuilder + "</sheetData>" +
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
            "<sheets><sheet name=\"SalaryTabulator\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
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

        return System.Security.SecurityElement.Escape(value) ?? string.Empty;
    }

    public sealed record CreateSalaryTabulatorChangeRequestRequest(
        string Reason,
        DateTime EffectiveFromUtc,
        IReadOnlyCollection<SalaryTabulatorChangeRequestItemRequest> Items);

    public sealed record UpdateSalaryTabulatorChangeRequestRequest(
        string Reason,
        DateTime EffectiveFromUtc,
        IReadOnlyCollection<SalaryTabulatorChangeRequestItemRequest> Items,
        Guid ConcurrencyToken);

    public sealed record SalaryTabulatorChangeRequestItemRequest(
        Guid SalaryClassPublicId,
        string SalaryScaleCode,
        string CurrencyCode,
        SalaryTabulatorChangeType ChangeType,
        decimal? ProposedBaseAmount,
        decimal? ProposedMinAmount,
        decimal? ProposedMaxAmount,
        string? Notes);

    public sealed record ConcurrencyRequest(Guid ConcurrencyToken);

    public sealed record ApprovalRequest(string DecisionComment, Guid ConcurrencyToken);
}
