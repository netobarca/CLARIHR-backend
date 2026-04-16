using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.LegalRepresentatives;
using CLARIHR.Application.Features.LegalRepresentatives.Common;
using CLARIHR.Domain.LegalRepresentatives;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class LegalRepresentativesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    IAuditService auditService,
    IUnitOfWork unitOfWork) : ControllerBase
{
    [HttpGet("api/v1/companies/{companyId:guid}/legal-representatives")]
    [ProducesResponseType<PagedResponse<LegalRepresentativeListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<LegalRepresentativeListItemResponse>>> Search(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery] bool? isPrimary,
        [FromQuery] LegalRepresentativeRepresentationType? representationType,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = LegalRepresentativeValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchLegalRepresentativesQuery(
                companyId,
                isActive,
                isPrimary,
                representationType,
                search,
                page,
                pageSize,
                includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/legal-representatives/{id:guid}")]
    [ProducesResponseType<LegalRepresentativeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LegalRepresentativeResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetLegalRepresentativeByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/legal-representatives/{id:guid}/usage")]
    [ProducesResponseType<LegalRepresentativeUsageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LegalRepresentativeUsageResponse>> Usage(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetLegalRepresentativeUsageQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/legal-representatives/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Export(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] bool? isActive = null,
        [FromQuery] bool? isPrimary = null,
        [FromQuery] LegalRepresentativeRepresentationType? representationType = null,
        [FromQuery(Name = "q")] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportLegalRepresentativesQuery(companyId, isActive, isPrimary, representationType, search),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<LegalRepresentativeExportRow>>.Failure(result.Error)).Result!;
        }

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.ReportExported,
                    AuditEntityTypes.LegalRepresentative,
                    null,
                    LegalRepresentativePermissionCodes.ResourceKey,
                    AuditActions.Export,
                    "Exported legal representatives report.",
                    After: new
                    {
                        resourceKey = LegalRepresentativePermissionCodes.ResourceKey,
                        format = "csv",
                        filters = new { isActive, isPrimary, representationType, q = search },
                        rowCount = result.Value.Count
                    }),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var csv = BuildCsv(result.Value);
            return File(Encoding.UTF8.GetBytes(csv), "text/csv", "legal-representatives.csv");
        }

        if (string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.ReportExported,
                    AuditEntityTypes.LegalRepresentative,
                    null,
                    LegalRepresentativePermissionCodes.ResourceKey,
                    AuditActions.Export,
                    "Exported legal representatives report.",
                    After: new
                    {
                        resourceKey = LegalRepresentativePermissionCodes.ResourceKey,
                        format = "xlsx",
                        filters = new { isActive, isPrimary, representationType, q = search },
                        rowCount = result.Value.Count
                    }),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var xlsx = BuildXlsx(result.Value);
            return File(
                xlsx,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "legal-representatives.xlsx");
        }

        return this.ToActionResult(Result<IReadOnlyCollection<LegalRepresentativeExportRow>>.Failure(LegalRepresentativeErrors.ExportFormatInvalid)).Result!;
    }

    [HttpPost("api/v1/companies/{companyId:guid}/legal-representatives")]
    [ProducesResponseType<LegalRepresentativeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LegalRepresentativeResponse>> Create(
        Guid companyId,
        [FromBody] CreateLegalRepresentativeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateLegalRepresentativeCommand(
                companyId,
                request.FirstName,
                request.LastName,
                request.DocumentType,
                request.DocumentNumber,
                request.PositionTitle,
                request.RepresentationType,
                request.AuthorityDescription,
                request.AppointmentInstrument,
                request.AppointmentDateUtc,
                request.EffectiveFromUtc,
                request.EffectiveToUtc,
                request.Email,
                request.Phone,
                request.IsPrimary),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<LegalRepresentativeResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/legal-representatives/{id:guid}")]
    [ProducesResponseType<LegalRepresentativeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LegalRepresentativeResponse>> Update(
        Guid id,
        [FromBody] UpdateLegalRepresentativeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateLegalRepresentativeCommand(
                id,
                request.FirstName,
                request.LastName,
                request.DocumentType,
                request.DocumentNumber,
                request.PositionTitle,
                request.RepresentationType,
                request.AuthorityDescription,
                request.AppointmentInstrument,
                request.AppointmentDateUtc,
                request.EffectiveFromUtc,
                request.EffectiveToUtc,
                request.Email,
                request.Phone,
                request.IsPrimary,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/legal-representatives/{id:guid}/activate")]
    [ProducesResponseType<LegalRepresentativeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LegalRepresentativeResponse>> Activate(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateLegalRepresentativeCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/legal-representatives/{id:guid}/inactivate")]
    [ProducesResponseType<LegalRepresentativeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LegalRepresentativeResponse>> Inactivate(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateLegalRepresentativeCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/legal-representatives/{id:guid}/set-primary")]
    [ProducesResponseType<LegalRepresentativeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LegalRepresentativeResponse>> SetPrimary(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new SetPrimaryLegalRepresentativeCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    private static string BuildCsv(IReadOnlyCollection<LegalRepresentativeExportRow> rows)
    {
        var lines = new List<string>
        {
            "PublicId,FirstName,LastName,FullName,DocumentType,DocumentNumber,PositionTitle,RepresentationType,AuthorityDescription,AppointmentInstrument,AppointmentDateUtc,EffectiveFromUtc,EffectiveToUtc,Email,Phone,IsPrimary,IsActive,CreatedAtUtc,ModifiedAtUtc"
        };

        lines.AddRange(rows.Select(row => string.Join(",",
            EscapeCsv(row.Id.ToString()),
            EscapeCsv(row.FirstName),
            EscapeCsv(row.LastName),
            EscapeCsv(row.FullName),
            EscapeCsv(row.DocumentType),
            EscapeCsv(row.DocumentNumber),
            EscapeCsv(row.PositionTitle),
            EscapeCsv(row.RepresentationType.ToString()),
            EscapeCsv(row.AuthorityDescription),
            EscapeCsv(row.AppointmentInstrument),
            EscapeCsv(row.AppointmentDateUtc?.ToString("O", CultureInfo.InvariantCulture)),
            EscapeCsv(row.EffectiveFromUtc.ToString("O", CultureInfo.InvariantCulture)),
            EscapeCsv(row.EffectiveToUtc?.ToString("O", CultureInfo.InvariantCulture)),
            EscapeCsv(row.Email),
            EscapeCsv(row.Phone),
            FormatNullableBoolean(row.IsPrimary),
            row.IsActive ? "true" : "false",
            EscapeCsv(row.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
            EscapeCsv(row.ModifiedAtUtc?.ToString("O", CultureInfo.InvariantCulture)))));

        return string.Join("\n", lines);
    }

    private static byte[] BuildXlsx(IReadOnlyCollection<LegalRepresentativeExportRow> rows)
    {
        static string Cell(string? value) =>
            $"<c t=\"inlineStr\"><is><t>{EscapeXml(value)}</t></is></c>";

        var headers = new[]
        {
            "PublicId",
            "FirstName",
            "LastName",
            "FullName",
            "DocumentType",
            "DocumentNumber",
            "PositionTitle",
            "RepresentationType",
            "AuthorityDescription",
            "AppointmentInstrument",
            "AppointmentDateUtc",
            "EffectiveFromUtc",
            "EffectiveToUtc",
            "Email",
            "Phone",
            "IsPrimary",
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
            sheetRows.Append(Cell(row.FirstName));
            sheetRows.Append(Cell(row.LastName));
            sheetRows.Append(Cell(row.FullName));
            sheetRows.Append(Cell(row.DocumentType));
            sheetRows.Append(Cell(row.DocumentNumber));
            sheetRows.Append(Cell(row.PositionTitle));
            sheetRows.Append(Cell(row.RepresentationType.ToString()));
            sheetRows.Append(Cell(row.AuthorityDescription));
            sheetRows.Append(Cell(row.AppointmentInstrument));
            sheetRows.Append(Cell(row.AppointmentDateUtc?.ToString("O", CultureInfo.InvariantCulture)));
            sheetRows.Append(Cell(row.EffectiveFromUtc.ToString("O", CultureInfo.InvariantCulture)));
            sheetRows.Append(Cell(row.EffectiveToUtc?.ToString("O", CultureInfo.InvariantCulture)));
            sheetRows.Append(Cell(row.Email));
            sheetRows.Append(Cell(row.Phone));
            sheetRows.Append(Cell(FormatNullableBoolean(row.IsPrimary)));
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
            "<sheets><sheet name=\"LegalRepresentatives\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
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

    private static string FormatNullableBoolean(bool? value) =>
        value switch
        {
            true => "true",
            false => "false",
            null => string.Empty
        };

    public sealed record CreateLegalRepresentativeRequest(
        string FirstName,
        string LastName,
        string DocumentType,
        string DocumentNumber,
        string PositionTitle,
        LegalRepresentativeRepresentationType RepresentationType,
        string? AuthorityDescription,
        string? AppointmentInstrument,
        DateTime? AppointmentDateUtc,
        DateTime EffectiveFromUtc,
        DateTime? EffectiveToUtc,
        string? Email,
        string? Phone,
        bool IsPrimary = false);

    public sealed record UpdateLegalRepresentativeRequest(
        string FirstName,
        string LastName,
        string DocumentType,
        string DocumentNumber,
        string PositionTitle,
        LegalRepresentativeRepresentationType RepresentationType,
        string? AuthorityDescription,
        string? AppointmentInstrument,
        DateTime? AppointmentDateUtc,
        DateTime EffectiveFromUtc,
        DateTime? EffectiveToUtc,
        string? Email,
        string? Phone,
        bool IsPrimary,
        Guid ConcurrencyToken);

    public sealed record ConcurrencyRequest(Guid ConcurrencyToken);
}
