using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CommercialPlans;
using CLARIHR.Application.Features.CommercialPlans.Common;
using CLARIHR.Backoffice.Api.Common;
using CLARIHR.Domain.Companies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Backoffice.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/platform/commercial-plans")]
[Tags("Commercial Plans")]
public sealed class CommercialPlansController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<CommercialPlanSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "Search commercial plans",
        Description = "Returns a paged list of global commercial plans.")]
    public async Task<ActionResult<PagedResponse<CommercialPlanSummaryResponse>>> Search(
        [FromQuery] CommercialPlanStatus? status,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = CommercialPlanValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchCommercialPlansQuery(status, search, page, pageSize),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("{publicId:guid}")]
    [ProducesResponseType<CommercialPlanResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a commercial plan",
        Description = "Returns a single commercial plan. The current `concurrencyToken` is included in the body for use in the `If-Match` header of a subsequent update.")]
    public async Task<ActionResult<CommercialPlanResponse>> GetById(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetCommercialPlanByIdQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType<CommercialPlanResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Query | StandardErrorSet.Conflict)]
    [SwaggerOperation(
        Summary = "Create a commercial plan",
        Description = "Creates a global commercial plan. Returns `201`; the current `concurrencyToken` is included in the body and the `ETag` header.")]
    public async Task<ActionResult<CommercialPlanResponse>> Create(
        [FromBody] CreateCommercialPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateCommercialPlanCommand(
                request.Code,
                request.Name,
                request.Description,
                request.BaseMonthlyFee,
                request.PricePerActiveEmployee,
                request.Status,
                request.ModuleKeys,
                request.Limits),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("{publicId:guid}")]
    [ProducesResponseType<CommercialPlanResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Update a commercial plan",
        Description = "Replaces the editable fields. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<CommercialPlanResponse>> Update(
        Guid publicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateCommercialPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateCommercialPlanCommand(
                publicId,
                request.Code,
                request.Name,
                request.Description,
                request.BaseMonthlyFee,
                request.PricePerActiveEmployee,
                request.ModuleKeys,
                request.Limits,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("{publicId:guid}/activate")]
    [ProducesResponseType<CommercialPlanResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Activate a commercial plan",
        Description = "Activates the plan. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<CommercialPlanResponse>> Activate(
        Guid publicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateCommercialPlanCommand(publicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("{publicId:guid}/inactivate")]
    [ProducesResponseType<CommercialPlanResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Inactivate a commercial plan",
        Description = "Inactivates the plan. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<CommercialPlanResponse>> Inactivate(
        Guid publicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateCommercialPlanCommand(publicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record CreateCommercialPlanRequest(
        string Code,
        string Name,
        string? Description,
        decimal BaseMonthlyFee,
        decimal PricePerActiveEmployee,
        CommercialPlanStatus Status,
        IReadOnlyCollection<string> ModuleKeys,
        IReadOnlyCollection<CommercialPlanLimitInput> Limits);

    public sealed record UpdateCommercialPlanRequest(
        string Code,
        string Name,
        string? Description,
        decimal BaseMonthlyFee,
        decimal PricePerActiveEmployee,
        IReadOnlyCollection<string> ModuleKeys,
        IReadOnlyCollection<CommercialPlanLimitInput> Limits);
}
