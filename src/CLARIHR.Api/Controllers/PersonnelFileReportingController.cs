using CLARIHR.Api.Common;
using CLARIHR.Api.Contracts.PersonnelFiles;
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
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
[Tags("Personnel Files")]
// Intentionally NOT annotated with [AuthorizationPolicySet]: AuthorizationPolicyConvention would
// assign the Manage policy to the POST `dynamic-query` action, but that action is a READ (its
// handler gates on EnsureCanReadAsync). Declaring Manage would exceed the handler gate and produce
// false 403s for read-only users (the two-layer authorization superset invariant). Authorization is
// therefore enforced per handler (EnsureCanReadAsync) on top of the class-level [Authorize].
public sealed class PersonnelFileReportingController(
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [HttpPost("api/v1/companies/{companyId:guid}/personnel-files/dynamic-query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileDynamicQueryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [SwaggerOperation(
        Summary = "Run a dynamic query over a company's personnel files",
        Description = """
            Read-only search and aggregation over a company's personnel files: arbitrary field
            filters, grouping, sorting, free-text (`q`) and pagination. Requires read permission only
            (it never mutates). For file downloads use the `export` endpoint (small result sets) or an
            asynchronous report export job (large result sets).
            """)]
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
    [SwaggerOperation(
        Summary = "Export a company's personnel files (synchronous)",
        Description = """
            Streams the filtered personnel files as a file (default `xlsx`, also `csv`) for immediate
            download. This synchronous path is capped at the configured synchronous row limit; when the
            filtered result would exceed it the endpoint responds `413 Payload Too Large`. For larger
            exports submit an asynchronous report export job instead
            (`POST /api/v1/companies/{companyId}/report-export-jobs` with `resourceKey=PERSONNEL_FILES`),
            then poll `GET /api/v1/report-export-jobs/{jobId}` and download its artifact. Both paths
            share the same underlying rows and filters; they differ only in row cap and delivery.
            """)]
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
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileAnalyticsSummaryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Get a company's personnel files analytics summary",
        Description = """
            Returns read-only aggregate counts for the company's personnel files (totals plus
            breakdowns by record type, age range and org unit), honoring the same filters as the
            list and export endpoints.
            """)]
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
