using System.Text.Json;
using CLARIHR.Api.Common;
using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Application.Abstractions.Reports;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Reports;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.Files;
using CLARIHR.Domain.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class ReportExportJobsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    IFileStorageProviderResolver providerResolver,
    IFilePurposeRuleProvider ruleProvider) : ControllerBase
{
    [HttpPost("api/v1/companies/{companyId:guid}/report-export-jobs")]
    [ProducesResponseType<ReportExportJobResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
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
    [ProducesResponseType<PagedResponse<ReportExportJobResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<ReportExportJobResponse>>> Search(
        Guid companyId,
        [FromQuery] ReportExportJobStatus? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchReportExportJobsQuery(companyId, status, pageNumber, pageSize),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/report-export-jobs/{jobId:guid}")]
    [ProducesResponseType<ReportExportJobResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReportExportJobResponse>> GetById(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetReportExportJobQuery(jobId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/report-export-jobs/{jobId:guid}/download")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status410Gone)]
    public async Task<IActionResult> Download(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetReportExportJobDownloadQuery(jobId), cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(Result<ReportExportJobDownloadResponse>.Failure(result.Error)).Result!;
        }

        var rule = ruleProvider.GetRule(FilePurpose.ReportExport);
        if (rule is null)
        {
            return CLARIHR.Api.Common.ProblemDetailsFactory.Create(HttpContext, ReportPolicyErrors.ExportStorageNotConfigured);
        }

        var provider = providerResolver.Resolve(rule.DefaultProvider);
        var containerName = rule.ContainerOverride ?? "clarihr-files";
        var stream = await provider.OpenReadStreamAsync(containerName, result.Value.BlobName, cancellationToken);
        if (stream is null)
        {
            return CLARIHR.Api.Common.ProblemDetailsFactory.Create(HttpContext, ReportPolicyErrors.ExportJobExpired);
        }

        return File(stream, result.Value.ContentType, result.Value.FileName);
    }

    [HttpPatch("api/v1/report-export-jobs/{jobId:guid}/cancel")]
    [ProducesResponseType<ReportExportJobResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReportExportJobResponse>> Cancel(
        Guid jobId,
        [FromBody] CancelReportExportJobRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(new CancelReportExportJobCommand(jobId, request.ConcurrencyToken), cancellationToken);
        return this.ToActionResult(result);
    }
}

public sealed record CreateReportExportJobRequest(
    string ResourceKey,
    string Format,
    JsonElement Parameters);
