using CLARIHR.Api.Common;
using CLARIHR.Api.Authorization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.IdentityAccess.Contracts;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.IdentityAccess.Users;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Route("api/iam/users")]
public sealed class IamUsersController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [AuthorizeResource("RBAC_USERS", RbacPermissionAction.Create)]
    [HttpPost]
    [ProducesResponseType<IamUserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IamUserResponse>> Create(
        [FromBody] CreateIamUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateIamUserCommand(
                request.FirstName,
                request.LastName,
                request.Email,
                request.IsActive,
                request.RolePublicIds),
            cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetById), new { userPublicId = result.Value.Id }, result.Value);
    }

    [AuthorizeResource("RBAC_USERS", RbacPermissionAction.Read)]
    [HttpGet]
    [ProducesResponseType<PagedResponse<IamUserSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResponse<IamUserSummaryResponse>>> List(
        [FromQuery] ListIamUsersQuery query,
        CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }

    [AuthorizeResource("RBAC_USERS", RbacPermissionAction.Read)]
    [HttpGet("{userId:guid}")]
    [ProducesResponseType<IamUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IamUserResponse>> GetById(Guid userId, CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(new GetIamUserByIdQuery(userId), cancellationToken);
        return this.ToActionResult(result);
    }

    [AuthorizeResource("RBAC_USERS", RbacPermissionAction.Update)]
    [HttpPut("{userId:guid}/roles")]
    [ProducesResponseType<IamUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IamUserResponse>> SyncRoles(
        Guid userId,
        [FromBody] SyncIamUserRolesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new SyncIamUserRolesCommand(userId, request.RolePublicIds),
            cancellationToken);

        return this.ToActionResult(result);
    }

    public sealed record CreateIamUserRequest(
        string FirstName,
        string LastName,
        string Email,
        bool IsActive = true,
        IReadOnlyCollection<Guid>? RolePublicIds = null);

    public sealed record SyncIamUserRolesRequest(IReadOnlyCollection<Guid> RolePublicIds);
}
