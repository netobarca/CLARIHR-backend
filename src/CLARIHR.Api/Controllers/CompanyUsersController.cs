using CLARIHR.Api.Common;
using CLARIHR.Api.Authorization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CompanyUsers;
using CLARIHR.Application.Features.IdentityAccess.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;

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
[Tags("Company Users")]
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
    [HttpGet("{userId:guid}")]
    [ProducesResponseType<CompanyUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyUserResponse>> Get(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(new GetCompanyUserQuery(userId), cancellationToken);
        return this.ToActionResultWithWeakETag(result, value => value.WeakETag);
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

        return this.ToCreatedAtActionResultWithWeakETag(
            result,
            nameof(Get),
            value => new { userPublicId = value.User.Id },
            value => value.User.WeakETag);
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

    // RFC 6902 JSON Patch over the editable fields of the company user. Media type
    // `application/json-patch+json`; the wire body is a bare array of operations. Patchable paths:
    // `/firstName`, `/lastName`, `/rolePublicIds`. Activation state changes use the `/deactivate` and
    // `/reactivate` actions; the e-mail address is immutable. Requires the current weak ETag in
    // `If-Match` (missing → 400, stale → 409 CONCURRENCY_CONFLICT). The refreshed weak ETag is returned
    // in the `ETag` header. Internally it resolves the partial change and reuses the PUT mutation path.
    [AuthorizeResource("RBAC_USERS", RbacPermissionAction.Update)]
    [HttpPatch("{userId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<CompanyUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
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
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
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
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
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
