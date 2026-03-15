using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Locations.Bootstrap;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class LocationsController(ICommandDispatcher commandDispatcher) : ControllerBase
{
    [HttpPost("api/v1/companies/{companyId:guid}/locations/bootstrap-tree")]
    [ProducesResponseType<LocationBootstrapTreeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LocationBootstrapTreeResponse>> BootstrapTree(
        Guid companyId,
        [FromBody] BootstrapLocationTreeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new BootstrapLocationTreeCommand(companyId, request.Root is null ? null : MapNode(request.Root)),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<LocationBootstrapTreeResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    private static LocationBootstrapTreeNodeInput MapNode(BootstrapLocationTreeNodeRequest request) =>
        new(
            request.Code,
            request.Name,
            request.Description,
            (request.Children ?? []).Select(MapNode).ToArray());

    public sealed record BootstrapLocationTreeRequest(BootstrapLocationTreeNodeRequest? Root);

    public sealed record BootstrapLocationTreeNodeRequest(
        string Code,
        string Name,
        string? Description,
        IReadOnlyCollection<BootstrapLocationTreeNodeRequest>? Children);
}
