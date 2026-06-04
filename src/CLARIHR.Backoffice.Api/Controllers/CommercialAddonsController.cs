using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CommercialAddons;
using CLARIHR.Application.Features.CommercialAddons.Common;
using CLARIHR.Backoffice.Api.Common;
using CLARIHR.Domain.Companies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Backoffice.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/platform/commercial-addons")]
[Tags("Commercial Addons")]
public sealed class CommercialAddonsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<CommercialAddonSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "Search commercial add-ons",
        Description = "Returns a paged list of global commercial add-ons.")]
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
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a commercial add-on",
        Description = "Returns a single commercial add-on. The current `concurrencyToken` is included in the body for use in the `If-Match` header of a subsequent update.")]
    public async Task<ActionResult<CommercialAddonResponse>> GetById(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetCommercialAddonByIdQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType<CommercialAddonResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Query | StandardErrorSet.Conflict)]
    [SwaggerOperation(
        Summary = "Create a commercial add-on",
        Description = "Creates a global commercial add-on. Returns `201`; the current `concurrencyToken` is included in the body and the `ETag` header.")]
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
                request.ModuleKeys,
                request.Periodicity,
                request.Status),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { publicId = value.PublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("{publicId:guid}")]
    [ProducesResponseType<CommercialAddonResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Update a commercial add-on",
        Description = "Replaces the editable fields. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<CommercialAddonResponse>> Update(
        Guid publicId,
        [FromIfMatch] Guid concurrencyToken,
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
                request.ModuleKeys,
                request.Periodicity,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("{publicId:guid}/activate")]
    [ProducesResponseType<CommercialAddonResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Activate a commercial add-on",
        Description = "Activates the add-on. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<CommercialAddonResponse>> Activate(
        Guid publicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateCommercialAddonCommand(publicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("{publicId:guid}/inactivate")]
    [ProducesResponseType<CommercialAddonResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Inactivate a commercial add-on",
        Description = "Inactivates the add-on. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<CommercialAddonResponse>> Inactivate(
        Guid publicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateCommercialAddonCommand(publicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
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
        IReadOnlyCollection<string> ModuleKeys,
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
        IReadOnlyCollection<string> ModuleKeys,
        CommercialAddonPeriodicity Periodicity);
}
