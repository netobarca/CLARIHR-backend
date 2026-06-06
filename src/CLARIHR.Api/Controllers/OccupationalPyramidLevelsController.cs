using Asp.Versioning;
using System.ComponentModel.DataAnnotations;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CompetencyFramework;
using CLARIHR.Application.Features.CompetencyFramework.Common;
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
public sealed class OccupationalPyramidLevelsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/occupational-pyramid-levels")]
    [EnableRateLimiting(CompetencyFrameworkRateLimitPolicies.Search)]
    [ProducesResponseType<PagedResponse<OccupationalPyramidLevelListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "Search occupational pyramid levels",
        Description = "Returns a paged list of the company's occupational pyramid levels.")]
    public async Task<ActionResult<PagedResponse<OccupationalPyramidLevelListItemResponse>>> SearchOccupationalPyramidLevels(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [Range(1, CompetencyFrameworkValidationRules.MaxPageSize)]
        [FromQuery] int pageSize = CompetencyFrameworkValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchOccupationalPyramidLevelsQuery(companyId, isActive, search, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("occupational-pyramid-levels/{id:guid}")]
    [ProducesResponseType<OccupationalPyramidLevelResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get an occupational pyramid level",
        Description = "Returns a single occupational pyramid level. The current `concurrencyToken` is included in the body for use in the `If-Match` header of a subsequent update.")]
    public async Task<ActionResult<OccupationalPyramidLevelResponse>> GetOccupationalPyramidLevelById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetOccupationalPyramidLevelByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("companies/{companyId:guid}/occupational-pyramid-levels")]
    [ProducesResponseType<OccupationalPyramidLevelResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Query | StandardErrorSet.Conflict)]
    [SwaggerOperation(
        Summary = "Create an occupational pyramid level",
        Description = "Creates an occupational pyramid level. Returns `201`; the current `concurrencyToken` is included in the body and the `ETag` header.")]
    public async Task<ActionResult<OccupationalPyramidLevelResponse>> CreateOccupationalPyramidLevel(
        Guid companyId,
        [FromBody] CreateOccupationalPyramidLevelRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateOccupationalPyramidLevelCommand(companyId, request.Code, request.Name, request.LevelOrder, request.Description),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetOccupationalPyramidLevelById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("occupational-pyramid-levels/{id:guid}")]
    [ProducesResponseType<OccupationalPyramidLevelResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Update an occupational pyramid level",
        Description = "Replaces the editable fields. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<OccupationalPyramidLevelResponse>> UpdateOccupationalPyramidLevel(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateOccupationalPyramidLevelRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateOccupationalPyramidLevelCommand(id, request.Code, request.Name, request.LevelOrder, request.Description, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("occupational-pyramid-levels/{id:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<OccupationalPyramidLevelResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Patch an occupational pyramid level (RFC 6902 JSON Patch)",
        Description = "Applies a partial update using JSON Patch (RFC 6902), media type `application/json-patch+json`. Patchable paths: `/code`, `/name`, `/levelOrder`, `/description` (optional, accepts `null` or remove to clear). Activation state uses the `/activate` and `/inactivate` actions. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<OccupationalPyramidLevelResponse>> PatchOccupationalPyramidLevel(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchOccupationalPyramidLevelRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchOccupationalPyramidLevelCommand(
                id,
                concurrencyToken,
                JsonPatchOperationMapper.Map(
                    patchDoc,
                    static (op, path, from, value) => new OccupationalPyramidLevelPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("occupational-pyramid-levels/{id:guid}/activate")]
    [ProducesResponseType<OccupationalPyramidLevelResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Activate an occupational pyramid level",
        Description = "Activates the level. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<OccupationalPyramidLevelResponse>> ActivateOccupationalPyramidLevel(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateOccupationalPyramidLevelCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("occupational-pyramid-levels/{id:guid}/inactivate")]
    [ProducesResponseType<OccupationalPyramidLevelResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Inactivate an occupational pyramid level",
        Description = "Inactivates the level. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<OccupationalPyramidLevelResponse>> InactivateOccupationalPyramidLevel(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateOccupationalPyramidLevelCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record CreateOccupationalPyramidLevelRequest(
        string Code,
        string Name,
        int LevelOrder,
        string? Description);

    public sealed record UpdateOccupationalPyramidLevelRequest(
        string Code,
        string Name,
        int LevelOrder,
        string? Description);

    public sealed class PatchOccupationalPyramidLevelRequest
    {
        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public int LevelOrder { get; set; }

        public string? Description { get; set; }
    }
}
