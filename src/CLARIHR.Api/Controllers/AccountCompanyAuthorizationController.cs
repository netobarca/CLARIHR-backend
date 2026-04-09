using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.AccountCompanies;
using CLARIHR.Application.Features.IdentityAccess.Contracts;
using CLARIHR.Application.Features.IdentityAccess.Roles;
using CLARIHR.Application.Features.IdentityAccess.Users;
using CLARIHR.Application.Abstractions.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/account/companies/{companyPublicId:guid}/authorization")]
public sealed class AccountCompanyAuthorizationController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    ITenantContext tenantContext) : ControllerBase
{
    [HttpGet("roles")]
    [ProducesResponseType<PagedResponse<AuthorizationRoleSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<AuthorizationRoleSummaryResponse>>> ListRoles(
        Guid companyPublicId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        if (IsCompanyScopeMismatch(companyPublicId))
        {
            return Forbid();
        }

        var result = await queryDispatcher.SendAsync(
            new ListIamRolesQuery(pageNumber, pageSize, search, includeAllowedActions),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<PagedResponse<AuthorizationRoleSummaryResponse>>.Failure(result.Error));
        }

        var mapped = new PagedResponse<AuthorizationRoleSummaryResponse>(
            result.Value.Items.Select(MapRoleSummary).ToArray(),
            result.Value.PageNumber,
            result.Value.PageSize,
            result.Value.TotalCount);

        return Ok(mapped);
    }

    [HttpPost("roles")]
    [ProducesResponseType<AuthorizationRoleResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthorizationRoleResponse>> CreateRole(
        Guid companyPublicId,
        [FromBody] CreateAuthorizationRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (IsCompanyScopeMismatch(companyPublicId))
        {
            return Forbid();
        }

        var result = await commandDispatcher.SendAsync(
            new CreateIamRoleCommand(request.Name, request.Description, request.PermissionIds),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<AuthorizationRoleResponse>.Failure(result.Error));
        }

        var mapped = MapRole(result.Value);
        return CreatedAtAction(nameof(GetRoleById), new { companyPublicId, rolePublicId = mapped.Id }, mapped);
    }

    [HttpGet("roles/{rolePublicId:guid}")]
    [ProducesResponseType<AuthorizationRoleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthorizationRoleResponse>> GetRoleById(
        Guid companyPublicId,
        Guid rolePublicId,
        CancellationToken cancellationToken = default)
    {
        if (IsCompanyScopeMismatch(companyPublicId))
        {
            return Forbid();
        }

        var result = await queryDispatcher.SendAsync(new GetIamRoleByIdQuery(rolePublicId), cancellationToken);
        return result.IsFailure
            ? this.ToActionResult(Result<AuthorizationRoleResponse>.Failure(result.Error))
            : Ok(MapRole(result.Value));
    }

    [HttpPut("roles/{rolePublicId:guid}")]
    [ProducesResponseType<AuthorizationRoleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthorizationRoleResponse>> UpdateRole(
        Guid companyPublicId,
        Guid rolePublicId,
        [FromBody] UpdateAuthorizationRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (IsCompanyScopeMismatch(companyPublicId))
        {
            return Forbid();
        }

        var result = await commandDispatcher.SendAsync(
            new UpdateIamRoleCommand(rolePublicId, request.Name, request.Description),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<AuthorizationRoleResponse>.Failure(result.Error))
            : Ok(MapRole(result.Value));
    }

    [HttpDelete("roles/{rolePublicId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteRole(
        Guid companyPublicId,
        Guid rolePublicId,
        CancellationToken cancellationToken = default)
    {
        if (IsCompanyScopeMismatch(companyPublicId))
        {
            return Forbid();
        }

        var result = await commandDispatcher.SendAsync(new DeleteIamRoleCommand(rolePublicId), cancellationToken);
        return result.IsFailure
            ? this.ToActionResult(result).Result!
            : NoContent();
    }

    [HttpGet("roles/{rolePublicId:guid}/grants")]
    [ProducesResponseType<AuthorizationRoleGrantsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthorizationRoleGrantsResponse>> GetRoleGrants(
        Guid companyPublicId,
        Guid rolePublicId,
        CancellationToken cancellationToken = default)
    {
        if (IsCompanyScopeMismatch(companyPublicId))
        {
            return Forbid();
        }

        var result = await queryDispatcher.SendAsync(new GetIamRoleByIdQuery(rolePublicId), cancellationToken);
        return result.IsFailure
            ? this.ToActionResult(Result<AuthorizationRoleGrantsResponse>.Failure(result.Error))
            : Ok(MapRoleGrants(result.Value));
    }

    [HttpPut("roles/{rolePublicId:guid}/grants")]
    [ProducesResponseType<AuthorizationRoleGrantsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthorizationRoleGrantsResponse>> UpdateRoleGrants(
        Guid companyPublicId,
        Guid rolePublicId,
        [FromBody] UpdateAuthorizationRoleGrantsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (IsCompanyScopeMismatch(companyPublicId))
        {
            return Forbid();
        }

        var result = await commandDispatcher.SendAsync(
            new SyncIamRolePermissionsCommand(rolePublicId, request.PermissionIds),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<AuthorizationRoleGrantsResponse>.Failure(result.Error))
            : Ok(MapRoleGrants(result.Value));
    }

    [HttpPut("users/{userPublicId:guid}/roles")]
    [ProducesResponseType<AuthorizationUserRolesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthorizationUserRolesResponse>> SyncUserRoles(
        Guid companyPublicId,
        Guid userPublicId,
        [FromBody] SyncAuthorizationUserRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        if (IsCompanyScopeMismatch(companyPublicId))
        {
            return Forbid();
        }

        var result = await commandDispatcher.SendAsync(
            new SyncIamUserRolesCommand(userPublicId, request.RoleIds),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<AuthorizationUserRolesResponse>.Failure(result.Error))
            : Ok(MapUserRoles(result.Value));
    }

    private bool IsCompanyScopeMismatch(Guid companyPublicId) =>
        !tenantContext.TenantId.HasValue || tenantContext.TenantId.Value != companyPublicId;

    private static AuthorizationRoleSummaryResponse MapRoleSummary(IamRoleSummaryResponse response) =>
        new(
            response.Id,
            response.Name,
            response.Description,
            response.IsSystemRole,
            response.PermissionCount,
            response.UserCount);

    private static AuthorizationRoleResponse MapRole(IamRoleResponse response) =>
        new(
            response.Id,
            response.Name,
            response.Description,
            response.IsSystemRole,
            response.UserCount,
            response.Permissions.Select(MapGrant).ToArray());

    private static AuthorizationRoleGrantsResponse MapRoleGrants(IamRoleResponse response) =>
        new(
            response.Id,
            response.Name,
            response.IsSystemRole,
            response.Permissions.Select(MapGrant).ToArray());

    private static AuthorizationUserRolesResponse MapUserRoles(IamUserResponse response) =>
        new(
            response.Id,
            response.Email,
            response.FirstName,
            response.LastName,
            response.IsActive,
            response.Roles.Select(role => new AuthorizationRoleReferenceResponse(
                role.Id,
                role.Name,
                role.Description,
                role.IsSystemRole)).ToArray());

    private static AuthorizationGrantResponse MapGrant(IamPermissionReferenceResponse permission) =>
        new(
            permission.Id,
            permission.Code,
            permission.Name,
            permission.Description,
            permission.Module,
            permission.Screen,
            permission.Kind.ToString(),
            permission.Action,
            permission.FieldName,
            permission.FieldAccess?.ToString());

    public sealed record CreateAuthorizationRoleRequest(
        string Name,
        string? Description,
        IReadOnlyCollection<Guid>? PermissionIds = null);

    public sealed record UpdateAuthorizationRoleRequest(string Name, string? Description);

    public sealed record UpdateAuthorizationRoleGrantsRequest(IReadOnlyCollection<Guid> PermissionIds);

    public sealed record SyncAuthorizationUserRolesRequest(IReadOnlyCollection<Guid> RoleIds);

    public sealed record AuthorizationRoleSummaryResponse(
        Guid Id,
        string Name,
        string? Description,
        bool IsSystemRole,
        int GrantCount,
        int UserCount);

    public sealed record AuthorizationRoleReferenceResponse(
        Guid Id,
        string Name,
        string? Description,
        bool IsSystemRole);

    public sealed record AuthorizationGrantResponse(
        Guid Id,
        string Code,
        string Name,
        string? Description,
        string Module,
        string ResourceKey,
        string Kind,
        string? Action,
        string? FieldName,
        string? FieldAccessState);

    public sealed record AuthorizationRoleResponse(
        Guid Id,
        string Name,
        string? Description,
        bool IsSystemRole,
        int UserCount,
        IReadOnlyCollection<AuthorizationGrantResponse> Grants);

    public sealed record AuthorizationRoleGrantsResponse(
        Guid RoleId,
        string RoleName,
        bool IsSystemRole,
        IReadOnlyCollection<AuthorizationGrantResponse> Grants);

    public sealed record AuthorizationUserRolesResponse(
        Guid UserId,
        string Email,
        string FirstName,
        string LastName,
        bool IsActive,
        IReadOnlyCollection<AuthorizationRoleReferenceResponse> Roles);
}
