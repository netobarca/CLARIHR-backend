using Asp.Versioning;
using System.ComponentModel.DataAnnotations;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PositionDescriptionCatalogs;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}")]
[Consumes("application/json")]
[Produces("application/json")]
[Tags("Position Description Catalogs")]
[AuthorizationPolicySet(PositionDescriptionCatalogPolicies.Read, PositionDescriptionCatalogPolicies.Manage)]
public sealed class PositionCategoriesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher)
    : ControllerBase
{
    [HttpGet("companies/{companyPublicId:guid}/position-categories")]
    [ProducesResponseType<PagedResponse<PositionCategoryResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List position categories",
        Description = """
            Returns a paginated list of position categories for the given company.
            Supports optional filtering by `classificationPublicId`, `isActive`
            and a free-text query (`q`) matched against code and name.
            Set `includeAllowedActions=true` to include, per item, the operations
            the current user is authorized to perform on it.
            """)]
    public async Task<ActionResult<PagedResponse<PositionCategoryResponse>>> Get(
        Guid companyPublicId,
        [FromQuery] Guid? classificationPublicId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [Range(1, PositionDescriptionCatalogValidationRules.MaxPageSize)]
        [FromQuery] int pageSize = PositionDescriptionCatalogValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchPositionCategoriesQuery(
                companyPublicId,
                classificationPublicId,
                isActive,
                search,
                page,
                pageSize,
                includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("position-categories/{positionCategoryPublicId:guid}")]
    [ProducesResponseType<PositionCategoryResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a position category by id",
        Description = """
            Returns a single position category by its public id. The owning company
            is resolved from the authenticated tenant (no `companyPublicId` in the
            route). The `concurrencyToken` travels in the body and is also emitted as
            the `ETag` response header for conditional requests / `If-Match` on PATCH.
            """)]
    public async Task<ActionResult<PositionCategoryResponse>> GetById(
        Guid positionCategoryPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPositionCategoryByIdQuery(positionCategoryPublicId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("companies/{companyPublicId:guid}/position-categories")]
    [ProducesResponseType<PositionCategoryResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Create a position category",
        Description = """
            Creates a position category under the given company. Returns `201 Created`
            with a `Location` header pointing to `GET /position-categories/{publicId}`
            (resolved against the routing table, version-relative). The created
            resource's `concurrencyToken` is returned for subsequent `PATCH`.
            """)]
    public async Task<ActionResult<PositionCategoryResponse>> Add(
        Guid companyPublicId,
        [FromBody] CreatePositionCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreatePositionCategoryCommand(
                companyPublicId,
                request.Code,
                request.Name,
                request.Description,
                request.ClassificationPublicId,
                request.SortOrder),
            cancellationToken);

        return this.ToCreatedAtActionResult(result, nameof(GetById), value => new { positionCategoryPublicId = value.Id });
    }

    [HttpPatch("position-categories/{positionCategoryPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PositionCategoryResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Patch a position category",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to a position category.

            **Full replacement**: there is intentionally **no `PUT`** endpoint
            for this resource — `POST` creates and this PATCH is the single
            mutation verb. A full-field replacement is expressed as a JSON Patch
            with `replace` operations for each member. A `PUT` request returns
            `405 Method Not Allowed` by design.

            **Deletion / soft-delete**: there is intentionally **no `DELETE`**
            endpoint for this resource. Deactivation ("soft-delete") and
            reactivation are performed through this PATCH by replacing the
            `isActive` member, e.g.
            `[{ "op": "replace", "path": "/isActive", "value": false }]`.
            A `DELETE` request returns `405 Method Not Allowed` by design.

            Requires the `If-Match` header with the current `concurrencyToken`
            to prevent lost updates.
            """)]
    public async Task<ActionResult<PositionCategoryResponse>> Patch(
        Guid positionCategoryPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchPositionCategoryRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPositionCategoryCommand(
                positionCategoryPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PositionDescriptionCatalogPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResult(result);
    }

    public class CreatePositionCategoryRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid ClassificationPublicId { get; set; }
        public int SortOrder { get; set; }
    }

    public sealed class PatchPositionCategoryRequest : CreatePositionCategoryRequest
    {
        public bool IsActive { get; set; } = true;
    }
}
