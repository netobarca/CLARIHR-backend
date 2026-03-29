using CLARIHR.Api.Common;
using CLARIHR.Api.Authorization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.IdentityAccess.Contracts;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.IdentityAccess.Permissions;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Route("api/iam/permissions")]
public sealed class IamPermissionsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [AuthorizeResource("RBAC_PERMISSIONS", RbacPermissionAction.Create)]
    [HttpPost]
    [ProducesResponseType<IamPermissionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IamPermissionResponse>> Create(
        [FromBody] CreateIamPermissionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetById), new { publicId = result.Value.Id }, result.Value);
    }

    [AuthorizeResource("RBAC_PERMISSIONS", RbacPermissionAction.Read)]
    [HttpGet]
    [ProducesResponseType<PagedResponse<IamPermissionSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResponse<IamPermissionSummaryResponse>>> List(
        [FromQuery] ListIamPermissionsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }

    [AuthorizeResource("RBAC_PERMISSIONS", RbacPermissionAction.Read)]
    [HttpGet("{permissionId:guid}")]
    [ProducesResponseType<IamPermissionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IamPermissionResponse>> GetById(Guid permissionId, CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(new GetIamPermissionByIdQuery(permissionId), cancellationToken);
        return this.ToActionResult(result);
    }
}
