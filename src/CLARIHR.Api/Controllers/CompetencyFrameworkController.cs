using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using CLARIHR.Api.Common;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.CompetencyFramework;
using CLARIHR.Application.Features.CompetencyFramework.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class CompetencyFrameworkController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    IAuditService auditService,
    IUnitOfWork unitOfWork) : ControllerBase
{
    [HttpGet("api/v1/companies/{companyId:guid}/occupational-pyramid-levels")]
    [ProducesResponseType<PagedResponse<OccupationalPyramidLevelListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<OccupationalPyramidLevelListItemResponse>>> SearchOccupationalPyramidLevels(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = CompetencyFrameworkValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchOccupationalPyramidLevelsQuery(companyId, isActive, search, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/occupational-pyramid-levels/{id:guid}")]
    [ProducesResponseType<OccupationalPyramidLevelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OccupationalPyramidLevelResponse>> GetOccupationalPyramidLevelById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetOccupationalPyramidLevelByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/companies/{companyId:guid}/occupational-pyramid-levels")]
    [ProducesResponseType<OccupationalPyramidLevelResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OccupationalPyramidLevelResponse>> CreateOccupationalPyramidLevel(
        Guid companyId,
        [FromBody] CreateOccupationalPyramidLevelRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateOccupationalPyramidLevelCommand(companyId, request.Code, request.Name, request.LevelOrder, request.Description),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<OccupationalPyramidLevelResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/occupational-pyramid-levels/{id:guid}")]
    [ProducesResponseType<OccupationalPyramidLevelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OccupationalPyramidLevelResponse>> UpdateOccupationalPyramidLevel(
        Guid id,
        [FromBody] UpdateOccupationalPyramidLevelRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateOccupationalPyramidLevelCommand(id, request.Code, request.Name, request.LevelOrder, request.Description, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/occupational-pyramid-levels/{id:guid}/activate")]
    [ProducesResponseType<OccupationalPyramidLevelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OccupationalPyramidLevelResponse>> ActivateOccupationalPyramidLevel(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateOccupationalPyramidLevelCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/occupational-pyramid-levels/{id:guid}/inactivate")]
    [ProducesResponseType<OccupationalPyramidLevelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OccupationalPyramidLevelResponse>> InactivateOccupationalPyramidLevel(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateOccupationalPyramidLevelCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/competency-conducts")]
    [ProducesResponseType<PagedResponse<CompetencyConductListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<CompetencyConductListItemResponse>>> SearchCompetencyConducts(
        Guid companyId,
        [FromQuery] Guid? competencyId,
        [FromQuery] Guid? competencyTypeId,
        [FromQuery] Guid? behaviorLevelId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = CompetencyFrameworkValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchCompetencyConductsQuery(
                companyId,
                competencyId,
                competencyTypeId,
                behaviorLevelId,
                isActive,
                search,
                page,
                pageSize,
                includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/competency-conducts/{id:guid}")]
    [ProducesResponseType<CompetencyConductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompetencyConductResponse>> GetCompetencyConductById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetCompetencyConductByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/companies/{companyId:guid}/competency-conducts")]
    [ProducesResponseType<CompetencyConductResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompetencyConductResponse>> CreateCompetencyConduct(
        Guid companyId,
        [FromBody] CreateCompetencyConductRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateCompetencyConductCommand(
                companyId,
                request.CompetencyId,
                request.CompetencyTypeId,
                request.BehaviorLevelId,
                request.Description,
                request.SortOrder),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<CompetencyConductResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/competency-conducts/{id:guid}")]
    [ProducesResponseType<CompetencyConductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompetencyConductResponse>> UpdateCompetencyConduct(
        Guid id,
        [FromBody] UpdateCompetencyConductRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateCompetencyConductCommand(
                id,
                request.CompetencyId,
                request.CompetencyTypeId,
                request.BehaviorLevelId,
                request.Description,
                request.SortOrder,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/competency-conducts/{id:guid}/activate")]
    [ProducesResponseType<CompetencyConductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompetencyConductResponse>> ActivateCompetencyConduct(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateCompetencyConductCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/competency-conducts/{id:guid}/inactivate")]
    [ProducesResponseType<CompetencyConductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompetencyConductResponse>> InactivateCompetencyConduct(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateCompetencyConductCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/competency-conducts/{id:guid}/behaviors")]
    [ProducesResponseType<CompetencyConductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompetencyConductResponse>> UpdateCompetencyConductBehaviors(
        Guid id,
        [FromBody] UpdateCompetencyConductBehaviorsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateCompetencyConductBehaviorsCommand(
                id,
                request.Behaviors?.Select(item => new CompetencyConductBehaviorInput(item.BehaviorId, item.Notes, item.SortOrder)).ToArray() ?? [],
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/job-profiles/{id:guid}/competency-matrix")]
    [ProducesResponseType<JobProfileCompetencyMatrixResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobProfileCompetencyMatrixResponse>> GetJobProfileCompetencyMatrix(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetJobProfileCompetencyMatrixQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/job-profiles/{id:guid}/competency-matrix")]
    [ProducesResponseType<JobProfileCompetencyMatrixResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<JobProfileCompetencyMatrixResponse>> UpdateJobProfileCompetencyMatrix(
        Guid id,
        [FromBody] UpdateJobProfileCompetencyMatrixRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateJobProfileCompetencyMatrixCommand(
                id,
                request.Items?.Select(item => new JobProfileCompetencyMatrixItemInput(
                        item.OccupationalPyramidLevelId,
                        item.CompetencyId,
                        item.CompetencyTypeId,
                        item.BehaviorLevelId,
                        item.ConductIds ?? [],
                        item.ExpectedEvidence,
                        item.SortOrder))
                    .ToArray() ?? [],
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/job-profiles/{id:guid}/competency-matrix/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportJobProfileCompetencyMatrix(
        Guid id,
        [FromQuery] string format = "xlsx",
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new ExportJobProfileCompetencyMatrixQuery(id), cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<JobProfileCompetencyMatrixExportRow>>.Failure(result.Error)).Result!;
        }

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            await WriteReportAuditAsync(id, "json", result.Value.Count, cancellationToken);
            return Ok(result.Value);
        }

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            await WriteReportAuditAsync(id, "csv", result.Value.Count, cancellationToken);
            var csv = BuildCsv(result.Value);
            return File(Encoding.UTF8.GetBytes(csv), "text/csv", "job-profile-competency-matrix.csv");
        }

        if (string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            await WriteReportAuditAsync(id, "xlsx", result.Value.Count, cancellationToken);
            var xlsx = BuildXlsx(result.Value);
            return File(
                xlsx,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "job-profile-competency-matrix.xlsx");
        }

        return this.ToActionResult(Result<IReadOnlyCollection<JobProfileCompetencyMatrixExportRow>>.Failure(CompetencyFrameworkErrors.ExportFormatInvalid)).Result!;
    }

    private async Task WriteReportAuditAsync(Guid jobProfileId, string format, int rowCount, CancellationToken cancellationToken)
    {
        await auditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.ReportExported,
                AuditEntityTypes.JobProfileCompetencyMatrix,
                jobProfileId,
                CompetencyFrameworkPermissionCodes.ResourceKey,
                AuditActions.Export,
                "Exported job profile competency matrix report.",
                After: new
                {
                    resourceKey = CompetencyFrameworkPermissionCodes.ResourceKey,
                    format,
                    rowCount
                }),
            cancellationToken);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string BuildCsv(IReadOnlyCollection<JobProfileCompetencyMatrixExportRow> rows)
    {
        var lines = new List<string>
        {
            "JobProfileId,JobProfileCode,JobProfileTitle,JobProfileStatus,JobProfileVersion,OccupationalPyramidLevelId,OccupationalPyramidLevelCode,OccupationalPyramidLevelName,OccupationalPyramidLevelOrder,CompetencyId,CompetencyCode,CompetencyName,CompetencyTypeId,CompetencyTypeCode,CompetencyTypeName,BehaviorLevelId,BehaviorLevelCode,BehaviorLevelName,ConductId,ConductDescription,ConductSortOrder,ExpectedEvidence,ItemSortOrder"
        };

        lines.AddRange(rows.Select(row => string.Join(",",
            EscapeCsv(row.JobProfileId.ToString()),
            EscapeCsv(row.JobProfileCode),
            EscapeCsv(row.JobProfileTitle),
            EscapeCsv(row.JobProfileStatus),
            row.JobProfileVersion,
            EscapeCsv(row.OccupationalPyramidLevelId.ToString()),
            EscapeCsv(row.OccupationalPyramidLevelCode),
            EscapeCsv(row.OccupationalPyramidLevelName),
            row.OccupationalPyramidLevelOrder,
            EscapeCsv(row.CompetencyId.ToString()),
            EscapeCsv(row.CompetencyCode),
            EscapeCsv(row.CompetencyName),
            EscapeCsv(row.CompetencyTypeId.ToString()),
            EscapeCsv(row.CompetencyTypeCode),
            EscapeCsv(row.CompetencyTypeName),
            EscapeCsv(row.BehaviorLevelId.ToString()),
            EscapeCsv(row.BehaviorLevelCode),
            EscapeCsv(row.BehaviorLevelName),
            EscapeCsv(row.ConductId?.ToString()),
            EscapeCsv(row.ConductDescription),
            EscapeCsv(row.ConductSortOrder?.ToString(CultureInfo.InvariantCulture)),
            EscapeCsv(row.ExpectedEvidence),
            row.ItemSortOrder)));

        return string.Join("\n", lines);
    }

    private static byte[] BuildXlsx(IReadOnlyCollection<JobProfileCompetencyMatrixExportRow> rows)
    {
        static string Cell(string? value) =>
            $"<c t=\"inlineStr\"><is><t>{EscapeXml(value)}</t></is></c>";

        var headers = new[]
        {
            "JobProfileId",
            "JobProfileCode",
            "JobProfileTitle",
            "JobProfileStatus",
            "JobProfileVersion",
            "OccupationalPyramidLevelId",
            "OccupationalPyramidLevelCode",
            "OccupationalPyramidLevelName",
            "OccupationalPyramidLevelOrder",
            "CompetencyId",
            "CompetencyCode",
            "CompetencyName",
            "CompetencyTypeId",
            "CompetencyTypeCode",
            "CompetencyTypeName",
            "BehaviorLevelId",
            "BehaviorLevelCode",
            "BehaviorLevelName",
            "ConductId",
            "ConductDescription",
            "ConductSortOrder",
            "ExpectedEvidence",
            "ItemSortOrder"
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
            sheetRows.Append(Cell(row.JobProfileId.ToString()));
            sheetRows.Append(Cell(row.JobProfileCode));
            sheetRows.Append(Cell(row.JobProfileTitle));
            sheetRows.Append(Cell(row.JobProfileStatus));
            sheetRows.Append(Cell(row.JobProfileVersion.ToString(CultureInfo.InvariantCulture)));
            sheetRows.Append(Cell(row.OccupationalPyramidLevelId.ToString()));
            sheetRows.Append(Cell(row.OccupationalPyramidLevelCode));
            sheetRows.Append(Cell(row.OccupationalPyramidLevelName));
            sheetRows.Append(Cell(row.OccupationalPyramidLevelOrder.ToString(CultureInfo.InvariantCulture)));
            sheetRows.Append(Cell(row.CompetencyId.ToString()));
            sheetRows.Append(Cell(row.CompetencyCode));
            sheetRows.Append(Cell(row.CompetencyName));
            sheetRows.Append(Cell(row.CompetencyTypeId.ToString()));
            sheetRows.Append(Cell(row.CompetencyTypeCode));
            sheetRows.Append(Cell(row.CompetencyTypeName));
            sheetRows.Append(Cell(row.BehaviorLevelId.ToString()));
            sheetRows.Append(Cell(row.BehaviorLevelCode));
            sheetRows.Append(Cell(row.BehaviorLevelName));
            sheetRows.Append(Cell(row.ConductId?.ToString()));
            sheetRows.Append(Cell(row.ConductDescription));
            sheetRows.Append(Cell(row.ConductSortOrder?.ToString(CultureInfo.InvariantCulture)));
            sheetRows.Append(Cell(row.ExpectedEvidence));
            sheetRows.Append(Cell(row.ItemSortOrder.ToString(CultureInfo.InvariantCulture)));
            sheetRows.Append("</row>");
        }

        var sheetXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<sheetData>" +
            sheetRows +
            "</sheetData>" +
            "</worksheet>";

        const string contentTypesXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "</Types>";

        const string rootRelsXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>";

        const string workbookXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            "<sheets><sheet name=\"CompetencyMatrix\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
            "</workbook>";

        const string workbookRelsXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "</Relationships>";

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteZipEntry(archive, "[Content_Types].xml", contentTypesXml);
            WriteZipEntry(archive, "_rels/.rels", rootRelsXml);
            WriteZipEntry(archive, "xl/workbook.xml", workbookXml);
            WriteZipEntry(archive, "xl/_rels/workbook.xml.rels", workbookRelsXml);
            WriteZipEntry(archive, "xl/worksheets/sheet1.xml", sheetXml);
        }

        return stream.ToArray();
    }

    private static void WriteZipEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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

    private static string EscapeXml(string? value) =>
        SecurityElement.Escape(value ?? string.Empty) ?? string.Empty;

    public sealed record CreateOccupationalPyramidLevelRequest(
        string Code,
        string Name,
        int LevelOrder,
        string? Description);

    public sealed record UpdateOccupationalPyramidLevelRequest(
        string Code,
        string Name,
        int LevelOrder,
        string? Description,
        Guid ConcurrencyToken);

    public sealed record CreateCompetencyConductRequest(
        Guid CompetencyId,
        Guid CompetencyTypeId,
        Guid BehaviorLevelId,
        string Description,
        int SortOrder);

    public sealed record UpdateCompetencyConductRequest(
        Guid CompetencyId,
        Guid CompetencyTypeId,
        Guid BehaviorLevelId,
        string Description,
        int SortOrder,
        Guid ConcurrencyToken);

    public sealed record UpdateCompetencyConductBehaviorsRequest(
        IReadOnlyCollection<CompetencyConductBehaviorRequest>? Behaviors,
        Guid ConcurrencyToken);

    public sealed record CompetencyConductBehaviorRequest(
        Guid BehaviorId,
        string? Notes,
        int SortOrder);

    public sealed record UpdateJobProfileCompetencyMatrixRequest(
        IReadOnlyCollection<JobProfileCompetencyMatrixItemRequest>? Items,
        Guid ConcurrencyToken);

    public sealed record JobProfileCompetencyMatrixItemRequest(
        Guid OccupationalPyramidLevelId,
        Guid CompetencyId,
        Guid CompetencyTypeId,
        Guid BehaviorLevelId,
        IReadOnlyCollection<Guid>? ConductIds,
        string? ExpectedEvidence,
        int SortOrder);

    public sealed record ConcurrencyRequest(Guid ConcurrencyToken);
}
