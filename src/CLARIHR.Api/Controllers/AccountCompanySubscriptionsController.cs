using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.AccountCompanies;
using CLARIHR.Domain.Companies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

// Authorization is bespoke per-resource ownership (the company's CreatedByUserPublicId must match the
// JWT subject), enforced in the handlers via AccountCompanySubscriptionHelper.ResolveOwnershipAsync —
// NOT RBAC. This family is intentionally excluded from [AuthorizationPolicySet]/GovernedFamilyRegex
// (mirror AccountCompanies): there is no permission/policy to declare, so a declarative attribute would
// be misleading. It IS enrolled in the OpenAPI guardrail ("Account Subscription") so a dropped
// [Tags]/[SwaggerOperation] fails CI. The route is canonically versioned under `api/v1/account/...`
// and uses the company-context placeholder `{companyPublicId}` (mirror AccountCompanies).
[ApiController]
[Authorize]
[Route("api/v1/account/companies/{companyPublicId:guid}/subscription")]
[Tags("Account Subscription")]
public sealed class AccountCompanySubscriptionsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<AccountCompanySubscriptionOverviewResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get the owned company's subscription overview",
        Description = """
            Returns the self-service subscription overview for a company owned by the authenticated
            user: current plan, active add-ons and the effective module grants (plan + add-ons). Only
            the company owner (`CreatedByUserPublicId == JWT subject`) may read it — a non-owner yields
            `403` and an unknown company `404`. The overview reflects the company's **active**
            subscription and carries its `concurrencyToken` (also returned in the `ETag` header) for use
            in the `If-Match` header of plan/add-on changes.
            """)]
    public async Task<ActionResult<AccountCompanySubscriptionOverviewResponse>> GetSubscription(
        [FromRoute] Guid companyPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetOwnedCompanySubscriptionQuery(companyPublicId),
            cancellationToken);
        return this.ToActionResultWithETag(result, static value => value.ConcurrencyToken);
    }

    [HttpGet("plans")]
    [ProducesResponseType<IReadOnlyCollection<AccountCompanySubscriptionPlanResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List subscription plans available to the owned company",
        Description = """
            Returns the commercial plans the owner can switch to, flagging the current one
            (`isCurrent`). The reserved MASTER plan is hidden unless the caller is a platform operator.
            Requires company ownership.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<AccountCompanySubscriptionPlanResponse>>> GetPlans(
        [FromRoute] Guid companyPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetOwnedCompanySubscriptionPlansQuery(companyPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("preview")]
    [ProducesResponseType<AccountCompanySubscriptionPlanPreviewResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Preview a plan change for the owned company",
        Description = """
            Computes the impact of switching the owned company to the target plan (added/removed
            modules, add-on compatibility warnings, eligibility) **without applying it**. This is a
            read-style query exposed over POST because it carries a request body. Switching to the
            MASTER plan requires platform-operator access (`403`).
            """)]
    public async Task<ActionResult<AccountCompanySubscriptionPlanPreviewResponse>> PreviewPlanChange(
        [FromRoute] Guid companyPublicId,
        [FromBody] PreviewOwnedCompanySubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new PreviewOwnedCompanySubscriptionPlanChangeQuery(companyPublicId, request.CommercialPlanId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut]
    [ProducesResponseType<AccountCompanySubscriptionOverviewResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Change the owned company's subscription plan",
        Description = """
            Applies an immediate plan change for the owned company: the current subscription is
            cancelled and a new active subscription is created in a single transaction, returning the
            resulting overview (so the client need not re-fetch) with the new `concurrencyToken` in the
            `ETag` header. Requires the current `concurrencyToken` in the `If-Match` header (missing →
            `400`, stale → `409 CONCURRENCY_CONFLICT`). Downgrading to FREE deactivates active add-ons.
            Switching to MASTER requires platform-operator access (`403`).
            """)]
    public async Task<ActionResult<AccountCompanySubscriptionOverviewResponse>> ChangePlan(
        [FromRoute] Guid companyPublicId,
        [FromBody] ChangeOwnedCompanySubscriptionRequest request,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ChangeOwnedCompanySubscriptionCommand(
                companyPublicId,
                request.CommercialPlanId,
                request.Observations,
                concurrencyToken),
            cancellationToken);
        return this.ToActionResultWithETag(result, static value => value.ConcurrencyToken);
    }

    [HttpGet("addons")]
    [ProducesResponseType<IReadOnlyCollection<AccountCompanySubscriptionAddonResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List the owned company's active add-ons",
        Description = """
            Returns the add-ons currently active on the owned company's subscription, enriched with
            their catalog modules. Requires company ownership.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<AccountCompanySubscriptionAddonResponse>>> GetAddons(
        [FromRoute] Guid companyPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetOwnedCompanySubscriptionAddonsQuery(companyPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("addons/marketplace")]
    [ProducesResponseType<IReadOnlyCollection<AccountCompanyMarketplaceAddonResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List add-ons available to acquire for the owned company",
        Description = """
            Returns the commercial add-on marketplace for the owned company, flagging which add-ons are
            already owned (`isOwned`) and which can be acquired (`canAcquire` — a FREE subscription must
            upgrade first, surfaced in `blockedReason`). Requires company ownership.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<AccountCompanyMarketplaceAddonResponse>>> GetMarketplace(
        [FromRoute] Guid companyPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetOwnedCompanySubscriptionMarketplaceQuery(companyPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("addons/preview")]
    [ProducesResponseType<AccountCompanyAddonChangePreviewResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Preview an add-on change for the owned company",
        Description = """
            Computes the impact of activating or deactivating an add-on (added/removed modules,
            eligibility, warnings) **without applying it**. Read-style query exposed over POST because
            it carries a request body.
            """)]
    public async Task<ActionResult<AccountCompanyAddonChangePreviewResponse>> PreviewAddonChange(
        [FromRoute] Guid companyPublicId,
        [FromBody] PreviewOwnedCompanyAddonRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new PreviewOwnedCompanyAddonChangeQuery(
                companyPublicId,
                request.CommercialAddonId,
                request.Action),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("addons")]
    [ProducesResponseType<AccountCompanySubscriptionOverviewResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Apply an add-on change to the owned company",
        Description = """
            Applies a **reversible** add-on change to the owned company's subscription. `action` selects
            Activate or Deactivate; both are commands (not a resource creation) and return the resulting
            subscription overview, so the response carries the new state and the client need not re-fetch
            (HTTP `200`, not `201`). Requires the current subscription `concurrencyToken` in the
            `If-Match` header (missing → `400`, stale → `409 CONCURRENCY_CONFLICT`). A concurrent submit
            for the same add-on also collides on the unique company-add-on constraint and yields `409`.
            """)]
    public async Task<ActionResult<AccountCompanySubscriptionOverviewResponse>> ApplyAddonChange(
        [FromRoute] Guid companyPublicId,
        [FromBody] ChangeOwnedCompanyAddonRequest request,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateOwnedCompanyAddonChangeCommand(
                companyPublicId,
                request.CommercialAddonId,
                request.Action,
                request.Observations,
                concurrencyToken),
            cancellationToken);
        return this.ToActionResultWithETag(result, static value => value.ConcurrencyToken);
    }

    public sealed record PreviewOwnedCompanySubscriptionRequest(Guid CommercialPlanId);

    public sealed record ChangeOwnedCompanySubscriptionRequest(Guid CommercialPlanId, string? Observations);

    public sealed record PreviewOwnedCompanyAddonRequest(Guid CommercialAddonId, SubscriptionAddonChangeAction Action);

    public sealed record ChangeOwnedCompanyAddonRequest(
        Guid CommercialAddonId,
        SubscriptionAddonChangeAction Action,
        string? Observations);
}
