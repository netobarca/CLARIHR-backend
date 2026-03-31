using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CommercialAddons;
using CLARIHR.Application.Features.CommercialAddons.Common;
using CLARIHR.Backoffice.Api.Common;
using CLARIHR.Domain.Companies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Backoffice.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/platform/commercial-addons")]
public sealed class CommercialAddonsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<CommercialAddonSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<CommercialAddonSummaryResponse>>> Search(
        [FromQuery] CommercialAddonType? type,
        [FromQuery] CommercialAddonBillingModel? billingModel,
        [FromQuery] CommercialAddonStatus? status,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = CommercialAddonValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchCommercialAddonsQuery(type, billingModel, status, search, page, pageSize),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("{publicId:guid}")]
    [ProducesResponseType<CommercialAddonResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommercialAddonResponse>> GetById(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetCommercialAddonByIdQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType<CommercialAddonResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CommercialAddonResponse>> Create(
        [FromBody] CreateCommercialAddonRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateCommercialAddonCommand(
                request.Code,
                request.Name,
                request.Description,
                request.Type,
                request.BillingModel,
                request.MeasurementUnit,
                request.UnitPrice,
                request.MinimumQuantity,
                request.MinimumMonthlyFee,
                request.Periodicity,
                request.Status),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<CommercialAddonResponse>.Failure(result.Error));
        }

        return CreatedAtAction(nameof(GetById), new { publicId = result.Value.PublicId }, result.Value);
    }

    [HttpPut("{publicId:guid}")]
    [ProducesResponseType<CommercialAddonResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CommercialAddonResponse>> Update(
        Guid publicId,
        [FromBody] UpdateCommercialAddonRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateCommercialAddonCommand(
                publicId,
                request.Code,
                request.Name,
                request.Description,
                request.Type,
                request.BillingModel,
                request.MeasurementUnit,
                request.UnitPrice,
                request.MinimumQuantity,
                request.MinimumMonthlyFee,
                request.Periodicity,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("{publicId:guid}/activate")]
    [ProducesResponseType<CommercialAddonResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CommercialAddonResponse>> Activate(
        Guid publicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateCommercialAddonCommand(publicId, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("{publicId:guid}/inactivate")]
    [ProducesResponseType<CommercialAddonResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CommercialAddonResponse>> Inactivate(
        Guid publicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateCommercialAddonCommand(publicId, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    public sealed record CreateCommercialAddonRequest(
        string Code,
        string Name,
        string? Description,
        CommercialAddonType Type,
        CommercialAddonBillingModel BillingModel,
        string MeasurementUnit,
        decimal UnitPrice,
        int? MinimumQuantity,
        decimal? MinimumMonthlyFee,
        CommercialAddonPeriodicity Periodicity,
        CommercialAddonStatus Status);

    public sealed record UpdateCommercialAddonRequest(
        string Code,
        string Name,
        string? Description,
        CommercialAddonType Type,
        CommercialAddonBillingModel BillingModel,
        string MeasurementUnit,
        decimal UnitPrice,
        int? MinimumQuantity,
        decimal? MinimumMonthlyFee,
        CommercialAddonPeriodicity Periodicity,
        Guid ConcurrencyToken);

    public sealed record ConcurrencyRequest(Guid ConcurrencyToken);
}
