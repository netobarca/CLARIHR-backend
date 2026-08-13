using Asp.Versioning;
using CLARIHR.Api.Authorization;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.CompetencyFramework;
using CLARIHR.Application.Features.CompetencyFramework.Common;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.Reports.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
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
        Description = "Returns the job profile's competency matrix as JSON: its items and each item's conducts. Every item carries its own `itemPublicId` and `concurrencyToken` for use in the `If-Match` header of a subsequent per-item update. A job profile with no expectations yet returns `200` with an empty `items` array.")]
    public async Task<ActionResult<JobProfileCompetencyMatrixResponse>> GetJobProfileCompetencyMatrix(
        Guid jobProfilePublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetJobProfileCompetencyMatrixQuery(jobProfilePublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("job-profiles/{jobProfilePublicId:guid}/competency-matrix/items/{itemPublicId:guid}")]
    [ProducesResponseType<JobProfileCompetencyMatrixItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a single competency matrix item",
        Description = "Returns one competency matrix item of the job profile with its derived competency / type / behavior-level triple, its conducts, and the current `concurrencyToken` for use in the `If-Match` header of a subsequent update.")]
    public async Task<ActionResult<JobProfileCompetencyMatrixItemResponse>> GetJobProfileCompetencyMatrixItem(
        Guid jobProfilePublicId,
        Guid itemPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetJobProfileCompetencyMatrixItemQuery(jobProfilePublicId, itemPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("job-profiles/{jobProfilePublicId:guid}/competency-matrix/items")]
    [ProducesResponseType<JobProfileCompetencyMatrixItemResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Query | StandardErrorSet.NotFound | StandardErrorSet.Conflict)]
    [SwaggerOperation(
        Summary = "Add a competency matrix item",
        Description = """
            Creates a single competency matrix item under the job profile.

            **An item IS one cell of a (competency, competency type, behaviour level) triple**, and that triple
            is DERIVED from `conductPublicIds` — `competencyCatalogItemId`, `competencyTypeCatalogItemId` and
            `behaviorLevelCatalogItemId` are response fields and must not be supplied. Consequence, and the
            rule most easily broken by reading `conductPublicIds` as a plain list: **every conduct in one item
            must share the same triple.** Conducts that disagree have no single cell to define and are refused
            with `409 JOB_PROFILE_COMPETENCY_MATRIX_CONDUCT_TRIPLE_MISMATCH` — split them into one item per
            triple.

            `conductPublicIds` is **required and non-empty** (an empty list returns `400`).

            Caps: **50 conducts per item**, **200 items per job profile**.

            Returns `201`; the `Location` header points to the created item and its `concurrencyToken` comes
            back in the body and the `ETag` header. Other `409`s: a duplicate item tuple, an `Archived` job
            profile, or exceeding the per-profile item cap.
            """)]
    public async Task<ActionResult<JobProfileCompetencyMatrixItemResponse>> AddJobProfileCompetencyMatrixItem(
        Guid jobProfilePublicId,
        [FromBody] MutateJobProfileCompetencyMatrixItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddJobProfileCompetencyMatrixItemCommand(
                jobProfilePublicId,
                request.OccupationalPyramidLevelPublicId,
                request.ConductPublicIds,
                request.ExpectedEvidence,
                request.ExpectedValue,
                request.SortOrder),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetJobProfileCompetencyMatrixItem),
            value => new { jobProfilePublicId, itemPublicId = value.ItemPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("job-profiles/{jobProfilePublicId:guid}/competency-matrix/items/{itemPublicId:guid}")]
    [ProducesResponseType<JobProfileCompetencyMatrixItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Replace a competency matrix item",
        Description = """
            Replaces all fields of an existing competency matrix item.

            Same derivation rule as the `POST`: the item's (competency, competency type, behaviour level) triple
            is DERIVED from `conductPublicIds` and must not be supplied, so **every conduct in one item must
            share the same triple** — otherwise `409 JOB_PROFILE_COMPETENCY_MATRIX_CONDUCT_TRIPLE_MISMATCH`.

            `conductPublicIds` is **required and non-empty**. Caps: **50 conducts per item**, **200 items per
            job profile**.

            Requires the item's current `concurrencyToken` in the `If-Match` header (missing → `400`, stale →
            `409`). Structural/field validation errors return `400`; other matrix-constraint violations
            (duplicate item tuple, an `Archived` job profile) return `409`. The refreshed token is returned in
            the body and the `ETag` header.
            """)]
    public async Task<ActionResult<JobProfileCompetencyMatrixItemResponse>> UpdateJobProfileCompetencyMatrixItem(
        Guid jobProfilePublicId,
        Guid itemPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] MutateJobProfileCompetencyMatrixItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateJobProfileCompetencyMatrixItemCommand(
                jobProfilePublicId,
                itemPublicId,
                request.OccupationalPyramidLevelPublicId,
                request.ConductPublicIds,
                request.ExpectedEvidence,
                request.ExpectedValue,
                request.SortOrder,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("job-profiles/{jobProfilePublicId:guid}/competency-matrix/items/{itemPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<JobProfileCompetencyMatrixItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Patch a competency matrix item (RFC 6902 JSON Patch)",
        Description = "Applies a partial update using JSON Patch (RFC 6902), media type `application/json-patch+json`. Patchable paths: `/occupationalPyramidLevelPublicId`, `/conductPublicIds`, `/expectedEvidence`, `/expectedValue`, `/sortOrder`. The competency / type / behavior level are re-derived from `conductPublicIds`. Requires the item's current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<JobProfileCompetencyMatrixItemResponse>> PatchJobProfileCompetencyMatrixItem(
        Guid jobProfilePublicId,
        Guid itemPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchJobProfileCompetencyMatrixItemRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchJobProfileCompetencyMatrixItemCommand(
                jobProfilePublicId,
                itemPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(
                    patchDoc,
                    static (op, path, from, value) => new JobProfileCompetencyMatrixItemPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("job-profiles/{jobProfilePublicId:guid}/competency-matrix/items/{itemPublicId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a competency matrix item",
        Description = "Deletes the specified competency matrix item. Requires the item's current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). Returns the parent job profile's updated concurrency token so the caller can continue mutating the profile without an extra round-trip.")]
    public async Task<ActionResult> RemoveJobProfileCompetencyMatrixItem(
        Guid jobProfilePublicId,
        Guid itemPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new RemoveJobProfileCompetencyMatrixItemCommand(jobProfilePublicId, itemPublicId, concurrencyToken),
            cancellationToken);

        return this.ToNoContentResult(result);
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

    public sealed record MutateJobProfileCompetencyMatrixItemRequest(
        Guid OccupationalPyramidLevelPublicId,
        // H-06 — obligatorio de verdad. Se declaraba nullable y el validador lo exigía, así que el OpenAPI
        // prometía un campo opcional que en runtime siempre se rechazaba vacío. El comportamiento no cambia;
        // lo que cambia es que el contrato ahora dice la verdad. La terna (competencia, tipo, nivel) del ítem
        // se DERIVA de estas conductas, y por eso no puede venir vacía.
        IReadOnlyCollection<Guid> ConductPublicIds,
        string? ExpectedEvidence,
        decimal? ExpectedValue,
        int SortOrder);

    public sealed class PatchJobProfileCompetencyMatrixItemRequest
    {
        public Guid OccupationalPyramidLevelPublicId { get; set; }

        public IReadOnlyCollection<Guid> ConductPublicIds { get; set; } = [];

        public string? ExpectedEvidence { get; set; }

        public decimal? ExpectedValue { get; set; }

        public int SortOrder { get; set; }
    }
}
