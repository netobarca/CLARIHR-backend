using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.Locations.WorkCenterTypes;
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
[Tags("Work Center Types")]
[AuthorizationPolicySet(LocationPolicies.Read, LocationPolicies.Manage)]
public sealed class WorkCenterTypesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/work-center-types")]
    [EnableRateLimiting(LocationRateLimitPolicies.Search)]
    [ProducesResponseType<PagedResponse<WorkCenterTypeResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List work center types for a company",
        Description = """
            Returns a paginated list of work center types for the company, filterable by
            `isActive` and free-text `q`. The owning company is validated against the
            authenticated tenant. Set `includeAllowedActions=true` to receive per-item
            read/manage flags.
            """)]
    public async Task<ActionResult<PagedResponse<WorkCenterTypeResponse>>> List(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [Range(1, LocationValidationRules.MaxPageSize)]
        [FromQuery] int pageSize = LocationValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetWorkCenterTypesQuery(companyId, isActive, q, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("work-center-types/{id:guid}")]
    [ProducesResponseType<WorkCenterTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a work center type by id",
        Description = """
            Returns a single work center type by its public id. The owning company is resolved
            from the authenticated tenant; a type belonging to another tenant yields `404`. The
            `concurrencyToken` is emitted as the `ETag` header on mutations.
            """)]
    public async Task<ActionResult<WorkCenterTypeResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetWorkCenterTypeByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("companies/{companyId:guid}/work-center-types")]
    [ProducesResponseType<WorkCenterTypeResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create a work center type",
        Description = """
            Creates a work center type under the company and returns `201 Created` with the
            `Location` header pointing to the new resource and the `ETag` header carrying its
            initial `concurrencyToken`. The flags `requiresAddress`/`requiresGeo` control the
            validation applied to work centers of this type. A duplicate code yields `409`.
            """)]
    public async Task<ActionResult<WorkCenterTypeResponse>> Create(
        Guid companyId,
        [FromBody] CreateWorkCenterTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateWorkCenterTypeCommand(
                companyId,
                request.Code,
                request.Name,
                request.RequiresAddress,
                request.RequiresGeo,
                request.AllowsBiometric),
            cancellationToken);

        // The PublicContractRouteConvention rewrites the GetById route token `{id}` to
        // `{publicId}`, so the Location route value MUST be keyed `publicId` (not `id`).
        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("work-center-types/{id:guid}")]
    [ProducesResponseType<WorkCenterTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update a work center type",
        Description = """
            Replaces the editable fields of a work center type (code, name, requiresAddress,
            requiresGeo, allowsBiometric). Requires the current `concurrencyToken` in the
            `If-Match` header (a missing/malformed header yields `400` and a stale token yields
            `409 CONCURRENCY_CONFLICT`). A duplicate code yields `409`. The refreshed token is
            returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<WorkCenterTypeResponse>> Update(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateWorkCenterTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateWorkCenterTypeCommand(
                id,
                request.Code,
                request.Name,
                request.RequiresAddress,
                request.RequiresGeo,
                request.AllowsBiometric,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("work-center-types/{id:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<WorkCenterTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a work center type",
        Description = """
            Applies a partial update using JSON Patch (RFC 6902), media type
            `application/json-patch+json`. Supported operations are `add`/`replace` on root paths
            `/code`, `/name`, `/requiresAddress`, `/requiresGeo`, `/allowsBiometric` (activation is
            handled by the dedicated `/activate` and `/inactivate` endpoints). Requires the current
            `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The
            refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<WorkCenterTypeResponse>> Patch(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchWorkCenterTypeRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchWorkCenterTypeCommand(
                id,
                concurrencyToken,
                JsonPatchOperationMapper.Map(
                    patchDoc,
                    static (op, path, from, value) => new WorkCenterTypePatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("work-center-types/{id:guid}/activate")]
    [ProducesResponseType<WorkCenterTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Activate a work center type",
        Description = """
            Reactivates an inactive work center type. Requires the current `concurrencyToken` in
            the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned
            in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<WorkCenterTypeResponse>> Activate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateWorkCenterTypeCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("work-center-types/{id:guid}/inactivate")]
    [ProducesResponseType<WorkCenterTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Inactivate a work center type",
        Description = """
            Deactivates (soft-delete) a work center type. Fails with `409` if active work centers
            still use it. Requires the current `concurrencyToken` in the `If-Match` header
            (missing → `400`, stale → `409`). The refreshed token is returned in the body and the
            `ETag` header.
            """)]
    public async Task<ActionResult<WorkCenterTypeResponse>> Inactivate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateWorkCenterTypeCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record CreateWorkCenterTypeRequest(
        string Code,
        string Name,
        bool RequiresAddress,
        bool RequiresGeo,
        bool AllowsBiometric);

    public sealed record UpdateWorkCenterTypeRequest(
        string Code,
        string Name,
        bool RequiresAddress,
        bool RequiresGeo,
        bool AllowsBiometric);

    public sealed class PatchWorkCenterTypeRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool RequiresAddress { get; set; }
        public bool RequiresGeo { get; set; }
        public bool AllowsBiometric { get; set; }
    }
}
