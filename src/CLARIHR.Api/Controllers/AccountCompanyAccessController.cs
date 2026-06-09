using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.AccountCompanies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

// Read-only access/authorization context for an owned company: the effective access context (commercial
// entitlements + the caller's roles/permissions/scopes), the role-builder catalog, and a per-resource policy
// projection. Extracted from AccountCompaniesController so that controller administers only the Company entity
// (canonical points 1/6/13); the routes are preserved verbatim under
// `api/v1/account/companies/{companyPublicId}` (+ `/access-context`, `/authorization/role-builder-catalog`,
// `/authorization/resource-policies/{resourceKey}`) — moving them here changed no URL.
//
// Authorization is bespoke per-resource ownership (the company's CreatedByUserPublicId must match the JWT
// subject, resolved in the handlers via AccountCompanySubscriptionHelper.ResolveOwnershipAsync) — NOT RBAC,
// unlike the sibling AccountCompanyAuthorizationController. The family is intentionally excluded from
// [AuthorizationPolicySet]/GovernedFamilyRegex (mirror AccountCompanies): there is no permission/policy to
// declare. It IS enrolled in the OpenAPI guardrail ("Account Access Context") so a dropped
// [Tags]/[SwaggerOperation] fails CI.
[ApiController]
[Authorize]
[Route("api/v1/account/companies/{companyPublicId:guid}")]
[Tags("Account Access Context")]
public sealed class AccountCompanyAccessController(
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("access-context")]
    [ProducesResponseType<AccountCompanyAccessContextResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get an owned company's access context",
        Description = """
            Returns the effective access context for an owned company: the commercial context (the active
            plan and add-ons with their capability codes), the effective capabilities and modules, and the
            current user's access within that company (roles, permissions and scopes). Requires ownership of
            the company (the caller must be its creator); a company owned by another user yields `403`/`404`.
            """)]
    public async Task<ActionResult<AccountCompanyAccessContextResponse>> GetAccessContext(
        Guid companyPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetOwnedCompanyAccessContextQuery(companyPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("authorization/role-builder-catalog")]
    [ProducesResponseType<AccountCompanyRoleBuilderCatalogResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get an owned company's role-builder catalog",
        Description = """
            Returns the catalog used to build authorization roles for an owned company: the available modules
            and capabilities (from the company's effective entitlements), the grantable (non-dormant)
            permissions, the field-policy catalog, and the supported scope types. Requires ownership of the
            company; a company owned by another user yields `403`/`404`.
            """)]
    public async Task<ActionResult<AccountCompanyRoleBuilderCatalogResponse>> GetRoleBuilderCatalog(
        Guid companyPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetOwnedCompanyRoleBuilderCatalogQuery(companyPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("authorization/resource-policies/{resourceKey}")]
    [ProducesResponseType<AccountCompanyResourcePolicyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get the current user's policy for a resource",
        Description = """
            Returns the current user's effective policy for a single resource key within an owned company:
            the action policy (can access/read/create/update/delete) and the per-field access states
            (`hidden`/`masked`/`readonly`/`editable`, plus the required and sensitive flags). An unknown
            resource key yields `400`. Requires ownership of the company and the active tenant context to
            match it.
            """)]
    public async Task<ActionResult<AccountCompanyResourcePolicyResponse>> GetResourcePolicy(
        Guid companyPublicId,
        string resourceKey,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetOwnedCompanyResourcePolicyQuery(companyPublicId, resourceKey), cancellationToken);
        return this.ToActionResult(result);
    }
}
