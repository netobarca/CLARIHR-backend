using Asp.Versioning;
using CLARIHR.Api.Authorization;
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
[Route("api/v{version:apiVersion}/companies/{companyId:guid}/location-hierarchy")]
[Tags("Location Hierarchy")]
[AuthorizationPolicySet(LocationPolicies.Read, LocationPolicies.Manage)]
[ResourceActions("LOCATION_HIERARCHY")]
public sealed class LocationHierarchyController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<LocationHierarchyConfigResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get the location hierarchy configuration",
        Description = """
            Returns the per-company location hierarchy configuration (whether the hierarchy is
            multi-level and the default group code/name). The owning company is validated against
            the authenticated tenant; a company without a configuration yields `404`.
            """)]
    public async Task<ActionResult<LocationHierarchyConfigResponse>> Get(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetLocationHierarchyQuery(companyId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut]
    [ProducesResponseType<LocationHierarchyConfigResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update the location hierarchy configuration",
        Description = """
            Updates the per-company location hierarchy configuration (the `isMultiLevel` flag).
            Switching to single-level requires exactly one active level, otherwise `409`. Requires
            the current `concurrencyToken` in the `If-Match` header (a missing/malformed header
            yields `400` and a stale token yields `409 CONCURRENCY_CONFLICT`). The refreshed token
            is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<LocationHierarchyConfigResponse>> Update(
        Guid companyId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateLocationHierarchyConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateLocationHierarchyConfigCommand(
                companyId,
                request.IsMultiLevel,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record UpdateLocationHierarchyConfigRequest(bool IsMultiLevel);
}
