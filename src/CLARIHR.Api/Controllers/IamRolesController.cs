using CLARIHR.Api.Common;
using CLARIHR.Api.Authorization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.IdentityAccess.Contracts;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.IdentityAccess.Roles;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Route("api/iam/roles")]
public sealed class IamRolesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [AuthorizeResource("RBAC_ROLES", RbacPermissionAction.Create)]
    [HttpPost]
    [ProducesResponseType<IamRoleResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IamRoleResponse>> Create(
        [FromBody] CreateIamRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateIamRoleCommand(request.Name, request.Description, request.PermissionPublicIds),
            cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetById), new { publicId = result.Value.Id }, result.Value);
    }

    [AuthorizeResource("RBAC_ROLES", RbacPermissionAction.Read)]
    [HttpGet]
    [ProducesResponseType<PagedResponse<IamRoleSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResponse<IamRoleSummaryResponse>>> List(
        [FromQuery] ListIamRolesQuery query,
        CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }

    [AuthorizeResource("RBAC_ROLES", RbacPermissionAction.Read)]
    [HttpGet("{roleId:guid}")]
    [ProducesResponseType<IamRoleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IamRoleResponse>> GetById(Guid roleId, CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(new GetIamRoleByIdQuery(roleId), cancellationToken);
        return this.ToActionResult(result);
    }

    [AuthorizeResource("RBAC_ROLES", RbacPermissionAction.Update)]
    [HttpPut("{roleId:guid}")]
    [ProducesResponseType<IamRoleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IamRoleResponse>> Update(
        Guid roleId,
        [FromBody] UpdateIamRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateIamRoleCommand(roleId, request.Name, request.Description),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [AuthorizeResource("RBAC_ROLES", RbacPermissionAction.Create)]
    [HttpPost("{roleId:guid}/clone")]
    [ProducesResponseType<IamRoleResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IamRoleResponse>> Clone(
        Guid roleId,
        [FromBody] CloneIamRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new CloneIamRoleCommand(roleId, request.Name, request.Description),
            cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetById), new { publicId = result.Value.Id }, result.Value);
    }

    [AuthorizeResource("RBAC_ROLES", RbacPermissionAction.Read)]
    [HttpGet("{roleId:guid}/permission-matrix")]
    [ProducesResponseType<RolePermissionMatrixResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RolePermissionMatrixResponse>> GetPermissionMatrix(
        Guid roleId,
        CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(new GetRolePermissionMatrixQuery(roleId), cancellationToken);
        return this.ToActionResult(result);
    }

    [AuthorizeResource("RBAC_ROLES", RbacPermissionAction.Update)]
    [HttpPut("{roleId:guid}/permission-matrix")]
    [ProducesResponseType<RolePermissionMatrixResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RolePermissionMatrixResponse>> UpdatePermissionMatrix(
        Guid roleId,
        [FromBody] UpdateRolePermissionMatrixRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateRolePermissionMatrixCommand(
                roleId,
                request.Screens.Select(static screen => new RolePermissionMatrixScreenUpdate(
                    screen.Screen,
                    screen.Access,
                    screen.Read,
                    screen.Create,
                    screen.Update,
                    screen.Delete))
                    .ToArray()),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [AuthorizeResource("RBAC_ROLES", RbacPermissionAction.Delete)]
    [HttpDelete("{roleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(Guid roleId, CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(new DeleteIamRoleCommand(roleId), cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result).Result!;
        }

        return NoContent();
    }

    [AuthorizeResource("RBAC_ROLES", RbacPermissionAction.Update)]
    [HttpPut("{roleId:guid}/permissions")]
    [ProducesResponseType<IamRoleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IamRoleResponse>> SyncPermissions(
        Guid roleId,
        [FromBody] SyncIamRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new SyncIamRolePermissionsCommand(roleId, request.PermissionPublicIds),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [AuthorizeResource("RBAC_ROLES", RbacPermissionAction.Update)]
    [HttpPut("{roleId:guid}/users")]
    [ProducesResponseType<IamRoleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IamRoleResponse>> SyncUsers(
        Guid roleId,
        [FromBody] SyncIamRoleUsersRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new SyncIamRoleUsersCommand(roleId, request.UserPublicIds),
            cancellationToken);

        return this.ToActionResult(result);
    }

    public sealed record CreateIamRoleRequest(
        string Name,
        string? Description,
        IReadOnlyCollection<Guid>? PermissionPublicIds = null);

    public sealed record UpdateIamRoleRequest(string Name, string? Description);

    public sealed record CloneIamRoleRequest(string? Name, string? Description);

    public sealed record UpdateRolePermissionMatrixRequest(
        IReadOnlyCollection<RolePermissionMatrixScreenRequest> Screens);

    public sealed record RolePermissionMatrixScreenRequest(
        string Screen,
        bool Access,
        bool Read,
        bool Create,
        bool Update,
        bool Delete);

    public sealed record SyncIamRolePermissionsRequest(IReadOnlyCollection<Guid> PermissionPublicIds);

    public sealed record SyncIamRoleUsersRequest(IReadOnlyCollection<Guid> UserPublicIds);
}
