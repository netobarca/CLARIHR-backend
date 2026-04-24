using CLARIHR.Api.Common;
using CLARIHR.Api.Authorization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CompanyUsers;
using CLARIHR.Application.Features.IdentityAccess.Common;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Route("api/company/users")]
public sealed class CompanyUsersController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [AuthorizeResource("RBAC_USERS", RbacPermissionAction.Read)]
    [HttpGet]
    [ProducesResponseType<PagedResponse<CompanyUserSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<CompanyUserSummaryResponse>>> List(
        [FromQuery] GetCompanyUsersQuery query,
        CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }

    [AuthorizeResource("RBAC_USERS", RbacPermissionAction.Read)]
    [HttpGet("{userPublicId:guid}")]
    [ProducesResponseType<CompanyUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyUserResponse>> Get(
        Guid userPublicId,
        CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(new GetCompanyUserQuery(userPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [AuthorizeResource("RBAC_USERS", RbacPermissionAction.Create)]
    [HttpPost]
    [ProducesResponseType<CompanyUserInvitationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CompanyUserInvitationResponse>> Create(
        [FromBody] CreateCompanyUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateCompanyUserCommand(
                request.Email,
                request.FirstName,
                request.LastName,
                request.RolePublicIds),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [AuthorizeResource("RBAC_USERS", RbacPermissionAction.Update)]
    [HttpPut("{userId:guid}")]
    [ProducesResponseType<CompanyUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CompanyUserResponse>> Update(
        Guid userId,
        [FromBody] UpdateCompanyUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateCompanyUserCommand(
                userId,
                request.FirstName,
                request.LastName,
                request.RolePublicIds),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [AuthorizeResource("RBAC_USERS", RbacPermissionAction.Update)]
    [HttpPatch("{userId:guid}/deactivate")]
    [ProducesResponseType<CompanyUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CompanyUserResponse>> Deactivate(Guid userId, CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(new DeactivateCompanyUserCommand(userId), cancellationToken);
        return this.ToActionResult(result);
    }

    [AuthorizeResource("RBAC_USERS", RbacPermissionAction.Update)]
    [HttpPatch("{userId:guid}/reactivate")]
    [ProducesResponseType<CompanyUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CompanyUserResponse>> Reactivate(Guid userId, CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(new ReactivateCompanyUserCommand(userId), cancellationToken);
        return this.ToActionResult(result);
    }

    [AuthorizeResource("RBAC_USERS", RbacPermissionAction.Update)]
    [HttpPost("{userId:guid}/reset-invitation")]
    [ProducesResponseType<CompanyUserInvitationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CompanyUserInvitationResponse>> ResetInvitation(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(new ResetInvitationCommand(userId), cancellationToken);
        return this.ToActionResult(result);
    }

    public sealed record CreateCompanyUserRequest(
        string Email,
        string FirstName,
        string LastName,
        IReadOnlyCollection<Guid> RolePublicIds);

    public sealed record UpdateCompanyUserRequest(
        string FirstName,
        string LastName,
        IReadOnlyCollection<Guid> RolePublicIds);
}
