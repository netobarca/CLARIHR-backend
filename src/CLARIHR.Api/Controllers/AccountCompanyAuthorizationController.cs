using CLARIHR.Api.Authorization;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.IdentityAccess.Contracts;
using CLARIHR.Application.Features.IdentityAccess.Roles;
using CLARIHR.Application.Features.IdentityAccess.Users;
using CLARIHR.Application.Abstractions.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

// Single front-door to IAM Roles administration (no RolesController; the *IamRole* commands are consumed
// only here) plus the IamUser role-assignment sub-resource. Authorization is RBAC handler-gated via
// IIamAdministrationAuthorizationService.EnsureAuthorizedAsync (Roles/Permissions/Users screens), NOT a
// declarative policy — so this family is intentionally excluded from [AuthorizationPolicySet]/
// GovernedFamilyRegex (mirror AccountCompanies / AccountCompanySubscriptions): there is no single
// permission/policy pair to declare. It IS enrolled in the OpenAPI guardrail ("Account Authorization")
// so a dropped [Tags]/[SwaggerOperation] fails CI. The route is canonically versioned under
// `api/v1/account/...` and uses the company-context placeholder `{companyPublicId}`.
//
// Concurrency: IamRole carries a strong token (If-Match: "<guid>" on role writes); the user-roles
// sub-resource uses a weak computed ETag (If-Match: W/"<hash>") because iam_users has ~8 writers and a
// strong token there would be expensive. The user-roles endpoint overlaps CompanyUsers
// PATCH /rolePublicIds (a second door with a different authz model); the overlap is accepted by design.
[ApiController]
[Authorize]
[Route("api/v1/account/companies/{companyPublicId:guid}/authorization")]
[Tags("Account Authorization")]
[ResourceActions("RBAC_ROLES")]
public sealed class AccountCompanyAuthorizationController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    ITenantContext tenantContext) : ControllerBase
{
    [HttpGet("roles")]
    [ProducesResponseType<PagedResponse<AuthorizationRoleSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List authorization roles",
        Description = """
            Returns the tenant's IAM roles (paged, optionally filtered by `search`). Set
            `includeAllowedActions=true` to enrich each row with the caller's allowed actions
            (edit/delete) for client affordances. Requires `Roles:Read` (handler-gated RBAC);
            an unauthorized caller yields `403`.
            """)]
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
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Create an authorization role",
        Description = """
            Creates a custom IAM role with an optional initial grant set (`permissionIds`). Returns
            `201` with the created role; the current `concurrencyToken` is included in the body and the
            `ETag` header for use in a subsequent update. A duplicate name yields `409`. Requires
            `Roles:Create` (handler-gated RBAC).
            """)]
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
        Response.Headers[ETagHeader.HeaderName] = ETagHeader.Format(mapped.ConcurrencyToken);
        return CreatedAtAction(nameof(GetRoleById), new { companyPublicId, rolePublicId = mapped.Id }, mapped);
    }

    [HttpGet("roles/{rolePublicId:guid}")]
    [ProducesResponseType<AuthorizationRoleResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get an authorization role",
        Description = """
            Returns a single IAM role with its full grant set. The current `concurrencyToken` is
            included in the body and the `ETag` header for use in the `If-Match` header of a subsequent
            update/patch/grants change. Requires `Roles:Read` (handler-gated RBAC); a cross-tenant id
            yields `404`.
            """)]
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
        if (result.IsFailure)
        {
            return this.ToActionResult(Result<AuthorizationRoleResponse>.Failure(result.Error));
        }

        var mapped = MapRole(result.Value);
        Response.Headers[ETagHeader.HeaderName] = ETagHeader.Format(mapped.ConcurrencyToken);
        return Ok(mapped);
    }

    [HttpPut("roles/{rolePublicId:guid}")]
    [ProducesResponseType<AuthorizationRoleResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Update an authorization role",
        Description = """
            Replaces the role's editable fields (name, description). Requires the current
            `concurrencyToken` in the `If-Match` header (missing → `400`, stale →
            `409 CONCURRENCY_CONFLICT`); the refreshed token is returned in the body and the `ETag`
            header. System roles cannot be modified (`403`); a duplicate name yields `409`. Requires
            `Roles:Update` (handler-gated RBAC).
            """)]
    public async Task<ActionResult<AuthorizationRoleResponse>> UpdateRole(
        Guid companyPublicId,
        Guid rolePublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateAuthorizationRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (IsCompanyScopeMismatch(companyPublicId))
        {
            return Forbid();
        }

        var result = await commandDispatcher.SendAsync(
            new UpdateIamRoleCommand(rolePublicId, request.Name, request.Description, concurrencyToken),
            cancellationToken);

        return RoleResultWithETag(result);
    }

    [HttpPatch("roles/{rolePublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<AuthorizationRoleResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Patch an authorization role (RFC 6902 JSON Patch)",
        Description = """
            Applies a partial update using JSON Patch (RFC 6902, media type
            `application/json-patch+json`; the wire body is a bare array of operations). Patchable
            paths: `/name` (required, cannot be removed) and `/description` (or `null`/remove to clear
            it). Grant changes use the dedicated `/grants` endpoint, not this patch; system roles cannot
            be modified (`403`). Requires the current `concurrencyToken` in the `If-Match` header
            (missing → `400`, stale → `409 CONCURRENCY_CONFLICT`); the refreshed token is returned in
            the body and the `ETag` header. Requires `Roles:Update` (handler-gated RBAC).
            """)]
    public async Task<ActionResult<AuthorizationRoleResponse>> PatchRole(
        Guid companyPublicId,
        Guid rolePublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchAuthorizationRoleRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        if (IsCompanyScopeMismatch(companyPublicId))
        {
            return Forbid();
        }

        var result = await commandDispatcher.SendAsync(
            new PatchIamRoleCommand(
                rolePublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(
                    patchDoc,
                    static (op, path, from, value) => new IamRolePatchOperation(op, path, from, value))),
            cancellationToken);

        return RoleResultWithETag(result);
    }

    [HttpDelete("roles/{rolePublicId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Delete an authorization role",
        Description = """
            Permanently deletes a custom IAM role (hard delete — there is no soft-delete state). The
            delete is guarded: system roles cannot be deleted (`403`) and a role still assigned to users
            yields `409` (`iam.roles.in_use`). Returns `204`. Requires `Roles:Delete` (handler-gated
            RBAC).
            """)]
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
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get an authorization role's grants",
        Description = """
            Returns the grant (permission) set of a single IAM role. The role's current
            `concurrencyToken` is included in the body and the `ETag` header for use in the `If-Match`
            header of a subsequent grants change. Requires `Roles:Read` (handler-gated RBAC); a
            cross-tenant id yields `404`.
            """)]
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
        if (result.IsFailure)
        {
            return this.ToActionResult(Result<AuthorizationRoleGrantsResponse>.Failure(result.Error));
        }

        Response.Headers[ETagHeader.HeaderName] = ETagHeader.Format(result.Value.ConcurrencyToken);
        return Ok(MapRoleGrants(result.Value));
    }

    [HttpPut("roles/{rolePublicId:guid}/grants")]
    [ProducesResponseType<AuthorizationRoleGrantsResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Replace an authorization role's grants",
        Description = """
            Replaces the role's full grant (permission) set in one call. Enforces the
            "keep at least one RBAC security administrator" invariant (`409` if the change would remove
            the last one) and protects system roles (`403`). Requires the role's current
            `concurrencyToken` in the `If-Match` header (missing → `400`, stale →
            `409 CONCURRENCY_CONFLICT`); the refreshed token is returned in the body and the `ETag`
            header. Requires `Permissions:Update` (handler-gated RBAC).
            """)]
    public async Task<ActionResult<AuthorizationRoleGrantsResponse>> UpdateRoleGrants(
        Guid companyPublicId,
        Guid rolePublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateAuthorizationRoleGrantsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (IsCompanyScopeMismatch(companyPublicId))
        {
            return Forbid();
        }

        var result = await commandDispatcher.SendAsync(
            new SyncIamRolePermissionsCommand(rolePublicId, request.PermissionIds, concurrencyToken),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<AuthorizationRoleGrantsResponse>.Failure(result.Error));
        }

        Response.Headers[ETagHeader.HeaderName] = ETagHeader.Format(result.Value.ConcurrencyToken);
        return Ok(MapRoleGrants(result.Value));
    }

    [HttpPut("users/{userPublicId:guid}/roles")]
    [ProducesResponseType<AuthorizationUserRolesResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Replace a user's role assignments",
        Description = """
            Replaces the IAM user's full role set (a second door to role assignment; overlaps
            CompanyUsers `PATCH /rolePublicIds` with a different authz model — accepted by design).
            Enforces the "keep at least one active administrator" invariant (`409`). Concurrency uses a
            WEAK computed ETag: send the current `W/"<hash>"` in the `If-Match` header (missing → `400`,
            stale → `409 CONCURRENCY_CONFLICT`), or `*` for an unconditional write; the rotated weak
            token is returned in the body (`weakETag`) and the `ETag` header. Requires `Users:Update`
            (handler-gated RBAC).
            """)]
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

        if (!TryGetWeakIfMatch(out var expectedETag))
        {
            return ValidationProblem(ModelState);
        }

        var result = await commandDispatcher.SendAsync(
            new SyncIamUserRolesCommand(userPublicId, request.RoleIds, expectedETag),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<AuthorizationUserRolesResponse>.Failure(result.Error));
        }

        var mapped = MapUserRoles(result.Value);
        if (!string.IsNullOrEmpty(result.Value.WeakETag))
        {
            Response.Headers[ETagHeader.HeaderName] = ETagHeader.FormatWeak(result.Value.WeakETag);
        }

        return Ok(mapped);
    }

    private bool IsCompanyScopeMismatch(Guid companyPublicId) =>
        !tenantContext.TenantId.HasValue || tenantContext.TenantId.Value != companyPublicId;

    // Sets the strong ETag header from the role's rotated concurrency token and returns the mapped
    // role; on failure maps the error to its ProblemDetails.
    private ActionResult<AuthorizationRoleResponse> RoleResultWithETag(Result<IamRoleResponse> result)
    {
        if (result.IsFailure)
        {
            return this.ToActionResult(Result<AuthorizationRoleResponse>.Failure(result.Error));
        }

        var mapped = MapRole(result.Value);
        Response.Headers[ETagHeader.HeaderName] = ETagHeader.Format(mapped.ConcurrencyToken);
        return Ok(mapped);
    }

    // Extracts the weak If-Match value (`W/"hash"`, `"hash"` or `*`) as the opaque tag. On a
    // missing/empty header it records the standard model error (mirroring IfMatchModelBinder /
    // CompanyUsersController) and returns false, so the caller responds 400.
    private bool TryGetWeakIfMatch(out string? expectedETag)
    {
        expectedETag = null;

        var normalized = Request.Headers[IfMatchHeader.HeaderName].ToString().Trim();
        if (normalized.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[2..].Trim();
        }

        if (normalized.Length >= 2 && normalized.StartsWith('"') && normalized.EndsWith('"'))
        {
            normalized = normalized[1..^1];
        }

        if (string.IsNullOrWhiteSpace(normalized))
        {
            ModelState.TryAddModelError(IfMatchHeader.HeaderName, IfMatchHeader.MissingDetail);
            return false;
        }

        expectedETag = normalized;
        return true;
    }

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
            response.ConcurrencyToken,
            response.Permissions.Select(MapGrant).ToArray());

    private static AuthorizationRoleGrantsResponse MapRoleGrants(IamRoleResponse response) =>
        new(
            response.Id,
            response.Name,
            response.IsSystemRole,
            response.ConcurrencyToken,
            response.Permissions.Select(MapGrant).ToArray());

    private static AuthorizationUserRolesResponse MapUserRoles(IamUserResponse response) =>
        new(
            response.Id,
            response.Email,
            response.FirstName,
            response.LastName,
            response.IsActive,
            response.WeakETag,
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

    public sealed class CreateAuthorizationRoleRequest
    {
        public string Name { get; init; } = null!;

        public string? Description { get; init; }

        public IReadOnlyCollection<Guid>? PermissionIds { get; init; }
    }

    public sealed class UpdateAuthorizationRoleRequest
    {
        public string Name { get; init; } = null!;

        public string? Description { get; init; }
    }

    public sealed class PatchAuthorizationRoleRequest
    {
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
    }

    public sealed class UpdateAuthorizationRoleGrantsRequest
    {
        public IReadOnlyCollection<Guid> PermissionIds { get; init; } = null!;
    }

    public sealed class SyncAuthorizationUserRolesRequest
    {
        public IReadOnlyCollection<Guid> RoleIds { get; init; } = null!;
    }

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
        Guid ConcurrencyToken,
        IReadOnlyCollection<AuthorizationGrantResponse> Grants,
        AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

    public sealed record AuthorizationRoleGrantsResponse(
        Guid RoleId,
        string RoleName,
        bool IsSystemRole,
        Guid ConcurrencyToken,
        IReadOnlyCollection<AuthorizationGrantResponse> Grants,
        AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

    public sealed record AuthorizationUserRolesResponse(
        Guid UserId,
        string Email,
        string FirstName,
        string LastName,
        bool IsActive,
        string? WeakETag,
        IReadOnlyCollection<AuthorizationRoleReferenceResponse> Roles,
        AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;
}
