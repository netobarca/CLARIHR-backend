using Asp.Versioning;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.Locations.Hierarchy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}")]
[Tags("Location Levels")]
[AuthorizationPolicySet(LocationPolicies.Read, LocationPolicies.Manage)]
public sealed class LocationLevelsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/location-levels")]
    [ProducesResponseType<IReadOnlyCollection<LocationLevelResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List location levels for a company",
        Description = """
            Returns the ordered list of location levels for the company (level order, display
            name, and the `isActive`/`isRequired`/`allowsWorkCenters` flags). The owning company
            is validated against the authenticated tenant.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<LocationLevelResponse>>> List(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetLocationLevelsQuery(companyId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("location-levels/{id:guid}")]
    [ProducesResponseType<LocationLevelResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a location level by id",
        Description = """
            Returns a single location level by its public id. The owning company is resolved from
            the authenticated tenant; a level belonging to another tenant yields `404`. The
            `concurrencyToken` is emitted as the `ETag` header on mutations.
            """)]
    public async Task<ActionResult<LocationLevelResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetLocationLevelByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("companies/{companyId:guid}/location-levels")]
    [ProducesResponseType<LocationLevelResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create a location level",
        Description = """
            Creates a location level at the given `levelOrder` and returns `201 Created` with the
            `Location` header pointing to the new resource and the `ETag` header carrying its
            initial `concurrencyToken`. The flags `isRequired`/`allowsWorkCenters` must be
            consistent with `isActive`, and `allowsWorkCenters` is only valid on the last active
            level. A duplicate level order yields `409`.
            """)]
    public async Task<ActionResult<LocationLevelResponse>> Create(
        Guid companyId,
        [FromBody] CreateLocationLevelRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateLocationLevelCommand(
                companyId,
                request.LevelOrder,
                request.DisplayName,
                request.IsActive,
                request.IsRequired,
                request.AllowsWorkCenters),
            cancellationToken);

        // The PublicContractRouteConvention rewrites the GetById route token `{id}` to
        // `{publicId}`, so the Location route value MUST be keyed `publicId` (not `id`).
        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("location-levels/{id:guid}")]
    [ProducesResponseType<LocationLevelResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update a location level",
        Description = """
            Reconfigures a location level (display name and the interdependent
            `isActive`/`isRequired`/`allowsWorkCenters` flags, validated as a unit). The level
            order is immutable. Requires the current `concurrencyToken` in the `If-Match` header
            (missing → `400`, stale → `409`). Flag-rule violations yield `409`. The refreshed token
            is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<LocationLevelResponse>> Update(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateLocationLevelRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateLocationLevelCommand(
                id,
                request.DisplayName,
                request.IsActive,
                request.IsRequired,
                request.AllowsWorkCenters,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("location-levels/{id:guid}/activate")]
    [ProducesResponseType<LocationLevelResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Activate a location level",
        Description = """
            Reactivates an inactive location level. A level that allows work centers can only be
            active as the last active level (`409` otherwise). Requires the current
            `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The
            refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<LocationLevelResponse>> Activate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateLocationLevelCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("location-levels/{id:guid}/inactivate")]
    [ProducesResponseType<LocationLevelResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Inactivate a location level",
        Description = """
            Deactivates a location level. Fails with `409` if it is required, if it is the last
            active level, or if it still has active groups. Requires the current `concurrencyToken`
            in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is
            returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<LocationLevelResponse>> Inactivate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateLocationLevelCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record CreateLocationLevelRequest(
        int LevelOrder,
        string DisplayName,
        bool IsActive,
        bool IsRequired,
        bool AllowsWorkCenters);

    public sealed record UpdateLocationLevelRequest(
        string DisplayName,
        bool IsActive,
        bool IsRequired,
        bool AllowsWorkCenters);
}
