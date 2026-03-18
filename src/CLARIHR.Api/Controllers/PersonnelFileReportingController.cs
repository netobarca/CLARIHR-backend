using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using CLARIHR.Api.Common;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class PersonnelFileReportingController(
    IQueryDispatcher queryDispatcher,
    IAuditService auditService,
    IUnitOfWork unitOfWork) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{id:guid}/print")]
    [ProducesResponseType<PersonnelFilePrintResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PersonnelFilePrintResponse>> Print(
        Guid id,
        [FromQuery] string? sections,
        CancellationToken cancellationToken = default)
    {
        var parsedSections = string.IsNullOrWhiteSpace(sections)
            ? null
            : sections.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFilePrintQuery(id, parsedSections),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        await auditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.ReportPrinted,
                AuditEntityTypes.PersonnelFile,
                id,
                PersonnelFilePermissionCodes.ResourceKey,
                AuditActions.Print,
                "Printed personnel file report.",
                After: new
                {
                    resourceKey = PersonnelFilePermissionCodes.ResourceKey,
                    format = "print",
                    filters = new { id, sections = result.Value.IncludedSections },
                    rowCount = 1
                }),
            cancellationToken);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/companies/{companyId:guid}/personnel-files/dynamic-query")]
    [ProducesResponseType<PersonnelFileDynamicQueryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileDynamicQueryResponse>> DynamicQuery(
        Guid companyId,
        [FromBody] DynamicQueryPersonnelFilesRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new DynamicQueryPersonnelFilesQuery(
                companyId,
                (request.Filters ?? Array.Empty<DynamicPersonnelFileFilterRequest>()).Select(item => new PersonnelFileDynamicFilterInput(
                    item.Field,
                    item.Operator,
                    item.Value,
                    item.ValueTo,
                    item.Values)).ToArray(),
                request.GroupBy ?? Array.Empty<string>(),
                (request.Sort ?? Array.Empty<DynamicPersonnelFileSortRequest>()).Select(item => new PersonnelFileDynamicSortInput(item.Field, item.Direction)).ToArray(),
                request.Q,
                request.Page,
                request.PageSize,
                request.IncludeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/personnel-files/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Export(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] bool? isActive = null,
        [FromQuery] PersonnelFileRecordType? recordType = null,
        [FromQuery] Guid? orgUnitId = null,
        [FromQuery] int? minAge = null,
        [FromQuery] int? maxAge = null,
        [FromQuery] string? maritalStatus = null,
        [FromQuery] string? nationality = null,
        [FromQuery] string? profession = null,
        [FromQuery] DateTime? createdFromUtc = null,
        [FromQuery] DateTime? createdToUtc = null,
        [FromQuery(Name = "q")] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] PersonnelFileSortDirection sortDirection = PersonnelFileSortDirection.Asc,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportPersonnelFilesQuery(
                companyId,
                isActive,
                recordType,
                orgUnitId,
                minAge,
                maxAge,
                maritalStatus,
                nationality,
                profession,
                createdFromUtc,
                createdToUtc,
                search,
                sortBy,
                sortDirection),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<PersonnelFileExportRow>>.Failure(result.Error)).Result!;
        }

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.ReportExported,
                    AuditEntityTypes.PersonnelFile,
                    null,
                    PersonnelFilePermissionCodes.ResourceKey,
                    AuditActions.Export,
                    "Exported personnel files report.",
                    After: new
                    {
                        resourceKey = PersonnelFilePermissionCodes.ResourceKey,
                        format = "csv",
                        filters = new
                        {
                            isActive,
                            recordType,
                            orgUnitId,
                            minAge,
                            maxAge,
                            maritalStatus,
                            nationality,
                            profession,
                            createdFromUtc,
                            createdToUtc,
                            q = search,
                            sortBy,
                            sortDirection
                        },
                        rowCount = result.Value.Count
                    }),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var csv = BuildCsv(result.Value);
            return File(Encoding.UTF8.GetBytes(csv), "text/csv", "personnel-files.csv");
        }

        if (string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.ReportExported,
                    AuditEntityTypes.PersonnelFile,
                    null,
                    PersonnelFilePermissionCodes.ResourceKey,
                    AuditActions.Export,
                    "Exported personnel files report.",
                    After: new
                    {
                        resourceKey = PersonnelFilePermissionCodes.ResourceKey,
                        format = "xlsx",
                        filters = new
                        {
                            isActive,
                            recordType,
                            orgUnitId,
                            minAge,
                            maxAge,
                            maritalStatus,
                            nationality,
                            profession,
                            createdFromUtc,
                            createdToUtc,
                            q = search,
                            sortBy,
                            sortDirection
                        },
                        rowCount = result.Value.Count
                    }),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var xlsx = BuildXlsx(result.Value);
            return File(
                xlsx,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "personnel-files.xlsx");
        }

        return this.ToActionResult(Result<IReadOnlyCollection<PersonnelFileExportRow>>.Failure(PersonnelFileErrors.ExportFormatInvalid)).Result!;
    }

    [HttpGet("api/v1/companies/{companyId:guid}/personnel-files/analytics/summary")]
    [ProducesResponseType<PersonnelFileAnalyticsSummaryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PersonnelFileAnalyticsSummaryResponse>> AnalyticsSummary(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery] PersonnelFileRecordType? recordType,
        [FromQuery] Guid? orgUnitId,
        [FromQuery] int? minAge,
        [FromQuery] int? maxAge,
        [FromQuery(Name = "q")] string? search,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFilesAnalyticsSummaryQuery(companyId, isActive, recordType, orgUnitId, minAge, maxAge, search),
            cancellationToken);

        return this.ToActionResult(result);
    }

    private static string BuildCsv(IReadOnlyCollection<PersonnelFileExportRow> rows)
    {
        var lines = new List<string>
        {
            "Id,RecordType,FirstName,LastName,FullName,BirthDate,Age,MaritalStatus,Profession,Nationality,PersonalEmail,InstitutionalEmail,PersonalPhone,InstitutionalPhone,OrgUnitId,IsActive,CreatedAtUtc,ModifiedAtUtc"
        };

        lines.AddRange(rows.Select(row => string.Join(",",
            EscapeCsv(row.Id.ToString()),
            EscapeCsv(row.RecordType.ToString()),
            EscapeCsv(row.FirstName),
            EscapeCsv(row.LastName),
            EscapeCsv(row.FullName),
            EscapeCsv(row.BirthDate.ToString("O", CultureInfo.InvariantCulture)),
            row.Age.ToString(CultureInfo.InvariantCulture),
            EscapeCsv(row.MaritalStatus),
            EscapeCsv(row.Profession),
            EscapeCsv(row.Nationality),
            EscapeCsv(row.PersonalEmail),
            EscapeCsv(row.InstitutionalEmail),
            EscapeCsv(row.PersonalPhone),
            EscapeCsv(row.InstitutionalPhone),
            EscapeCsv(row.OrgUnitId?.ToString()),
            row.IsActive ? "true" : "false",
            EscapeCsv(row.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
            EscapeCsv(row.ModifiedAtUtc?.ToString("O", CultureInfo.InvariantCulture)))));

        return string.Join("\n", lines);
    }

    private static byte[] BuildXlsx(IReadOnlyCollection<PersonnelFileExportRow> rows)
    {
        static string Cell(string? value) =>
            $"<c t=\"inlineStr\"><is><t>{EscapeXml(value)}</t></is></c>";

        var headers = new[]
        {
            "Id",
            "RecordType",
            "FirstName",
            "LastName",
            "FullName",
            "BirthDate",
            "Age",
            "MaritalStatus",
            "Profession",
            "Nationality",
            "PersonalEmail",
            "InstitutionalEmail",
            "PersonalPhone",
            "InstitutionalPhone",
            "OrgUnitId",
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
            sheetRows.Append(Cell(row.RecordType.ToString()));
            sheetRows.Append(Cell(row.FirstName));
            sheetRows.Append(Cell(row.LastName));
            sheetRows.Append(Cell(row.FullName));
            sheetRows.Append(Cell(row.BirthDate.ToString("O", CultureInfo.InvariantCulture)));
            sheetRows.Append(Cell(row.Age.ToString(CultureInfo.InvariantCulture)));
            sheetRows.Append(Cell(row.MaritalStatus));
            sheetRows.Append(Cell(row.Profession));
            sheetRows.Append(Cell(row.Nationality));
            sheetRows.Append(Cell(row.PersonalEmail));
            sheetRows.Append(Cell(row.InstitutionalEmail));
            sheetRows.Append(Cell(row.PersonalPhone));
            sheetRows.Append(Cell(row.InstitutionalPhone));
            sheetRows.Append(Cell(row.OrgUnitId?.ToString()));
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
            "<sheets><sheet name=\"PersonnelFiles\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
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
}
