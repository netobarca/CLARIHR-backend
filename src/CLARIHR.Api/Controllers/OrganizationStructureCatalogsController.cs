using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using CLARIHR.Api.Authorization;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.OrgStructureCatalogs;
using CLARIHR.Application.Features.OrgStructureCatalogs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}")]
[Tags("Organization Structure Catalogs")]
[AuthorizationPolicySet(OrgStructureCatalogPolicies.Read, OrgStructureCatalogPolicies.Manage)]
[ResourceActions(OrgStructureCatalogPermissionCodes.ResourceKey)]
public sealed class OrganizationStructureCatalogsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher)
    : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/organization-structure-catalogs/unit-types")]
    [EnableRateLimiting(OrgStructureCatalogRateLimitPolicies.Search)]
    [ProducesResponseType<PagedResponse<OrgUnitTypeCatalogListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List org unit types for a company",
        Description = """
            Returns a paginated list of organization unit-type catalog items for the company,
            filterable by `isActive` and free-text `q`. The owning company is validated against the
            authenticated tenant. List items omit `description` (detail-only); fetch the item by id
            for the full payload. Set `includeAllowedActions=true` to receive per-item read/manage flags.
            """)]
    public async Task<ActionResult<PagedResponse<OrgUnitTypeCatalogListItemResponse>>> SearchOrgUnitTypes(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery, Range(1, OrgStructureCatalogValidationRules.MaxPageSize)] int pageSize = OrgStructureCatalogValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchOrgUnitTypesQuery(companyId, isActive, search, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("organization-structure-catalogs/unit-types/{id:guid}/usage")]
    [ProducesResponseType<OrgUnitTypeUsageResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a unit type's usage",
        Description = """
            Returns the active/inactive reference counts for the unit type across organization units
            and position category classifications, indicating whether it is safe to inactivate.
            Inactivation collapses both checks into a single `ORG_STRUCTURE_CATALOG_IN_USE`; this
            endpoint separates them so a client can say WHICH references block it.
            """)]
    public async Task<ActionResult<OrgUnitTypeUsageResponse>> UnitTypeUsage(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetOrgUnitTypeUsageQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("organization-structure-catalogs/unit-types/{id:guid}")]
    [ProducesResponseType<OrgUnitTypeCatalogItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get an org unit type by id",
        Description = """
            Returns a single organization unit-type catalog item by its public id. The owning company is
            resolved from the authenticated tenant; an item belonging to another tenant yields `404`. The
            `concurrencyToken` is emitted as the `ETag` header on mutations.
            """)]
    public async Task<ActionResult<OrgUnitTypeCatalogItemResponse>> GetOrgUnitTypeById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetOrgUnitTypeByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("companies/{companyId:guid}/organization-structure-catalogs/unit-types")]
    [ProducesResponseType<OrgUnitTypeCatalogItemResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create an org unit type",
        Description = """
            Creates an organization unit-type catalog item under the company and returns `201 Created`
            with the `Location` header pointing to the new resource and the `ETag` header carrying its
            initial `concurrencyToken`. A duplicate code yields `409`.
            """)]
    public async Task<ActionResult<OrgUnitTypeCatalogItemResponse>> CreateOrgUnitType(
        Guid companyId,
        [FromBody] UpsertCatalogItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateOrgUnitTypeCommand(companyId, request.Code, request.Name, request.Description, request.SortOrder),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetOrgUnitTypeById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("organization-structure-catalogs/unit-types/{id:guid}")]
    [ProducesResponseType<OrgUnitTypeCatalogItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update an org unit type",
        Description = """
            Replaces the editable fields (code, name, description, sort order) of an organization
            unit-type catalog item. Requires the current `concurrencyToken` in the `If-Match` header
            (missing → `400`, stale → `409`). A duplicate code yields `409`. The refreshed token is
            returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<OrgUnitTypeCatalogItemResponse>> UpdateOrgUnitType(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpsertCatalogItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateOrgUnitTypeCommand(id, request.Code, request.Name, request.Description, request.SortOrder, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("organization-structure-catalogs/unit-types/{id:guid}")]
    [ProducesResponseType<OrgUnitTypeCatalogItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Delete a unit type",
        Description = """
            Hard-deletes the unit type. Requires the current `concurrencyToken` in the `If-Match`
            header (missing → `400`, stale → `409`). Rejected with
            `409 ORG_STRUCTURE_CATALOG_IN_USE_FOR_DELETE` when anything still references it —
            organization units or position category classifications, active **or** inactive, since
            both foreign keys are `RESTRICT`. Use
            `GET /organization-structure-catalogs/unit-types/{id}/usage` to see which references
            block it. Inactivation remains the way to retire a type that is in use; this exists for
            the record created by mistake that nothing ever used.
            """)]
    public async Task<ActionResult<OrgUnitTypeCatalogItemResponse>> DeleteUnitType(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(new DeleteOrgUnitTypeCommand(id, concurrencyToken), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPatch("organization-structure-catalogs/unit-types/{id:guid}/activate")]
    [ProducesResponseType<OrgUnitTypeCatalogItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Activate an org unit type",
        Description = """
            Reactivates an inactive organization unit-type catalog item. Requires the current
            `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed
            token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<OrgUnitTypeCatalogItemResponse>> ActivateOrgUnitType(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateOrgUnitTypeCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("organization-structure-catalogs/unit-types/{id:guid}/inactivate")]
    [ProducesResponseType<OrgUnitTypeCatalogItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Inactivate an org unit type",
        Description = """
            Deactivates (soft-delete) an organization unit-type catalog item. Fails with `409` if it is
            still in use by org units or position-category classifications. Requires the current
            `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed
            token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<OrgUnitTypeCatalogItemResponse>> InactivateOrgUnitType(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateOrgUnitTypeCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpGet("companies/{companyId:guid}/organization-structure-catalogs/functional-areas")]
    [EnableRateLimiting(OrgStructureCatalogRateLimitPolicies.Search)]
    [ProducesResponseType<PagedResponse<FunctionalAreaCatalogListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List functional areas for a company",
        Description = """
            Returns a paginated list of functional-area catalog items for the company, filterable by
            `isActive` and free-text `q`. The owning company is validated against the authenticated
            tenant. List items omit `description` (detail-only); fetch the item by id for the full
            payload. Set `includeAllowedActions=true` to receive per-item read/manage flags.
            """)]
    public async Task<ActionResult<PagedResponse<FunctionalAreaCatalogListItemResponse>>> SearchFunctionalAreas(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery, Range(1, OrgStructureCatalogValidationRules.MaxPageSize)] int pageSize = OrgStructureCatalogValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchFunctionalAreasQuery(companyId, isActive, search, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("organization-structure-catalogs/functional-areas/{id:guid}/usage")]
    [ProducesResponseType<FunctionalAreaUsageResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a functional area's usage",
        Description = """
            Returns the active/inactive reference counts for the functional area across organization
            units, plus how many company preferences point at it. **The dashboard preference has no
            foreign key** — it references the area by `code` — so it does not appear in the database
            constraint graph, yet deleting the area would leave the HR-ratio metric pointing at a code
            that no longer exists. It is counted here for that reason.
            """)]
    public async Task<ActionResult<FunctionalAreaUsageResponse>> FunctionalAreaUsage(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetFunctionalAreaUsageQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("organization-structure-catalogs/functional-areas/{id:guid}")]
    [ProducesResponseType<FunctionalAreaCatalogItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a functional area by id",
        Description = """
            Returns a single functional-area catalog item by its public id. The owning company is
            resolved from the authenticated tenant; an item belonging to another tenant yields `404`. The
            `concurrencyToken` is emitted as the `ETag` header on mutations.
            """)]
    public async Task<ActionResult<FunctionalAreaCatalogItemResponse>> GetFunctionalAreaById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetFunctionalAreaByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("companies/{companyId:guid}/organization-structure-catalogs/functional-areas")]
    [ProducesResponseType<FunctionalAreaCatalogItemResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create a functional area",
        Description = """
            Creates a functional-area catalog item under the company and returns `201 Created` with the
            `Location` header pointing to the new resource and the `ETag` header carrying its initial
            `concurrencyToken`. A duplicate code yields `409`.
            """)]
    public async Task<ActionResult<FunctionalAreaCatalogItemResponse>> CreateFunctionalArea(
        Guid companyId,
        [FromBody] UpsertCatalogItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateFunctionalAreaCommand(companyId, request.Code, request.Name, request.Description, request.SortOrder),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetFunctionalAreaById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("organization-structure-catalogs/functional-areas/{id:guid}")]
    [ProducesResponseType<FunctionalAreaCatalogItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update a functional area",
        Description = """
            Replaces the editable fields (code, name, description, sort order) of a functional-area
            catalog item. Requires the current `concurrencyToken` in the `If-Match` header (missing →
            `400`, stale → `409`). A duplicate code yields `409`. The refreshed token is returned in the
            body and the `ETag` header.
            """)]
    public async Task<ActionResult<FunctionalAreaCatalogItemResponse>> UpdateFunctionalArea(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpsertCatalogItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateFunctionalAreaCommand(id, request.Code, request.Name, request.Description, request.SortOrder, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("organization-structure-catalogs/functional-areas/{id:guid}")]
    [ProducesResponseType<FunctionalAreaCatalogItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Delete a functional area",
        Description = """
            Hard-deletes the functional area. Requires the current `concurrencyToken` in the `If-Match`
            header (missing → `400`, stale → `409`). Rejected with
            `409 ORG_STRUCTURE_CATALOG_IN_USE_FOR_DELETE` when anything still references it —
            organization units, active **or** inactive since the foreign key is `RESTRICT`, and also
            the company dashboard preference, which points at the area by `code` without a foreign
            key. Use `GET /organization-structure-catalogs/functional-areas/{id}/usage` to see which
            references block it. Inactivation remains the way to retire an area that is in use; this
            exists for the record created by mistake that nothing ever used.
            """)]
    public async Task<ActionResult<FunctionalAreaCatalogItemResponse>> DeleteFunctionalArea(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(new DeleteFunctionalAreaCommand(id, concurrencyToken), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPatch("organization-structure-catalogs/functional-areas/{id:guid}/activate")]
    [ProducesResponseType<FunctionalAreaCatalogItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Activate a functional area",
        Description = """
            Reactivates an inactive functional-area catalog item. Requires the current `concurrencyToken`
            in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in
            the body and the `ETag` header.
            """)]
    public async Task<ActionResult<FunctionalAreaCatalogItemResponse>> ActivateFunctionalArea(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateFunctionalAreaCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("organization-structure-catalogs/functional-areas/{id:guid}/inactivate")]
    [ProducesResponseType<FunctionalAreaCatalogItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Inactivate a functional area",
        Description = """
            Deactivates (soft-delete) a functional-area catalog item. Fails with `409` if it is still in
            use by org units. Requires the current `concurrencyToken` in the `If-Match` header (missing →
            `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<FunctionalAreaCatalogItemResponse>> InactivateFunctionalArea(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateFunctionalAreaCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record UpsertCatalogItemRequest(
        string Code,
        string Name,
        string? Description,
        int SortOrder);
}
