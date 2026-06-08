using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Reports;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

// REX-B/C: technical/handler-gated controller (authz is delegated per-resource via
// ReportExportResourceAuthorizer, so [AuthorizationPolicySet] does NOT apply by design). It carries
// [Tags("Reports")] + per-action [SwaggerOperation] + [ProducesStandardErrors] and is enrolled in the
// OpenAPI guardrail. The literal `api/v1/...` routes are kept unversioned (no [ApiVersion]), matching
// the SalaryTabulator precedent for these per-resource-gated controllers.
[ApiController]
[Authorize]
[Tags("Reports")]
public sealed class ReportExportJobsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [HttpPost("api/v1/companies/{companyId:guid}/report-export-jobs")]
    [ProducesResponseType<ReportExportJobResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Enqueue a report export job",
        Description = """
            Queues an asynchronous report export for the company and returns `202 Accepted` with the
            `Location` header pointing to the new job. The job references a whitelisted `resourceKey`
            and a `format`; a background worker processes it and produces a downloadable artifact.
            Requires the per-resource read permission for the requested `resourceKey`; an oversized
            request yields `413` and missing storage configuration yields `503`.
            """)]
    public async Task<IActionResult> Create(
        Guid companyId,
        [FromBody] CreateReportExportJobRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateReportExportJobCommand(
                companyId,
                request.ResourceKey,
                request.Format,
                request.Parameters.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                    ? "{}"
                    : request.Parameters.GetRawText()),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<ReportExportJobResponse>.Failure(result.Error)).Result!
            : Accepted($"/api/v1/report-export-jobs/{result.Value.Id:D}", result.Value);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/report-export-jobs")]
    [EnableRateLimiting(ReportExportJobRateLimitPolicies.Search)]
    [ProducesResponseType<PagedResponse<ReportExportJobResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List report export jobs for a company",
        Description = """
            Returns a paginated list of report export jobs for the company, optionally filtered by
            `status`. The owning company is validated against the authenticated tenant. The list is
            scoped to the jobs whose `resourceKey` the caller may read (same per-resource gate as
            get-by-id/download/cancel), so export metadata for resources the user cannot read is not
            disclosed. Rate-limited per user+tenant.
            """)]
    public async Task<ActionResult<PagedResponse<ReportExportJobResponse>>> Search(
        Guid companyId,
        [FromQuery] ReportExportJobStatus? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchReportExportJobsQuery(companyId, status, pageNumber, pageSize),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/report-export-jobs/{jobId:guid}")]
    [ProducesResponseType<ReportExportJobResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a report export job by id",
        Description = """
            Returns a single report export job by its public id. Requires the per-resource read
            permission for the job's `resourceKey`; a job belonging to another tenant — or to a
            resource the caller cannot read — yields `404`/`403`. The current `concurrencyToken` is
            returned in the body for use in the `If-Match` header of cancel.
            """)]
    public async Task<ActionResult<ReportExportJobResponse>> GetById(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetReportExportJobQuery(jobId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/report-export-jobs/{jobId:guid}/download")]
    [EnableRateLimiting(ReportExportJobRateLimitPolicies.Download)]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status410Gone)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Download a report export artifact",
        Description = """
            Streams the generated artifact of a succeeded export job. Requires the per-resource read
            permission for the job's `resourceKey`. A job that is not yet ready yields `409`; an
            expired artifact yields `410`. Rate-limited per user+tenant.
            """)]
    public async Task<IActionResult> Download(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetReportExportJobDownloadQuery(jobId), cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(Result<ReportExportJobDownloadResponse>.Failure(result.Error)).Result!;
        }

        // REX-G: storage rule/provider/container resolution lives in ReportExportDeliveryService now.
        var streamResult = await reportExportDeliveryService.OpenArtifactStreamAsync(result.Value.BlobName, cancellationToken);
        if (streamResult.IsFailure)
        {
            return CLARIHR.Api.Common.ProblemDetailsFactory.Create(HttpContext, streamResult.Error);
        }

        return File(streamResult.Value, result.Value.ContentType, result.Value.FileName);
    }

    [HttpPatch("api/v1/report-export-jobs/{jobId:guid}/cancel")]
    [ProducesResponseType<ReportExportJobResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Cancel a report export job",
        Description = """
            Cancels a queued or running report export job. Requires the per-resource read permission
            for the job's `resourceKey` and the current `concurrencyToken` in the `If-Match` header
            (missing → `400`, stale → `409 REPORT_CONCURRENCY_CONFLICT`). A job already in a terminal
            state yields `409 REPORT_EXPORT_JOB_NOT_CANCELLABLE`.
            """)]
    public async Task<ActionResult<ReportExportJobResponse>> Cancel(
        Guid jobId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(new CancelReportExportJobCommand(jobId, concurrencyToken), cancellationToken);
        return this.ToActionResultWithETag(result, static value => value.ConcurrencyToken);
    }
}

public sealed record CreateReportExportJobRequest(
    string ResourceKey,
    string Format,
    JsonElement Parameters);
