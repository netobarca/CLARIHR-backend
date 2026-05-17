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
// AuthZ (defense-in-depth): GET→PositionDescriptionCatalogPolicies.Read, POST/PATCH→PositionDescriptionCatalogPolicies.Manage — assigned centrally by AuthorizationPolicyConvention.
public sealed class PositionCategoryClassificationsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher)
    : ControllerBase
{
    [HttpGet("companies/{companyPublicId:guid}/position-category-classifications")]
    [ProducesResponseType<PagedResponse<PositionCategoryClassificationResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    public async Task<ActionResult<PagedResponse<PositionCategoryClassificationResponse>>> Get(
        Guid companyPublicId,
        [FromQuery] Guid? positionFunctionTypePublicId,
        [FromQuery] Guid? positionContractTypePublicId,
        [FromQuery] Guid? orgUnitTypePublicId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [Range(1, PositionDescriptionCatalogValidationRules.MaxPageSize)]
        [FromQuery] int pageSize = PositionDescriptionCatalogValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchPositionCategoryClassificationsQuery(
                companyPublicId,
                positionFunctionTypePublicId,
                positionContractTypePublicId,
                orgUnitTypePublicId,
                isActive,
                search,
                page,
                pageSize,
                includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("position-category-classifications/{positionCategoryClassificationPublicId:guid}")]
    [ProducesResponseType<PositionCategoryClassificationResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    public async Task<ActionResult<PositionCategoryClassificationResponse>> GetById(
        Guid positionCategoryClassificationPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPositionCategoryClassificationByIdQuery(positionCategoryClassificationPublicId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("companies/{companyPublicId:guid}/position-category-classifications")]
    [ProducesResponseType<PositionCategoryClassificationResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<PositionCategoryClassificationResponse>> Add(
        Guid companyPublicId,
        [FromBody] UpsertPositionCategoryClassificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreatePositionCategoryClassificationCommand(
                companyPublicId,
                request.Code,
                request.Name,
                request.Description,
                request.PositionFunctionTypePublicId,
                request.PositionContractTypePublicId,
                request.OrgUnitTypePublicId,
                request.SortOrder),
            cancellationToken);

        return this.ToCreatedAtActionResult(result, nameof(GetById), value => new { positionCategoryClassificationPublicId = value.Id });
    }

    [HttpPatch("position-category-classifications/{positionCategoryClassificationPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PositionCategoryClassificationResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Patch a position category classification",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to a position category classification.

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
    public async Task<ActionResult<PositionCategoryClassificationResponse>> Patch(
        Guid positionCategoryClassificationPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchPositionCategoryClassificationRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPositionCategoryClassificationCommand(
                positionCategoryClassificationPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PositionDescriptionCatalogPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResult(result);
    }

    public sealed class UpsertPositionCategoryClassificationRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid PositionFunctionTypePublicId { get; set; }
        public Guid PositionContractTypePublicId { get; set; }
        public Guid OrgUnitTypePublicId { get; set; }
        public int SortOrder { get; set; }
    }

    public sealed class PatchPositionCategoryClassificationRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid PositionFunctionTypePublicId { get; set; }
        public Guid PositionContractTypePublicId { get; set; }
        public Guid OrgUnitTypePublicId { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
