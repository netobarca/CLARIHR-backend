using CLARIHR.Api.Common;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PersonnelFiles.Reporting;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.PersonnelFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
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

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
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

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
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

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpGet("api/v1/companies/{companyId:guid}/personnel-files/dashboard/overview")]
    [Produces("application/json")]
    [ProducesResponseType<DashboardOverviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "HR analytics dashboard — composition, demographics, structure and quality overview",
        Description = """
            Read-only aggregate indicators over the company's employees: headcount (total/active/inactive),
            breakdowns by category (record type, employment status, contract type, position category,
            functional area, org unit, work center), age and seniority distributions (parametrizable ranges),
            marital status, expediente freshness (updated vs outdated — D-08), HR-staff-per-100 ratio (D-06)
            and position occupancy (plazas ocupadas/vacantes — D-13). Honors the common dimension filters
            (year, functionalAreaId, orgUnitId, positionCategoryId, jobProfileId, workCenterId) and the
            includeInactive toggle (D-03). Requires the ViewReports or Read permission (gated in the handler).
            """)]
    public async Task<ActionResult<DashboardOverviewResponse>> DashboardOverview(
        Guid companyId,
        [FromQuery] int? year = null,
        [FromQuery] Guid? functionalAreaId = null,
        [FromQuery] Guid? orgUnitId = null,
        [FromQuery] Guid? positionCategoryId = null,
        [FromQuery] Guid? jobProfileId = null,
        [FromQuery] Guid? workCenterId = null,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetDashboardOverviewQuery(
                companyId,
                year,
                functionalAreaId,
                orgUnitId,
                positionCategoryId,
                jobProfileId,
                workCenterId,
                includeInactive),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpGet("api/v1/companies/{companyId:guid}/personnel-files/dashboard/hires")]
    [Produces("application/json")]
    [ProducesResponseType<DashboardHiresResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "HR analytics dashboard — hires (altas) time series for a year",
        Description = """
            Monthly count of hires for the requested year (defaults to the current year), derived from each
            employee's HireDate (D-02). Honors the dimension filters. Bajas and turnover are DEFERRED to the
            future "Baja de Personal" module. Note: a rehire overwrites HireDate, so a rehired employee counts
            as a hire of the rehire year (R-03). Requires the ViewReports or Read permission.
            """)]
    public async Task<ActionResult<DashboardHiresResponse>> DashboardHires(
        Guid companyId,
        [FromQuery] int? year = null,
        [FromQuery] Guid? functionalAreaId = null,
        [FromQuery] Guid? orgUnitId = null,
        [FromQuery] Guid? positionCategoryId = null,
        [FromQuery] Guid? jobProfileId = null,
        [FromQuery] Guid? workCenterId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetDashboardHiresQuery(
                companyId,
                year ?? DateTime.UtcNow.Year,
                functionalAreaId,
                orgUnitId,
                positionCategoryId,
                jobProfileId,
                workCenterId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpGet("api/v1/companies/{companyId:guid}/personnel-files/dashboard/span-of-control")]
    [Produces("application/json")]
    [ProducesResponseType<DashboardSpanOfControlResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "HR analytics dashboard — collaborators per manager (span of control)",
        Description = """
            Direct-report count per manager (D-05): the manager is the occupant of the slot referenced by the
            report's DirectDependencyPositionSlotId. Includes the count of employees without a resolvable
            manager. Honors the dimension filters and the includeInactive toggle. Requires ViewReports or Read.
            """)]
    public async Task<ActionResult<DashboardSpanOfControlResponse>> DashboardSpanOfControl(
        Guid companyId,
        [FromQuery] Guid? functionalAreaId = null,
        [FromQuery] Guid? orgUnitId = null,
        [FromQuery] Guid? positionCategoryId = null,
        [FromQuery] Guid? jobProfileId = null,
        [FromQuery] Guid? workCenterId = null,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetDashboardSpanOfControlQuery(
                companyId,
                functionalAreaId,
                orgUnitId,
                positionCategoryId,
                jobProfileId,
                workCenterId,
                includeInactive),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpGet("api/v1/companies/{companyId:guid}/personnel-files/dashboard/metadata")]
    [Produces("application/json")]
    [ProducesResponseType<DashboardMetadataResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "HR analytics dashboard — metadata (parametrizable ranges + settings)",
        Description = """
            The configured age and seniority range buckets (with numeric bounds, used to label the
            distributions) plus the resolved company parametrization (the "expediente actualizado" threshold in
            months and the HR functional-area marker, if set). Lets the frontend render legends and read the
            current configuration. Requires the ViewReports or Read permission.
            """)]
    public async Task<ActionResult<DashboardMetadataResponse>> DashboardMetadata(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetDashboardMetadataQuery(companyId),
            cancellationToken);

        return this.ToActionResult(result);
    }
}
