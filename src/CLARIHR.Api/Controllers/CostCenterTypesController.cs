using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using CLARIHR.Api.Authorization;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CostCenters.Common;
using CLARIHR.Application.Features.CostCenters.Types;
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
[Tags("Cost Center Types")]
[AuthorizationPolicySet(CostCenterPolicies.Read, CostCenterPolicies.Manage)]
[ResourceActions(CostCenterPermissionCodes.ResourceKey)]
public sealed class CostCenterTypesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/cost-center-types")]
    [EnableRateLimiting(CostCenterRateLimitPolicies.Search)]
    [ProducesResponseType<PagedResponse<CostCenterTypeListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List cost center types for a company",
        Description = """
            Returns a paginated list of cost center types for the company, filterable by
            `isActive` and free-text `q`. The owning company is validated against the
            authenticated tenant. List items omit `description` (detail-only); fetch the type by
            id for the full payload. Set `includeAllowedActions=true` to receive per-item
            read/manage flags.
            """)]
    public async Task<ActionResult<PagedResponse<CostCenterTypeListItemResponse>>> List(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [Range(1, CostCenterValidationRules.MaxPageSize)]
        [FromQuery] int pageSize = CostCenterValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetCostCenterTypesQuery(companyId, isActive, q, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("cost-center-types/{id:guid}")]
    [ProducesResponseType<CostCenterTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a cost center type by id",
        Description = """
            Returns a single cost center type by its public id. The owning company is resolved
            from the authenticated tenant; a type belonging to another tenant yields `403
            TENANT_MISMATCH` and a non-existent id yields `404`. The `concurrencyToken` is
            emitted as the `ETag` header on mutations.
            """)]
    public async Task<ActionResult<CostCenterTypeResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetCostCenterTypeByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("companies/{companyId:guid}/cost-center-types")]
    [ProducesResponseType<CostCenterTypeResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create a cost center type",
        Description = """
            Creates a cost center type under the company and returns `201 Created` with the
            `Location` header pointing to the new resource and the `ETag` header carrying its
            initial `concurrencyToken`. Cost centers reference these types by public id. A
            duplicate code yields `409`.
            """)]
    public async Task<ActionResult<CostCenterTypeResponse>> Create(
        Guid companyId,
        [FromBody] CreateCostCenterTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateCostCenterTypeCommand(
                companyId,
                request.Code,
                request.Name,
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

    [HttpPut("cost-center-types/{id:guid}")]
    [ProducesResponseType<CostCenterTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update a cost center type",
        Description = """
            Replaces the editable fields of a cost center type (code, name, description).
            Requires the current `concurrencyToken` in the `If-Match` header (a
            missing/malformed header yields `400` and a stale token yields
            `409 CONCURRENCY_CONFLICT`). A duplicate code yields `409`. The refreshed token is
            returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<CostCenterTypeResponse>> Update(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateCostCenterTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateCostCenterTypeCommand(
                id,
                request.Code,
                request.Name,
                request.Description,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("cost-center-types/{id:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<CostCenterTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a cost center type",
        Description = """
            Applies a partial update using JSON Patch (RFC 6902), media type
            `application/json-patch+json`. Supported operations are `add`/`replace`/`remove` on
            root paths `/code`, `/name`, `/description` (activation is handled by the dedicated
            `/activate` and `/inactivate` endpoints). Requires the current `concurrencyToken` in
            the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is
            returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<CostCenterTypeResponse>> Patch(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchCostCenterTypeRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchCostCenterTypeCommand(
                id,
                concurrencyToken,
                JsonPatchOperationMapper.Map(
                    patchDoc,
                    static (op, path, from, value) => new CostCenterTypePatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("cost-center-types/{id:guid}/activate")]
    [ProducesResponseType<CostCenterTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Activate a cost center type",
        Description = """
            Reactivates an inactive cost center type. Requires the current `concurrencyToken` in
            the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is
            returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<CostCenterTypeResponse>> Activate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateCostCenterTypeCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("cost-center-types/{id:guid}/inactivate")]
    [ProducesResponseType<CostCenterTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Inactivate a cost center type",
        Description = """
            Deactivates (soft-delete) a cost center type. Fails with `409` if active cost
            centers still use it. Requires the current `concurrencyToken` in the `If-Match`
            header (missing → `400`, stale → `409`). The refreshed token is returned in the body
            and the `ETag` header.
            """)]
    public async Task<ActionResult<CostCenterTypeResponse>> Inactivate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateCostCenterTypeCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record CreateCostCenterTypeRequest(
        string Code,
        string Name,
        string? Description);

    public sealed record UpdateCostCenterTypeRequest(
        string Code,
        string Name,
        string? Description);

    public sealed class PatchCostCenterTypeRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
