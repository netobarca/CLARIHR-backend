using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Api.Common;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
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
public sealed class PersonnelFileEmploymentController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    IAuditService auditService,
    IUnitOfWork unitOfWork) : ControllerBase
{
    [HttpPost("api/v1/personnel-files/{id:guid}/hire")]
    [ProducesResponseType<PersonnelFileEmployeeProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileEmployeeProfileResponse>> Hire(
        Guid id,
        [FromBody] HirePersonnelFileRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new HirePersonnelFileCommand(
                id,
                request.EmployeeCode,
                request.EmploymentStatusCode,
                request.IsEmploymentActive,
                request.ContractTypeCode,
                request.HireDate,
                request.WorkdayCode,
                request.PayrollTypeCode,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/employee-profile")]
    [ProducesResponseType<PersonnelFileEmployeeProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileEmployeeProfileResponse>> UpdateEmployeeProfile(
        Guid id,
        [FromBody] UpdatePersonnelFileEmployeeProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileEmployeeProfileCommand(
                id,
                request.EmployeeCode,
                request.EmploymentStatusCode,
                request.IsEmploymentActive,
                request.ContractTypeCode,
                request.HireDate,
                request.RetirementCategoryCode,
                request.RetirementReasonCode,
                request.RetirementNotes,
                request.RetirementDate,
                request.WorkdayCode,
                request.PayrollTypeCode,
                request.PositionSlotPublicId,
                request.JobProfilePublicId,
                request.OrgUnitPublicId,
                request.WorkCenterPublicId,
                request.CostCenterPublicId,
                request.ContractStartDate,
                request.ContractEndDate,
                request.VacationConfigurationJson,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/employment-assignments")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>> ReplaceEmploymentAssignments(
        Guid id,
        [FromBody] ReplaceEmploymentAssignmentsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileEmploymentAssignmentsCommand(
                id,
                request.Items.Select(item => new EmploymentAssignmentInput(
                    item.AssignmentTypeCode,
                    item.PositionSlotPublicId,
                    item.OrgUnitPublicId,
                    item.WorkCenterPublicId,
                    item.CostCenterPublicId,
                    item.StartDate,
                    item.EndDate,
                    item.IsPrimary,
                    item.IsActive,
                    item.Notes)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/contract-history")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>> ReplaceContractHistory(
        Guid id,
        [FromBody] ReplaceContractHistoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileContractHistoryCommand(
                id,
                request.Items.Select(item => new ContractHistoryInput(
                    item.ContractTypeCode,
                    item.ContractDate,
                    item.ContractEndDate,
                    item.PositionSlotPublicId,
                    item.Notes)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{id:guid}/position-hierarchy")]
    [ProducesResponseType<PersonnelFilePositionHierarchyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFilePositionHierarchyResponse>> GetPositionHierarchy(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFilePositionHierarchyQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/authorization-substitutions")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>> ReplaceAuthorizationSubstitutions(
        Guid id,
        [FromBody] ReplaceAuthorizationSubstitutionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileAuthorizationSubstitutionsCommand(
                id,
                request.Items.Select(item => new AuthorizationSubstitutionInput(
                    item.SubstitutionTypeCode,
                    item.SubstitutePersonnelFilePublicId,
                    item.SubstitutePositionTitle,
                    item.StartDate,
                    item.EndDate,
                    item.IsActive,
                    item.Notes)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{id:guid}/personnel-actions")]
    [ProducesResponseType<PersonnelFilePersonnelActionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFilePersonnelActionResponse>> AddPersonnelAction(
        Guid id,
        [FromBody] AddPersonnelActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFilePersonnelActionCommand(
                id,
                request.ActionTypeCode,
                request.ActionStatusCode,
                request.ActionDateUtc,
                request.EffectiveFromUtc,
                request.EffectiveToUtc,
                request.Description,
                request.Reference,
                request.Amount,
                request.CurrencyCode,
                request.ConcurrencyToken),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<PersonnelFilePersonnelActionResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpGet("api/v1/personnel-files/{id:guid}/personnel-actions")]
    [ProducesResponseType<PagedResponse<PersonnelFilePersonnelActionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResponse<PersonnelFilePersonnelActionResponse>>> SearchPersonnelActions(
        Guid id,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string? type = null,
        [FromQuery] string? status = null,
        [FromQuery(Name = "q")] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] PersonnelFileSortDirection sortDirection = PersonnelFileSortDirection.Desc,
        [FromQuery(Name = "page")] int pageNumber = 1,
        [FromQuery] int pageSize = PersonnelFileValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchPersonnelFilePersonnelActionsQuery(
                id,
                fromUtc,
                toUtc,
                type,
                status,
                search,
                sortBy,
                sortDirection,
                pageNumber,
                pageSize),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{id:guid}/personnel-actions/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ExportPersonnelActions(
        Guid id,
        [FromQuery] string format = "xlsx",
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string? type = null,
        [FromQuery] string? status = null,
        [FromQuery(Name = "q")] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] PersonnelFileSortDirection sortDirection = PersonnelFileSortDirection.Desc,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportPersonnelFilePersonnelActionsQuery(
                id,
                fromUtc,
                toUtc,
                type,
                status,
                search,
                sortBy,
                sortDirection),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<PersonnelFilePersonnelActionExportRow>>.Failure(result.Error)).Result!;
        }

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.ReportExported,
                    AuditEntityTypes.PersonnelFile,
                    id,
                    PersonnelFilePermissionCodes.ResourceKey,
                    AuditActions.Export,
                    "Exported personnel actions report.",
                    After: new
                    {
                        resourceKey = PersonnelFilePermissionCodes.ResourceKey,
                        format = "csv",
                        filters = new { fromUtc, toUtc, type, status, q = search, sortBy, sortDirection },
                        rowCount = result.Value.Count
                    }),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var csv = BuildPersonnelActionsCsv(result.Value);
            return File(Encoding.UTF8.GetBytes(csv), "text/csv", "personnel-actions.csv");
        }

        if (string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.ReportExported,
                    AuditEntityTypes.PersonnelFile,
                    id,
                    PersonnelFilePermissionCodes.ResourceKey,
                    AuditActions.Export,
                    "Exported personnel actions report.",
                    After: new
                    {
                        resourceKey = PersonnelFilePermissionCodes.ResourceKey,
                        format = "xlsx",
                        filters = new { fromUtc, toUtc, type, status, q = search, sortBy, sortDirection },
                        rowCount = result.Value.Count
                    }),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var xlsx = BuildPersonnelActionsXlsx(result.Value);
            return File(
                xlsx,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "personnel-actions.xlsx");
        }

        return this.ToActionResult(Result<IReadOnlyCollection<PersonnelFilePersonnelActionExportRow>>.Failure(PersonnelFileErrors.ExportFormatInvalid)).Result!;
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/assets-accesses")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>> ReplaceAssetsAccesses(
        Guid id,
        [FromBody] ReplaceAssetsAccessesRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileAssetsAccessesCommand(
                id,
                request.Items.Select(item => new AssetAccessInput(
                    item.AssetTypeCode,
                    item.AssetOrAccessName,
                    item.AccessLevelCode,
                    item.StartDateUtc,
                    item.EndDateUtc,
                    item.DeliveryDateUtc,
                    item.DeliveryStatusCode,
                    item.IsActive,
                    item.Notes)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    private static string BuildPersonnelActionsCsv(IReadOnlyCollection<PersonnelFilePersonnelActionExportRow> rows)
    {
        var lines = new List<string>
        {
            "PublicId,ActionTypeCode,ActionStatusCode,ActionDateUtc,EffectiveFromUtc,EffectiveToUtc,Description,Reference,Amount,CurrencyCode,IsSystemGenerated,CreatedAtUtc,ModifiedAtUtc"
        };

        lines.AddRange(rows.Select(row => string.Join(",",
            EscapeCsv(row.Id.ToString()),
            EscapeCsv(row.ActionTypeCode),
            EscapeCsv(row.ActionStatusCode),
            EscapeCsv(row.ActionDateUtc.ToString("O", CultureInfo.InvariantCulture)),
            EscapeCsv(row.EffectiveFromUtc?.ToString("O", CultureInfo.InvariantCulture)),
            EscapeCsv(row.EffectiveToUtc?.ToString("O", CultureInfo.InvariantCulture)),
            EscapeCsv(row.Description),
            EscapeCsv(row.Reference),
            EscapeCsv(row.Amount?.ToString(CultureInfo.InvariantCulture)),
            EscapeCsv(row.CurrencyCode),
            row.IsSystemGenerated ? "true" : "false",
            EscapeCsv(row.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
            EscapeCsv(row.ModifiedAtUtc?.ToString("O", CultureInfo.InvariantCulture)))));

        return string.Join("\n", lines);
    }

    private static byte[] BuildPersonnelActionsXlsx(IReadOnlyCollection<PersonnelFilePersonnelActionExportRow> rows) =>
        BuildSimpleXlsx(
            "PersonnelActions",
            [
                "PublicId",
                "ActionTypeCode",
                "ActionStatusCode",
                "ActionDateUtc",
                "EffectiveFromUtc",
                "EffectiveToUtc",
                "Description",
                "Reference",
                "Amount",
                "CurrencyCode",
                "IsSystemGenerated",
                "CreatedAtUtc",
                "ModifiedAtUtc"
            ],
            rows.Select(row => new[]
            {
                row.Id.ToString(),
                row.ActionTypeCode,
                row.ActionStatusCode,
                row.ActionDateUtc.ToString("O", CultureInfo.InvariantCulture),
                row.EffectiveFromUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                row.EffectiveToUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                row.Description ?? string.Empty,
                row.Reference ?? string.Empty,
                row.Amount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                row.CurrencyCode ?? string.Empty,
                row.IsSystemGenerated ? "true" : "false",
                row.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                row.ModifiedAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty
            }).ToArray());

    private static byte[] BuildSimpleXlsx(string sheetName, IReadOnlyList<string> headers, IReadOnlyCollection<string[]> rows)
    {
        static string Cell(string? value) =>
            $"<c t=\"inlineStr\"><is><t>{EscapeXml(value)}</t></is></c>";

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
            foreach (var value in row)
            {
                sheetRows.Append(Cell(value));
            }

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
            $"<sheets><sheet name=\"{EscapeXml(sheetName)}\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
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
