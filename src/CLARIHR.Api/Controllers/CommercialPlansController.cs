using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CommercialPlans;
using CLARIHR.Application.Features.CommercialPlans.Common;
using CLARIHR.Domain.Companies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/account/commercial-plans")]
public sealed class CommercialPlansController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<CommercialPlanSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
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

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CommercialPlanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommercialPlanResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetCommercialPlanByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType<CommercialPlanResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
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
                request.Limits),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<CommercialPlanResponse>.Failure(result.Error));
        }

        return CreatedAtAction(nameof(GetById), new { publicId = result.Value.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<CommercialPlanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CommercialPlanResponse>> Update(
        Guid id,
        [FromBody] UpdateCommercialPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateCommercialPlanCommand(
                id,
                request.Code,
                request.Name,
                request.Description,
                request.BaseMonthlyFee,
                request.PricePerActiveEmployee,
                request.Limits,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("{id:guid}/activate")]
    [ProducesResponseType<CommercialPlanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CommercialPlanResponse>> Activate(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateCommercialPlanCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("{id:guid}/inactivate")]
    [ProducesResponseType<CommercialPlanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CommercialPlanResponse>> Inactivate(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateCommercialPlanCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    public sealed record CreateCommercialPlanRequest(
        string Code,
        string Name,
        string? Description,
        decimal BaseMonthlyFee,
        decimal PricePerActiveEmployee,
        CommercialPlanStatus Status,
        IReadOnlyCollection<CommercialPlanLimitInput> Limits);

    public sealed record UpdateCommercialPlanRequest(
        string Code,
        string Name,
        string? Description,
        decimal BaseMonthlyFee,
        decimal PricePerActiveEmployee,
        IReadOnlyCollection<CommercialPlanLimitInput> Limits,
        Guid ConcurrencyToken);

    public sealed record ConcurrencyRequest(Guid ConcurrencyToken);
}
