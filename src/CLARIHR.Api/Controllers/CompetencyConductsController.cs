using Asp.Versioning;
using System.ComponentModel.DataAnnotations;
using CLARIHR.Api.Authorization;
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
[ResourceActions(CompetencyFrameworkPermissionCodes.ResourceKey)]
public sealed class CompetencyConductsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/competency-conducts")]
    [EnableRateLimiting(CompetencyFrameworkRateLimitPolicies.Search)]
    [ProducesResponseType<PagedResponse<CompetencyConductListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "Search competency conducts",
        Description = "Returns a paged list of the company's competency conducts.")]
    public async Task<ActionResult<PagedResponse<CompetencyConductListItemResponse>>> SearchCompetencyConducts(
        Guid companyId,
        [FromQuery(Name = "competencyId")] Guid? competencyId,
        [FromQuery(Name = "competencyTypeId")] Guid? competencyTypeId,
        [FromQuery(Name = "behaviorLevelId")] Guid? behaviorLevelId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [Range(1, CompetencyFrameworkValidationRules.MaxPageSize)]
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

    [HttpGet("competency-conducts/{id:guid}")]
    [ProducesResponseType<CompetencyConductResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a competency conduct",
        Description = "Returns a single competency conduct. The current `concurrencyToken` is included in the body for use in the `If-Match` header of a subsequent update.")]
    public async Task<ActionResult<CompetencyConductResponse>> GetCompetencyConductById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetCompetencyConductByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("companies/{companyId:guid}/competency-conducts")]
    [ProducesResponseType<CompetencyConductResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Query | StandardErrorSet.NotFound | StandardErrorSet.Conflict)]
    [SwaggerOperation(
        Summary = "Create a competency conduct",
        Description = "Creates a competency conduct. Returns `201`; the current `concurrencyToken` is included in the body and the `ETag` header.")]
    public async Task<ActionResult<CompetencyConductResponse>> CreateCompetencyConduct(
        Guid companyId,
        [FromBody] CreateCompetencyConductRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateCompetencyConductCommand(
                companyId,
                request.CompetencyPublicId,
                request.CompetencyTypePublicId,
                request.BehaviorLevelPublicId,
                request.Description,
                request.SortOrder),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetCompetencyConductById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("competency-conducts/{id:guid}")]
    [ProducesResponseType<CompetencyConductResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Update a competency conduct",
        Description = "Replaces the editable fields. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<CompetencyConductResponse>> UpdateCompetencyConduct(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateCompetencyConductRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateCompetencyConductCommand(
                id,
                request.CompetencyPublicId,
                request.CompetencyTypePublicId,
                request.BehaviorLevelPublicId,
                request.Description,
                request.SortOrder,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("competency-conducts/{id:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<CompetencyConductResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Patch a competency conduct (RFC 6902 JSON Patch)",
        Description = "Applies a partial update using JSON Patch (RFC 6902), media type `application/json-patch+json`. Patchable paths: `/competencyPublicId`, `/competencyTypePublicId`, `/behaviorLevelPublicId`, `/description`, `/sortOrder`. Activation state uses the `/activate` and `/inactivate` actions; behaviors use the behaviors endpoint. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`); a duplicate competency/type/level/description tuple returns `409`. The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<CompetencyConductResponse>> PatchCompetencyConduct(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchCompetencyConductRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchCompetencyConductCommand(
                id,
                concurrencyToken,
                JsonPatchOperationMapper.Map(
                    patchDoc,
                    static (op, path, from, value) => new CompetencyConductPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("competency-conducts/{id:guid}/activate")]
    [ProducesResponseType<CompetencyConductResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Activate a competency conduct",
        Description = "Activates the conduct. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<CompetencyConductResponse>> ActivateCompetencyConduct(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateCompetencyConductCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("competency-conducts/{id:guid}/inactivate")]
    [ProducesResponseType<CompetencyConductResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Inactivate a competency conduct",
        Description = "Inactivates the conduct. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<CompetencyConductResponse>> InactivateCompetencyConduct(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateCompetencyConductCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPut("competency-conducts/{id:guid}/behaviors")]
    [ProducesResponseType<CompetencyConductResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Replace a competency conduct's behaviors",
        Description = "Replaces the conduct's behaviors collection. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<CompetencyConductResponse>> UpdateCompetencyConductBehaviors(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateCompetencyConductBehaviorsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateCompetencyConductBehaviorsCommand(
                id,
                request.Behaviors?.Select(item => new CompetencyConductBehaviorInput(item.BehaviorPublicId, item.Notes, item.SortOrder)).ToArray() ?? [],
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record CreateCompetencyConductRequest(
        Guid CompetencyPublicId,
        Guid CompetencyTypePublicId,
        Guid BehaviorLevelPublicId,
        string Description,
        int SortOrder);

    public sealed record UpdateCompetencyConductRequest(
        Guid CompetencyPublicId,
        Guid CompetencyTypePublicId,
        Guid BehaviorLevelPublicId,
        string Description,
        int SortOrder);

    public sealed class PatchCompetencyConductRequest
    {
        public Guid CompetencyPublicId { get; set; }

        public Guid CompetencyTypePublicId { get; set; }

        public Guid BehaviorLevelPublicId { get; set; }

        public string Description { get; set; } = string.Empty;

        public int SortOrder { get; set; }
    }

    public sealed record UpdateCompetencyConductBehaviorsRequest(
        IReadOnlyCollection<CompetencyConductBehaviorRequest>? Behaviors);

    public sealed record CompetencyConductBehaviorRequest(
        Guid BehaviorPublicId,
        string? Notes,
        int SortOrder);
}
