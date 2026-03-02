using CLARIHR.Api.Common;
using CLARIHR.Api.Authorization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.IdentityAccess.Contracts;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.IdentityAccess.Rbac;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Route("api/rbac")]
public sealed class RbacController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [AuthorizeResource("RBAC_PERMISSIONS", RbacPermissionAction.Read)]
    [HttpGet("resources")]
    [ProducesResponseType<RbacResourcesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RbacResourcesResponse>> GetResources(CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(new GetRbacResourcesQuery(), cancellationToken);
        return this.ToActionResult(result);
    }

    [AuthorizeResource("RBAC_PERMISSIONS", RbacPermissionAction.Read)]
    [HttpGet("resources/{resourceKey}/fields")]
    [ProducesResponseType<ResourceFieldsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ResourceFieldsResponse>> GetResourceFields(
        string resourceKey,
        CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(new GetResourceFieldsQuery(resourceKey), cancellationToken);
        return this.ToActionResult(result);
    }

    [AuthorizeResource("RBAC_PERMISSIONS", RbacPermissionAction.Read)]
    [HttpGet("roles/{roleId:guid}/permissions")]
    [ProducesResponseType<RbacRolePermissionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RbacRolePermissionsResponse>> GetRolePermissions(
        Guid roleId,
        CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(new GetRolePermissionsQuery(roleId), cancellationToken);
        return this.ToActionResult(result);
    }

    [AuthorizeResource("RBAC_PERMISSIONS", RbacPermissionAction.Update)]
    [HttpPut("roles/{roleId:guid}/permissions")]
    [ProducesResponseType<RbacRolePermissionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RbacRolePermissionsResponse>> UpsertRolePermissions(
        Guid roleId,
        [FromBody] UpsertRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new UpsertRolePermissionsCommand(
                roleId,
                request.Permissions
                    .Select(static permission => new RoleResourcePermissionUpdate(
                        permission.ResourceKey,
                        permission.HasAccess,
                        permission.CanRead,
                        permission.CanCreate,
                        permission.CanUpdate,
                        permission.CanDelete))
                    .ToArray()),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [AuthorizeResource("RBAC_PERMISSIONS", RbacPermissionAction.Read)]
    [HttpGet("roles/{roleId:guid}/field-permissions")]
    [ProducesResponseType<RoleFieldPermissionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RoleFieldPermissionsResponse>> GetRoleFieldPermissions(
        Guid roleId,
        [FromQuery] string resourceKey,
        CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(
            new GetRoleFieldPermissionsQuery(roleId, resourceKey),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [AuthorizeResource("RBAC_PERMISSIONS", RbacPermissionAction.Update)]
    [HttpPut("roles/{roleId:guid}/field-permissions")]
    [ProducesResponseType<RoleFieldPermissionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RoleFieldPermissionsResponse>> UpsertRoleFieldPermissions(
        Guid roleId,
        [FromBody] UpsertRoleFieldPermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new UpsertRoleFieldPermissionsCommand(
                roleId,
                request.ResourceKey,
                request.Fields
                    .Select(static field => new RoleFieldPermissionUpdate(
                        field.FieldKey,
                        field.IsVisible,
                        field.IsEditable,
                        field.IsRequired,
                        field.IsMasked))
                    .ToArray()),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [AuthorizeResource("RBAC_PERMISSIONS", RbacPermissionAction.Read)]
    [HttpGet("audit")]
    [ProducesResponseType<PagedResponse<RbacPermissionAuditEntryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResponse<RbacPermissionAuditEntryResponse>>> GetAudit(
        [FromQuery] Guid? roleId,
        [FromQuery] string? resourceKey,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPermissionAuditQuery(roleId, resourceKey, from, to, page, pageSize),
            cancellationToken);
        return this.ToActionResult(result);
    }

    public sealed record UpsertRolePermissionsRequest(
        IReadOnlyCollection<RolePermissionUpdateRequest> Permissions);

    public sealed record RolePermissionUpdateRequest(
        string ResourceKey,
        bool HasAccess,
        bool CanRead,
        bool CanCreate,
        bool CanUpdate,
        bool CanDelete);

    public sealed record UpsertRoleFieldPermissionsRequest(
        string ResourceKey,
        IReadOnlyCollection<RoleFieldPermissionUpdateRequest> Fields);

    public sealed record RoleFieldPermissionUpdateRequest(
        string FieldKey,
        bool IsVisible,
        bool IsEditable,
        bool IsRequired = false,
        bool IsMasked = false);
}
