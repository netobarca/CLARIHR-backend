using CLARIHR.Api.Common;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.Reports.Common;
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
    IUnitOfWork unitOfWork,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
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
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
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
                sortDirection,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<PersonnelFileExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "personnel-files",
            "PersonnelFiles",
            AuditEntityTypes.PersonnelFile,
            ReportExportResources.PersonnelFiles,
            "Exported personnel files report.",
            new
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
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
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

}
