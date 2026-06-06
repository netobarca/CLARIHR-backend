using System.ComponentModel.DataAnnotations;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Api.Authorization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CompanyUsers;
using CLARIHR.Application.Features.CompanyUsers.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

// Authorization is handler-gated via RBAC field-level permissions ([AuthorizeResource("RBAC_USERS", ...)]
// + ICompanyUserAuthorizationService + IFieldPermissionService), NOT the declarative Read/Manage policy
// pair, so this family is intentionally excluded from [AuthorizationPolicySet]/GovernedFamilyRegex.
// Concurrency: the resource is a read projection over three aggregates (User/auth_users +
// UserCompanyMembership + IamUser) with NO persisted token, so it uses a WEAK computed ETag — a
// deterministic hash of the projection (CompanyUserETag) advertised as `W/"..."`. Mutations require the
// current value in `If-Match` (missing → 400, stale → 409 CONCURRENCY_CONFLICT, per the app-wide
// convention — no 412/428). The canonical version segment is carried literally in the route
// (`api/v1/company/users`); the tenant/company is resolved implicitly from the active token.
[ApiController]
[Route("api/v1/company/users")]
[Consumes("application/json")]
[Produces("application/json")]
[Tags("Company Users")]
public sealed class CompanyUsersController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [AuthorizeResource("RBAC_USERS", RbacPermissionAction.Read)]
    [HttpGet]
    [EnableRateLimiting(CompanyUserRateLimitPolicies.Search)]
    [ProducesResponseType<PagedResponse<CompanyUserSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List company users",
        Description = """
            Returns a paginated list of the users of the active company, already
            filtered by the caller's field-level permissions. Use `page`,
            `pageSize`, `status`, `roleId` and `search` to navigate and filter.
            """)]
    public async Task<ActionResult<PagedResponse<CompanyUserSummaryResponse>>> List(
        [FromQuery] UserStatus? status,
        [FromQuery] Guid? roleId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [Range(1, CompanyUserValidationRules.MaxPageSize)]
        [FromQuery] int pageSize = CompanyUserValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetCompanyUsersQuery(page, pageSize, status, roleId, search, includeAllowedActions),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [AuthorizeResource("RBAC_USERS", RbacPermissionAction.Read)]
    [HttpGet("{userId:guid}")]
    [ProducesResponseType<CompanyUserResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a company user by id",
        Description = """
            Returns a single user of the active company. The response carries a
            weak `ETag` header (`W/"<hash>"`) — send it in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`/deactivate`/`/reactivate` requests to guard
            against lost updates.
            """)]
    public async Task<ActionResult<CompanyUserResponse>> Get(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(new GetCompanyUserQuery(userId), cancellationToken);
        return this.ToActionResultWithWeakETag(result, value => value.WeakETag);
    }

    [AuthorizeResource("RBAC_USERS", RbacPermissionAction.Create)]
    [HttpPost]
    [EnableRateLimiting(CompanyUserRateLimitPolicies.Invite)]
    [ProducesResponseType<CompanyUserInvitationResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Invite a user to the company",
        Description = """
            Invites a user to the active company and assigns the initial roles.
            Returns `201 Created` with the created user wrapped in a
            `CompanyUserInvitationResponse`; the `Location` header points to the
            user and the `ETag` header carries its initial weak token. Reuses an
            existing local user when the e-mail already exists.
            """)]
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

        return this.ToCreatedAtActionResultWithWeakETag(
            result,
            nameof(Get),
            value => new { userPublicId = value.User.Id },
            value => value.User.WeakETag);
    }

    [AuthorizeResource("RBAC_USERS", RbacPermissionAction.Update)]
    [HttpPut("{userId:guid}")]
    [ProducesResponseType<CompanyUserResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Update a company user",
        Description = """
            Replaces the editable fields (first name, last name) and the assigned
            roles of a user in the active company. Requires the current weak token
            in the `If-Match` header (missing → `400`, stale → `409`); the rotated
            token is returned in the `ETag` header. Field-level authorization is
            evaluated only for the fields that actually change.
            """)]
    public async Task<ActionResult<CompanyUserResponse>> Update(
        Guid userId,
        [FromBody] UpdateCompanyUserRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetWeakIfMatch(out var expectedETag))
        {
            return ValidationProblem(ModelState);
        }

        var result = await commandDispatcher.SendAsync(
            new UpdateCompanyUserCommand(
                userId,
                request.FirstName,
                request.LastName,
                request.RolePublicIds,
                expectedETag),
            cancellationToken);

        return this.ToActionResultWithWeakETag(result, value => value.WeakETag);
    }

    // Resolves the partial change onto the current projection and reuses the PUT mutation path
    // (PatchCompanyUserCommand → UpdateCompanyUserCommand) so PUT and PATCH cannot drift.
    [AuthorizeResource("RBAC_USERS", RbacPermissionAction.Update)]
    [HttpPatch("{userId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<CompanyUserResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Patch a company user (RFC 6902 JSON Patch)",
        Description = """
            Applies a partial update using JSON Patch (RFC 6902, media type
            `application/json-patch+json`; the wire body is a bare array of
            operations). Patchable paths: `/firstName`, `/lastName`,
            `/rolePublicIds`. Activation state uses the `/deactivate` and
            `/reactivate` actions; the e-mail address is immutable. Requires the
            current weak token in the `If-Match` header (missing → `400`, stale →
            `409`); the rotated token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<CompanyUserResponse>> Patch(
        Guid userId,
        [FromBody] JsonPatchDocument<PatchCompanyUserRequest> patchDoc,
        CancellationToken cancellationToken)
    {
        if (!TryGetWeakIfMatch(out var expectedETag))
        {
            return ValidationProblem(ModelState);
        }

        var command = new PatchCompanyUserCommand(
            userId,
            JsonPatchOperationMapper.Map(
                patchDoc,
                static (op, path, from, value) => new CompanyUserPatchOperation(op, path, from, value)),
            expectedETag);

        var result = await commandDispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResultWithWeakETag(result, value => value.WeakETag);
    }

    [AuthorizeResource("RBAC_USERS", RbacPermissionAction.Update)]
    [HttpPatch("{userId:guid}/deactivate")]
    [ProducesResponseType<CompanyUserResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Deactivate a company user",
        Description = """
            Deactivates the user in the active company (functional user, membership
            and linked IAM user) and revokes their refresh tokens. Enforces the
            "at least one active administrator" invariant. Requires the current weak
            token in the `If-Match` header (missing → `400`, stale → `409`); the
            rotated token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<CompanyUserResponse>> Deactivate(Guid userId, CancellationToken cancellationToken)
    {
        if (!TryGetWeakIfMatch(out var expectedETag))
        {
            return ValidationProblem(ModelState);
        }

        var result = await commandDispatcher.SendAsync(new DeactivateCompanyUserCommand(userId, expectedETag), cancellationToken);
        return this.ToActionResultWithWeakETag(result, value => value.WeakETag);
    }

    [AuthorizeResource("RBAC_USERS", RbacPermissionAction.Update)]
    [HttpPatch("{userId:guid}/reactivate")]
    [ProducesResponseType<CompanyUserResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Reactivate a company user",
        Description = """
            Reactivates the user's membership in the active company and re-enables
            the linked IAM user when the functional user becomes `Active`. Requires
            the current weak token in the `If-Match` header (missing → `400`, stale
            → `409`); the rotated token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<CompanyUserResponse>> Reactivate(Guid userId, CancellationToken cancellationToken)
    {
        if (!TryGetWeakIfMatch(out var expectedETag))
        {
            return ValidationProblem(ModelState);
        }

        var result = await commandDispatcher.SendAsync(new ReactivateCompanyUserCommand(userId, expectedETag), cancellationToken);
        return this.ToActionResultWithWeakETag(result, value => value.WeakETag);
    }

    [AuthorizeResource("RBAC_USERS", RbacPermissionAction.Update)]
    [HttpPost("{userId:guid}/reset-invitation")]
    [EnableRateLimiting(CompanyUserRateLimitPolicies.Invite)]
    [ProducesResponseType<CompanyUserInvitationResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Resend a company user invitation",
        Description = """
            Issues a fresh invitation for a pending/resendable local user: revokes
            previous invitation tokens, emits a new one and sends the e-mail.
            Returns `200 OK` with the `CompanyUserInvitationResponse`. Does not
            require `If-Match` (it does not mutate the projected user fields).
            """)]
    public async Task<ActionResult<CompanyUserInvitationResponse>> ResetInvitation(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(new ResetInvitationCommand(userId), cancellationToken);
        return this.ToActionResult(result);
    }

    // Extracts the weak If-Match value (`W/"hash"` or `"hash"`) as the opaque tag. On a missing/empty
    // header it records the standard model error (mirroring IfMatchModelBinder) and returns false, so
    // the caller responds 400. Note: `reset-invitation` intentionally does NOT require If-Match — it
    // reissues the invitation token and does not mutate the projected user resource.
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

    public sealed record CreateCompanyUserRequest(
        string Email,
        string FirstName,
        string LastName,
        IReadOnlyCollection<Guid> RolePublicIds);

    public sealed record UpdateCompanyUserRequest(
        string FirstName,
        string LastName,
        IReadOnlyCollection<Guid> RolePublicIds);

    // Patchable surface for the RFC 6902 JSON Patch endpoint. Used only for the OpenAPI schema and the
    // `JsonPatchDocument<T>` binding — values are applied server-side by CompanyUserPatchApplier.
    public sealed class PatchCompanyUserRequest
    {
        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public IReadOnlyCollection<Guid> RolePublicIds { get; set; } = [];
    }
}
