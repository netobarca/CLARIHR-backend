using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Locations.WorkCenters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class WorkCentersController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/companies/{companyId:guid}/work-centers")]
    [ProducesResponseType<PagedResponse<WorkCenterResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
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

    [HttpGet("api/v1/work-centers/{id:guid}")]
    [ProducesResponseType<WorkCenterResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkCenterResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetWorkCenterByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/companies/{companyId:guid}/work-centers")]
    [ProducesResponseType<WorkCenterResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
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

        return result.IsFailure
            ? this.ToActionResult(Result<WorkCenterResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/work-centers/{id:guid}")]
    [ProducesResponseType<WorkCenterResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkCenterResponse>> Update(
        Guid id,
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
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/work-centers/{id:guid}/reassign-group")]
    [ProducesResponseType<WorkCenterResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkCenterResponse>> ReassignGroup(
        Guid id,
        [FromBody] ReassignWorkCenterGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReassignWorkCenterGroupCommand(id, request.LocationGroupPublicId, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/work-centers/{id:guid}/activate")]
    [ProducesResponseType<WorkCenterResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkCenterResponse>> Activate(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateWorkCenterCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/work-centers/{id:guid}/inactivate")]
    [ProducesResponseType<WorkCenterResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkCenterResponse>> Inactivate(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateWorkCenterCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
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
        string? Notes,
        Guid ConcurrencyToken);

    public sealed record ReassignWorkCenterGroupRequest(Guid LocationGroupPublicId, Guid ConcurrencyToken);

    public sealed record ConcurrencyRequest(Guid ConcurrencyToken);
}
