using Asp.Versioning;
using CLARIHR.Api.Authorization;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.CompetencyFramework;
using CLARIHR.Application.Features.CompetencyFramework.Common;
using CLARIHR.Application.Features.Reports.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}")]
[Tags("Competency Framework")]
[ResourceActions("JOB_PROFILE_COMPETENCY_MATRIX")]
public sealed class JobProfileCompetencyMatrixController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [HttpGet("job-profiles/{jobProfilePublicId:guid}/competency-matrix")]
    [ProducesResponseType<JobProfileCompetencyMatrixResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a job profile's competency matrix",
        Description = "Returns the job profile's competency matrix as JSON: its items, each item's conducts, and the current `concurrencyToken` (the job profile's token) for use in the `If-Match` header of a subsequent matrix replace. A job profile with no expectations yet returns `200` with an empty `items` array.")]
    public async Task<ActionResult<JobProfileCompetencyMatrixResponse>> GetJobProfileCompetencyMatrix(
        Guid jobProfilePublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetJobProfileCompetencyMatrixQuery(jobProfilePublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("job-profiles/{jobProfilePublicId:guid}/competency-matrix")]
    [ProducesResponseType<JobProfileCompetencyMatrixResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Replace a job profile's competency matrix",
        Description = "Replaces the entire competency matrix for the job profile. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). Structural/field validation errors return `400`; matrix-constraint violations (duplicate item tuples, conduct-set mismatches, or an `Archived` job profile) return `409`. The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<JobProfileCompetencyMatrixResponse>> UpdateJobProfileCompetencyMatrix(
        Guid jobProfilePublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateJobProfileCompetencyMatrixRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateJobProfileCompetencyMatrixCommand(
                jobProfilePublicId,
                request.Items?.Select(item => new JobProfileCompetencyMatrixItemInput(
                        item.OccupationalPyramidLevelPublicId,
                        item.CompetencyPublicId,
                        item.CompetencyTypePublicId,
                        item.BehaviorLevelPublicId,
                        item.ConductPublicIds ?? [],
                        item.ExpectedEvidence,
                        item.SortOrder))
                    .ToArray() ?? [],
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpGet("job-profiles/{jobProfilePublicId:guid}/competency-matrix/export")]
    [EnableRateLimiting(CompetencyFrameworkRateLimitPolicies.Export)]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesStandardErrors(StandardErrorSet.Query | StandardErrorSet.NotFound)]
    [SwaggerOperation(
        Summary = "Export a job profile's competency matrix",
        Description = "Exports the job profile's competency matrix as a downloadable report in the requested `format` (default `xlsx`). The export is bounded by a synchronous row limit; oversized matrices return `413`.")]
    public async Task<IActionResult> ExportJobProfileCompetencyMatrix(
        Guid jobProfilePublicId,
        [FromQuery] string format = "xlsx",
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportJobProfileCompetencyMatrixQuery(jobProfilePublicId, reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<JobProfileCompetencyMatrixExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "job-profile-competency-matrix",
            "CompetencyMatrix",
            AuditEntityTypes.JobProfileCompetencyMatrix,
            ReportExportResources.JobProfileCompetencyMatrix,
            "Exported job profile competency matrix report.",
            new { jobProfileId = jobProfilePublicId },
            CompetencyFrameworkErrors.ExportFormatInvalid,
            cancellationToken);
    }

    public sealed record UpdateJobProfileCompetencyMatrixRequest(
        IReadOnlyCollection<JobProfileCompetencyMatrixItemRequest>? Items);

    public sealed record JobProfileCompetencyMatrixItemRequest(
        Guid OccupationalPyramidLevelPublicId,
        Guid CompetencyPublicId,
        Guid CompetencyTypePublicId,
        Guid BehaviorLevelPublicId,
        IReadOnlyCollection<Guid>? ConductPublicIds,
        string? ExpectedEvidence,
        int SortOrder);
}
