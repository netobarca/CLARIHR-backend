using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CommercialPlans;
using CLARIHR.Application.Features.CommercialPlans.Common;
using CLARIHR.Backoffice.Api.Common;
using CLARIHR.Domain.Companies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Backoffice.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/platform/commercial-plans")]
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

    [HttpGet("{publicId:guid}")]
    [ProducesResponseType<CommercialPlanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommercialPlanResponse>> GetById(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetCommercialPlanByIdQuery(publicId), cancellationToken);
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
                request.ModuleKeys,
                request.Limits),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<CommercialPlanResponse>.Failure(result.Error));
        }

        return CreatedAtAction(nameof(GetById), new { publicId = result.Value.Id }, result.Value);
    }

    [HttpPut("{publicId:guid}")]
    [ProducesResponseType<CommercialPlanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CommercialPlanResponse>> Update(
        Guid publicId,
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
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("{publicId:guid}/activate")]
    [ProducesResponseType<CommercialPlanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CommercialPlanResponse>> Activate(
        Guid publicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateCommercialPlanCommand(publicId, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("{publicId:guid}/inactivate")]
    [ProducesResponseType<CommercialPlanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CommercialPlanResponse>> Inactivate(
        Guid publicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateCommercialPlanCommand(publicId, request.ConcurrencyToken),
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
        IReadOnlyCollection<string> ModuleKeys,
        IReadOnlyCollection<CommercialPlanLimitInput> Limits);

    public sealed record UpdateCommercialPlanRequest(
        string Code,
        string Name,
        string? Description,
        decimal BaseMonthlyFee,
        decimal PricePerActiveEmployee,
        IReadOnlyCollection<string> ModuleKeys,
        IReadOnlyCollection<CommercialPlanLimitInput> Limits,
        Guid ConcurrencyToken);

    public sealed record ConcurrencyRequest(Guid ConcurrencyToken);
}
