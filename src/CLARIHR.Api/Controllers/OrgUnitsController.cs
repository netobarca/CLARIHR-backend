using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.OrgUnits;
using CLARIHR.Domain.OrgUnits;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class OrgUnitsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/companies/{companyId:guid}/org-units")]
    [ProducesResponseType<PagedResponse<OrgUnitResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<OrgUnitResponse>>> Search(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery] OrgUnitType? type,
        [FromQuery] Guid? parentId,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchOrgUnitsQuery(companyId, isActive, q, type, parentId, page, pageSize),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/org-units/{id:guid}")]
    [ProducesResponseType<OrgUnitResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrgUnitResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetOrgUnitByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/org-units/tree")]
    [ProducesResponseType<IReadOnlyCollection<OrgUnitTreeNodeResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<OrgUnitTreeNodeResponse>>> Tree(
        Guid companyId,
        [FromQuery] Guid? rootId,
        [FromQuery] int? depth,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetOrgUnitTreeQuery(companyId, rootId, depth),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/org-units/graph")]
    [ProducesResponseType<OrgUnitGraphResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrgUnitGraphResponse>> Graph(
        Guid companyId,
        [FromQuery] Guid? rootId,
        [FromQuery] int? depth,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetOrgUnitGraphQuery(companyId, rootId, depth),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/companies/{companyId:guid}/org-units")]
    [ProducesResponseType<OrgUnitResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrgUnitResponse>> Create(
        Guid companyId,
        [FromBody] CreateOrgUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateOrgUnitCommand(
                companyId,
                request.Code,
                request.Name,
                request.UnitType,
                request.ParentId,
                request.SortOrder,
                request.Description,
                request.CostCenterCode,
                request.ManagerEmployeeId),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<OrgUnitResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/org-units/{id:guid}")]
    [ProducesResponseType<OrgUnitResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrgUnitResponse>> Update(
        Guid id,
        [FromBody] UpdateOrgUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateOrgUnitCommand(
                id,
                request.Code,
                request.Name,
                request.UnitType,
                request.SortOrder,
                request.Description,
                request.CostCenterCode,
                request.ManagerEmployeeId,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/org-units/{id:guid}/move")]
    [ProducesResponseType<OrgUnitResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrgUnitResponse>> Move(
        Guid id,
        [FromBody] MoveOrgUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new MoveOrgUnitCommand(id, request.NewParentId, request.SortOrder, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/org-units/{id:guid}/activate")]
    [ProducesResponseType<OrgUnitResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrgUnitResponse>> Activate(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateOrgUnitCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/org-units/{id:guid}/inactivate")]
    [ProducesResponseType<OrgUnitResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrgUnitResponse>> Inactivate(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateOrgUnitCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    public sealed record CreateOrgUnitRequest(
        string Code,
        string Name,
        OrgUnitType UnitType,
        Guid? ParentId,
        int? SortOrder,
        string? Description,
        string? CostCenterCode,
        Guid? ManagerEmployeeId);

    public sealed record UpdateOrgUnitRequest(
        string Code,
        string Name,
        OrgUnitType UnitType,
        int? SortOrder,
        string? Description,
        string? CostCenterCode,
        Guid? ManagerEmployeeId,
        Guid ConcurrencyToken);

    public sealed record MoveOrgUnitRequest(Guid? NewParentId, int? SortOrder, Guid ConcurrencyToken);

    public sealed record ConcurrencyRequest(Guid ConcurrencyToken);
}
