using Asp.Versioning;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.Locations.WorkCenters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}")]
[Tags("Work Centers")]
[AuthorizationPolicySet(LocationPolicies.Read, LocationPolicies.Manage)]
public sealed class WorkCentersController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/work-centers")]
    [ProducesResponseType<PagedResponse<WorkCenterResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List work centers for a company",
        Description = """
            Returns a paginated list of work centers for the company, filterable by `groupId`,
            `typeId`, `isActive` and free-text `q`. The owning company is validated against the
            authenticated tenant. Set `includeAllowedActions=true` to receive per-item
            read/manage flags.
            """)]
    public async Task<ActionResult<PagedResponse<WorkCenterResponse>>> Search(
        Guid companyId,
        [FromQuery] Guid? groupId,
        [FromQuery] Guid? typeId,
        [FromQuery] bool? isActive,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchWorkCentersQuery(companyId, groupId, typeId, isActive, q, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("work-centers/{id:guid}")]
    [ProducesResponseType<WorkCenterResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a work center by id",
        Description = """
            Returns a single work center by its public id. The owning company is resolved from
            the authenticated tenant; a work center belonging to another tenant yields `404`. The
            `concurrencyToken` is emitted as the `ETag` header on mutations.
            """)]
    public async Task<ActionResult<WorkCenterResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetWorkCenterByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("companies/{companyId:guid}/work-centers")]
    [ProducesResponseType<WorkCenterResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create a work center",
        Description = """
            Creates a work center under the company and returns `201 Created` with the `Location`
            header pointing to the new resource and the `ETag` header carrying its initial
            `concurrencyToken`. The work center type and location group are referenced by public
            id; the group's level must allow work centers and the type may require an address or
            geo coordinates. A duplicate code yields `409`.
            """)]
    public async Task<ActionResult<WorkCenterResponse>> Create(
        Guid companyId,
        [FromBody] CreateWorkCenterRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateWorkCenterCommand(
                companyId,
                request.Code,
                request.Name,
                request.WorkCenterTypePublicId,
                request.LocationGroupPublicId,
                request.Address,
                request.GeoLat,
                request.GeoLong,
                request.Phone,
                request.Email,
                request.Notes),
            cancellationToken);

        // The PublicContractRouteConvention rewrites the GetById route token `{id}` to
        // `{publicId}`, so the Location route value MUST be keyed `publicId` (not `id`).
        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("work-centers/{id:guid}")]
    [ProducesResponseType<WorkCenterResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update a work center",
        Description = """
            Replaces the editable fields of a work center (code, name, type, group, address, geo,
            contact, notes). Requires the current `concurrencyToken` in the `If-Match` header (a
            missing/malformed header yields `400` and a stale token yields `409
            CONCURRENCY_CONFLICT`). A duplicate code yields `409`; type/group assignment rules
            yield `409`/`422`. The refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<WorkCenterResponse>> Update(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateWorkCenterRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateWorkCenterCommand(
                id,
                request.Code,
                request.Name,
                request.WorkCenterTypePublicId,
                request.LocationGroupPublicId,
                request.Address,
                request.GeoLat,
                request.GeoLong,
                request.Phone,
                request.Email,
                request.Notes,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("work-centers/{id:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<WorkCenterResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a work center",
        Description = """
            Applies a partial update using JSON Patch (RFC 6902), media type
            `application/json-patch+json`. Supported operations are `add`/`replace`/`remove` on
            root paths `/code`, `/name`, `/address`, `/geoLat`, `/geoLong`, `/phone`, `/email`,
            `/notes`. The type and group are changed via PUT or the `/reassign-group` endpoint;
            activation via `/activate` and `/inactivate`. Type-dependent rules (address/geo
            required by the type) are re-validated. Requires the current `concurrencyToken` in the
            `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in
            the body and the `ETag` header.
            """)]
    public async Task<ActionResult<WorkCenterResponse>> Patch(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchWorkCenterRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchWorkCenterCommand(
                id,
                concurrencyToken,
                JsonPatchOperationMapper.Map(
                    patchDoc,
                    static (op, path, from, value) => new WorkCenterPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("work-centers/{id:guid}/reassign-group")]
    [ProducesResponseType<WorkCenterResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Reassign a work center to another location group",
        Description = """
            Moves the work center to a different location group (by public id). The target group's
            level must allow work centers, otherwise `409`. Requires the current `concurrencyToken`
            in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is
            returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<WorkCenterResponse>> ReassignGroup(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] ReassignWorkCenterGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReassignWorkCenterGroupCommand(id, request.LocationGroupPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("work-centers/{id:guid}/activate")]
    [ProducesResponseType<WorkCenterResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Activate a work center",
        Description = """
            Reactivates an inactive work center. Requires the current `concurrencyToken` in the
            `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in
            the body and the `ETag` header.
            """)]
    public async Task<ActionResult<WorkCenterResponse>> Activate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateWorkCenterCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("work-centers/{id:guid}/inactivate")]
    [ProducesResponseType<WorkCenterResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Inactivate a work center",
        Description = """
            Deactivates (soft-delete) a work center. Fails with `409` if it still has active
            dependencies. Requires the current `concurrencyToken` in the `If-Match` header
            (missing → `400`, stale → `409`). The refreshed token is returned in the body and the
            `ETag` header.
            """)]
    public async Task<ActionResult<WorkCenterResponse>> Inactivate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateWorkCenterCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record CreateWorkCenterRequest(
        string Code,
        string Name,
        Guid WorkCenterTypePublicId,
        Guid LocationGroupPublicId,
        string? Address,
        decimal? GeoLat,
        decimal? GeoLong,
        string? Phone,
        string? Email,
        string? Notes);

    public sealed record UpdateWorkCenterRequest(
        string Code,
        string Name,
        Guid WorkCenterTypePublicId,
        Guid LocationGroupPublicId,
        string? Address,
        decimal? GeoLat,
        decimal? GeoLong,
        string? Phone,
        string? Email,
        string? Notes);

    public sealed record ReassignWorkCenterGroupRequest(Guid LocationGroupPublicId);

    public sealed class PatchWorkCenterRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public decimal? GeoLat { get; set; }
        public decimal? GeoLong { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Notes { get; set; }
    }
}
