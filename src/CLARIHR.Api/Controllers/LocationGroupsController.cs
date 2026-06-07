using Asp.Versioning;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.Locations.Groups;
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
[Tags("Location Groups")]
[AuthorizationPolicySet(LocationPolicies.Read, LocationPolicies.Manage)]
public sealed class LocationGroupsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/location-groups/tree")]
    [EnableRateLimiting(LocationRateLimitPolicies.Tree)]
    [ProducesResponseType<IReadOnlyCollection<LocationGroupTreeNodeResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "Get the location groups tree",
        Description = """
            Returns the full hierarchy of location groups for the company as a nested tree
            (each node carries its `children`). The owning company is validated against the
            authenticated tenant. This is the hierarchical projection; the flat paginated list
            is served by the sibling list endpoint.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<LocationGroupTreeNodeResponse>>> Tree(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetLocationGroupTreeQuery(companyId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("companies/{companyId:guid}/location-groups")]
    [EnableRateLimiting(LocationRateLimitPolicies.Search)]
    [ProducesResponseType<PagedResponse<LocationGroupResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List location groups for a company",
        Description = """
            Returns a paginated flat list of location groups for the company, filterable by
            `levelOrder`, `isActive` and free-text `q`. The owning company is validated against
            the authenticated tenant. Set `includeAllowedActions=true` to receive per-item
            read/manage flags. For the hierarchy use the `/tree` endpoint.
            """)]
    public async Task<ActionResult<PagedResponse<LocationGroupResponse>>> Search(
        Guid companyId,
        [FromQuery] int? levelOrder,
        [FromQuery] bool? isActive,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchLocationGroupsQuery(companyId, levelOrder, isActive, q, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("location-groups/{id:guid}")]
    [ProducesResponseType<LocationGroupResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a location group by id",
        Description = """
            Returns a single location group by its public id. The owning company is resolved from
            the authenticated tenant; a group belonging to another tenant yields `404`. The
            `concurrencyToken` is emitted as the `ETag` header on mutations.
            """)]
    public async Task<ActionResult<LocationGroupResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetLocationGroupByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("companies/{companyId:guid}/location-groups")]
    [ProducesResponseType<LocationGroupResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create a location group",
        Description = """
            Creates a location group under the company at the given `levelOrder` and returns
            `201 Created` with the `Location` header pointing to the new resource and the `ETag`
            header carrying its initial `concurrencyToken`. A parent (by public id) is required for
            non-root levels and must sit exactly one level above. A duplicate code yields `409`.
            """)]
    public async Task<ActionResult<LocationGroupResponse>> Create(
        Guid companyId,
        [FromBody] CreateLocationGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateLocationGroupCommand(
                companyId,
                request.LevelOrder,
                request.Code,
                request.Name,
                request.ParentPublicId,
                request.Description),
            cancellationToken);

        // The PublicContractRouteConvention rewrites the GetById route token `{id}` to
        // `{publicId}`, so the Location route value MUST be keyed `publicId` (not `id`).
        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("location-groups/{id:guid}")]
    [ProducesResponseType<LocationGroupResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update a location group",
        Description = """
            Replaces the editable fields of a location group (code, name, description). The level
            and parent are immutable here (use `/move` to reparent). The default group's
            code/name are protected (`409`). Requires the current `concurrencyToken` in the
            `If-Match` header (missing → `400`, stale → `409`). A duplicate code yields `409`. The
            refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<LocationGroupResponse>> Update(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateLocationGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateLocationGroupCommand(
                id,
                request.Code,
                request.Name,
                request.Description,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("location-groups/{id:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<LocationGroupResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a location group",
        Description = """
            Applies a partial update using JSON Patch (RFC 6902), media type
            `application/json-patch+json`. Supported operations are `add`/`replace`/`remove` on
            root paths `/code`, `/name`, `/description`. The level is immutable, the parent is
            changed via `/move`, and activation via `/activate` and `/inactivate`. The default
            group's code/name are protected (`409`). Requires the current `concurrencyToken` in the
            `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in
            the body and the `ETag` header.
            """)]
    public async Task<ActionResult<LocationGroupResponse>> Patch(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchLocationGroupRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchLocationGroupCommand(
                id,
                concurrencyToken,
                JsonPatchOperationMapper.Map(
                    patchDoc,
                    static (op, path, from, value) => new LocationGroupPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("location-groups/{id:guid}/move")]
    [ProducesResponseType<LocationGroupResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Move a location group to another parent",
        Description = """
            Reparents the location group (by parent public id). The new parent must sit exactly one
            level above and be active; a move that would create a cycle yields `409`. Root-level
            groups take no parent. Requires the current `concurrencyToken` in the `If-Match` header
            (missing → `400`, stale → `409`). The refreshed token is returned in the body and the
            `ETag` header.
            """)]
    public async Task<ActionResult<LocationGroupResponse>> Move(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] MoveLocationGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new MoveLocationGroupCommand(id, request.ParentPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("location-groups/{id:guid}/activate")]
    [ProducesResponseType<LocationGroupResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Activate a location group",
        Description = """
            Reactivates an inactive location group. Requires the current `concurrencyToken` in the
            `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in
            the body and the `ETag` header.
            """)]
    public async Task<ActionResult<LocationGroupResponse>> Activate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateLocationGroupCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("location-groups/{id:guid}/inactivate")]
    [ProducesResponseType<LocationGroupResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Inactivate a location group",
        Description = """
            Deactivates (soft-delete) a location group. Fails with `409` if it still has active
            child groups or active work centers, or if it is the protected default group. Requires
            the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale →
            `409`). The refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<LocationGroupResponse>> Inactivate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateLocationGroupCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record CreateLocationGroupRequest(
        int LevelOrder,
        string Code,
        string Name,
        Guid? ParentPublicId,
        string? Description);

    public sealed record UpdateLocationGroupRequest(
        string Code,
        string Name,
        string? Description);

    public sealed record MoveLocationGroupRequest(Guid? ParentPublicId);

    public sealed class PatchLocationGroupRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
