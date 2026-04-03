using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.AccountCompanies;
using CLARIHR.Domain.Companies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/account/companies/{publicId:guid}/subscription")]
public sealed class AccountCompanySubscriptionsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("/api/account/companies/{publicId:guid}/subscription")]
    [ProducesResponseType<AccountCompanySubscriptionOverviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountCompanySubscriptionOverviewResponse>> GetSubscription(
        [FromRoute] Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetOwnedCompanySubscriptionQuery(ResolveCompanyPublicId(publicId)),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("/api/account/companies/{publicId:guid}/subscription/plans")]
    [ProducesResponseType<IReadOnlyCollection<AccountCompanySubscriptionPlanResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<AccountCompanySubscriptionPlanResponse>>> GetPlans(
        [FromRoute] Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetOwnedCompanySubscriptionPlansQuery(ResolveCompanyPublicId(publicId)),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("/api/account/companies/{publicId:guid}/subscription/preview")]
    [ProducesResponseType<AccountCompanySubscriptionPlanPreviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountCompanySubscriptionPlanPreviewResponse>> PreviewPlanChange(
        [FromRoute] Guid publicId,
        [FromBody] PreviewOwnedCompanySubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new PreviewOwnedCompanySubscriptionPlanChangeQuery(ResolveCompanyPublicId(publicId), request.CommercialPlanId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("/api/account/companies/{publicId:guid}/subscription")]
    [ProducesResponseType<AccountCompanySubscriptionOverviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountCompanySubscriptionOverviewResponse>> ChangePlan(
        [FromRoute] Guid publicId,
        [FromBody] ChangeOwnedCompanySubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ChangeOwnedCompanySubscriptionCommand(
                ResolveCompanyPublicId(publicId),
                request.CommercialPlanId,
                request.Observations),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("/api/account/companies/{publicId:guid}/subscription/addons")]
    [ProducesResponseType<IReadOnlyCollection<AccountCompanySubscriptionAddonResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<AccountCompanySubscriptionAddonResponse>>> GetAddons(
        [FromRoute] Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetOwnedCompanySubscriptionAddonsQuery(ResolveCompanyPublicId(publicId)),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("/api/account/companies/{publicId:guid}/subscription/addons/marketplace")]
    [ProducesResponseType<IReadOnlyCollection<AccountCompanyMarketplaceAddonResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<AccountCompanyMarketplaceAddonResponse>>> GetMarketplace(
        [FromRoute] Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetOwnedCompanySubscriptionMarketplaceQuery(ResolveCompanyPublicId(publicId)),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("/api/account/companies/{publicId:guid}/subscription/addons/preview")]
    [ProducesResponseType<AccountCompanyAddonChangePreviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountCompanyAddonChangePreviewResponse>> PreviewAddonChange(
        [FromRoute] Guid publicId,
        [FromBody] PreviewOwnedCompanyAddonRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new PreviewOwnedCompanyAddonChangeQuery(
                ResolveCompanyPublicId(publicId),
                request.CommercialAddonId,
                request.Action),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("/api/account/companies/{publicId:guid}/subscription/addons")]
    [ProducesResponseType<AccountCompanySubscriptionOverviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountCompanySubscriptionOverviewResponse>> ApplyAddonChange(
        [FromRoute] Guid publicId,
        [FromBody] ChangeOwnedCompanyAddonRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateOwnedCompanyAddonChangeCommand(
                ResolveCompanyPublicId(publicId),
                request.CommercialAddonId,
                request.Action,
                request.Observations),
            cancellationToken);
        return this.ToActionResult(result);
    }

    public sealed record PreviewOwnedCompanySubscriptionRequest(Guid CommercialPlanId);

    public sealed record ChangeOwnedCompanySubscriptionRequest(Guid CommercialPlanId, string? Observations);

    public sealed record PreviewOwnedCompanyAddonRequest(Guid CommercialAddonId, SubscriptionAddonChangeAction Action);

    public sealed record ChangeOwnedCompanyAddonRequest(
        Guid CommercialAddonId,
        SubscriptionAddonChangeAction Action,
        string? Observations);

    private Guid ResolveCompanyPublicId(Guid publicId)
    {
        if (publicId != Guid.Empty)
        {
            return publicId;
        }

        return Guid.TryParse(RouteData.Values["publicId"]?.ToString(), out var routeCompanyPublicId)
            ? routeCompanyPublicId
            : Guid.Empty;
    }
}
