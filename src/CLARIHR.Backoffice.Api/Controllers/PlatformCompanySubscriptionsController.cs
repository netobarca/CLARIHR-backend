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
        [FromBody] ChangePlatformCompanySubscriptionStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ChangePlatformCompanySubscriptionStatusCommand(
                companyPublicId,
                subscriptionPublicId,
                request.TargetStatus,
                request.ReasonCode,
                request.Observations),
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

    public sealed record UpsertPlatformCompanySubscriptionRequest(
        Guid CommercialPlanId,
        DateTime StartDateUtc,
        DateTime? ExpiresAtUtc,
        CompanySubscriptionPeriodicity Periodicity);

    public sealed record ChangePlatformCompanySubscriptionStatusRequest(
        SubscriptionStatus TargetStatus,
        SubscriptionStatusChangeReasonCode ReasonCode,
        string? Observations);
}
