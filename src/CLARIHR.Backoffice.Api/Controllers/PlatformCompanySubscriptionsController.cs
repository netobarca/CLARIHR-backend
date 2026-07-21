using CLARIHR.Api.Common.Binders;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PlatformSubscriptions;
using CLARIHR.Backoffice.Api.Common;
using CLARIHR.Domain.Companies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Backoffice.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/platform/companies/{companyPublicId:guid}")]
public sealed class PlatformCompanySubscriptionsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("subscription")]
    [ProducesResponseType<PlatformCompanySubscriptionOverviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlatformCompanySubscriptionOverviewResponse>> GetCurrent(
        Guid companyPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPlatformCompanySubscriptionQuery(companyPublicId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("subscription/preview")]
    [ProducesResponseType<PlatformCompanySubscriptionPreviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlatformCompanySubscriptionPreviewResponse>> Preview(
        Guid companyPublicId,
        [FromBody] UpsertPlatformCompanySubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new PreviewPlatformCompanySubscriptionQuery(
                companyPublicId,
                request.CommercialPlanId,
                request.StartDateUtc,
                request.ExpiresAtUtc,
                request.Periodicity),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("subscriptions")]
    [ProducesResponseType<PagedResponse<PlatformCompanySubscriptionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<PlatformCompanySubscriptionResponse>>> Search(
        Guid companyPublicId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPlatformCompanySubscriptionsQuery(companyPublicId, page, pageSize),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("subscription")]
    [ProducesResponseType<PlatformCompanySubscriptionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlatformCompanySubscriptionResponse>> Replace(
        Guid companyPublicId,
        [FromBody] UpsertPlatformCompanySubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivatePlatformCompanySubscriptionCommand(
                companyPublicId,
                request.CommercialPlanId,
                request.StartDateUtc,
                request.ExpiresAtUtc,
                request.Periodicity),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("subscriptions/{subscriptionPublicId:guid}/status")]
    [ProducesResponseType<PlatformCompanySubscriptionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlatformCompanySubscriptionResponse>> ChangeStatus(
        Guid companyPublicId,
        Guid subscriptionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] ChangePlatformCompanySubscriptionStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ChangePlatformCompanySubscriptionStatusCommand(
                companyPublicId,
                subscriptionPublicId,
                request.TargetStatus,
                request.ReasonCode,
                request.Observations,
                request.EffectiveDateUtc,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("subscriptions/{subscriptionPublicId:guid}/status/preview")]
    [ProducesResponseType<PlatformCompanySubscriptionStatusChangePreviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlatformCompanySubscriptionStatusChangePreviewResponse>> PreviewStatusChange(
        Guid companyPublicId,
        Guid subscriptionPublicId,
        [FromBody] ChangePlatformCompanySubscriptionStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new PreviewPlatformCompanySubscriptionStatusChangeQuery(
                companyPublicId,
                subscriptionPublicId,
                request.TargetStatus,
                request.ReasonCode,
                request.Observations,
                request.EffectiveDateUtc),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("subscriptions/{subscriptionPublicId:guid}/status-history")]
    [ProducesResponseType<PagedResponse<PlatformCompanySubscriptionStatusTransitionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<PlatformCompanySubscriptionStatusTransitionResponse>>> GetStatusHistory(
        Guid companyPublicId,
        Guid subscriptionPublicId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchPlatformCompanySubscriptionStatusHistoryQuery(
                companyPublicId,
                subscriptionPublicId,
                page,
                pageSize),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("subscription/plan-changes/preview")]
    [ProducesResponseType<PlatformCompanySubscriptionPlanChangePreviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlatformCompanySubscriptionPlanChangePreviewResponse>> PreviewPlanChange(
        Guid companyPublicId,
        [FromBody] PreviewPlatformCompanySubscriptionPlanChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new PreviewPlatformCompanySubscriptionPlanChangeQuery(
                companyPublicId,
                request.CommercialPlanId,
                request.Mode,
                request.RequestedEffectiveDateUtc),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("subscription/plan-changes")]
    [ProducesResponseType<PlatformCompanySubscriptionPlanChangeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlatformCompanySubscriptionPlanChangeResponse>> CreatePlanChange(
        Guid companyPublicId,
        [FromBody] CreatePlatformCompanySubscriptionPlanChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreatePlatformCompanySubscriptionPlanChangeCommand(
                companyPublicId,
                request.CommercialPlanId,
                request.Mode,
                request.RequestedEffectiveDateUtc,
                request.ReasonCode,
                request.Observations),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("subscription/plan-changes")]
    [ProducesResponseType<PagedResponse<PlatformCompanySubscriptionPlanChangeResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<PlatformCompanySubscriptionPlanChangeResponse>>> GetPlanChanges(
        Guid companyPublicId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchPlatformCompanySubscriptionPlanChangesQuery(companyPublicId, page, pageSize),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("subscription/plan-changes/{planChangePublicId:guid}/cancel")]
    [ProducesResponseType<PlatformCompanySubscriptionPlanChangeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlatformCompanySubscriptionPlanChangeResponse>> CancelPlanChange(
        Guid companyPublicId,
        Guid planChangePublicId,
        [FromBody] CancelPlatformCompanySubscriptionPlanChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CancelPlatformCompanySubscriptionPlanChangeCommand(
                companyPublicId,
                planChangePublicId,
                request.Observations,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("subscription/addons")]
    [ProducesResponseType<PagedResponse<PlatformCompanyAddonResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<PlatformCompanyAddonResponse>>> GetCompanyAddons(
        Guid companyPublicId,
        [FromQuery] CompanyAddonStatus? status = null,
        [FromQuery(Name = "q")] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchPlatformCompanyAddonsQuery(companyPublicId, status, search, page, pageSize),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("subscription/addons/eligible")]
    [ProducesResponseType<PagedResponse<PlatformCompanyEligibleAddonResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<PlatformCompanyEligibleAddonResponse>>> GetEligibleAddons(
        Guid companyPublicId,
        [FromQuery] CommercialAddonType? type = null,
        [FromQuery(Name = "q")] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchPlatformCompanyEligibleAddonsQuery(companyPublicId, type, search, page, pageSize),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("subscription/addon-changes/preview")]
    [ProducesResponseType<PlatformCompanyAddonChangePreviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlatformCompanyAddonChangePreviewResponse>> PreviewAddonChange(
        Guid companyPublicId,
        [FromBody] PreviewPlatformCompanyAddonChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new PreviewPlatformCompanyAddonChangeQuery(
                companyPublicId,
                request.CommercialAddonId,
                request.Action,
                request.Mode,
                request.RequestedEffectiveDateUtc),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("subscription/addon-changes")]
    [ProducesResponseType<PlatformCompanyAddonChangeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlatformCompanyAddonChangeResponse>> CreateAddonChange(
        Guid companyPublicId,
        [FromBody] CreatePlatformCompanyAddonChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreatePlatformCompanyAddonChangeCommand(
                companyPublicId,
                request.CommercialAddonId,
                request.Action,
                request.Mode,
                request.RequestedEffectiveDateUtc,
                request.ReasonCode,
                request.Observations),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("subscription/addon-changes")]
    [ProducesResponseType<PagedResponse<PlatformCompanyAddonChangeResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<PlatformCompanyAddonChangeResponse>>> GetAddonChanges(
        Guid companyPublicId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchPlatformCompanyAddonChangesQuery(companyPublicId, page, pageSize),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("subscription/addon-changes/{addonChangePublicId:guid}/cancel")]
    [ProducesResponseType<PlatformCompanyAddonChangeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlatformCompanyAddonChangeResponse>> CancelAddonChange(
        Guid companyPublicId,
        Guid addonChangePublicId,
        [FromBody] CancelPlatformCompanyAddonChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CancelPlatformCompanyAddonChangeCommand(
                companyPublicId,
                addonChangePublicId,
                request.Observations,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    public sealed record UpsertPlatformCompanySubscriptionRequest(
        Guid CommercialPlanId,
        DateTime StartDateUtc,
        DateTime? ExpiresAtUtc,
        CompanySubscriptionPeriodicity Periodicity);

    public sealed record ChangePlatformCompanySubscriptionStatusRequest(
        SubscriptionStatus TargetStatus,
        SubscriptionStatusChangeReasonCode ReasonCode,
        string? Observations,
        DateTime? EffectiveDateUtc);

    public sealed record PreviewPlatformCompanySubscriptionPlanChangeRequest(
        Guid CommercialPlanId,
        SubscriptionPlanChangeMode Mode,
        DateTime? RequestedEffectiveDateUtc);

    public sealed record CreatePlatformCompanySubscriptionPlanChangeRequest(
        Guid CommercialPlanId,
        SubscriptionPlanChangeMode Mode,
        DateTime? RequestedEffectiveDateUtc,
        SubscriptionPlanChangeReasonCode ReasonCode,
        string? Observations);

    public sealed record CancelPlatformCompanySubscriptionPlanChangeRequest(string Observations, Guid ConcurrencyToken);

    public sealed record PreviewPlatformCompanyAddonChangeRequest(
        Guid CommercialAddonId,
        SubscriptionAddonChangeAction Action,
        SubscriptionAddonChangeMode Mode,
        DateTime? RequestedEffectiveDateUtc);

    public sealed record CreatePlatformCompanyAddonChangeRequest(
        Guid CommercialAddonId,
        SubscriptionAddonChangeAction Action,
        SubscriptionAddonChangeMode Mode,
        DateTime? RequestedEffectiveDateUtc,
        SubscriptionAddonChangeReasonCode ReasonCode,
        string? Observations);

    public sealed record CancelPlatformCompanyAddonChangeRequest(string Observations, Guid ConcurrencyToken);
}
