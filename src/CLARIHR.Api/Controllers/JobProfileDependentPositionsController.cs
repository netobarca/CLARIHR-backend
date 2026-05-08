using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.JobProfiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/job-profiles/{jobProfileId:guid}/dependent-positions")]
public sealed class JobProfileDependentPositionsController(
    ICommandDispatcher commandDispatcher) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<JobProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobProfileResponse>> Add(
        Guid jobProfileId,
        [FromBody] AddDependentPositionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddJobProfileDependentPositionCommand(
                jobProfileId,
                request.DependentJobProfileId,
                request.Quantity,
                request.Notes,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("{dependentPositionId:guid}")]
    [ProducesResponseType<JobProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobProfileResponse>> Update(
        Guid jobProfileId,
        Guid dependentPositionId,
        [FromBody] UpdateDependentPositionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateJobProfileDependentPositionCommand(
                jobProfileId,
                dependentPositionId,
                request.DependentJobProfileId,
                request.Quantity,
                request.Notes,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpDelete("{dependentPositionId:guid}")]
    [ProducesResponseType<JobProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobProfileResponse>> Remove(
        Guid jobProfileId,
        Guid dependentPositionId,
        [FromBody] ConcurrencyTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new RemoveJobProfileDependentPositionCommand(jobProfileId, dependentPositionId, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    public sealed class AddDependentPositionRequest
    {
        public Guid DependentJobProfileId { get; init; }
        public int Quantity { get; init; }
        public string? Notes { get; init; }
        public Guid ConcurrencyToken { get; init; }
    }

    public sealed class UpdateDependentPositionRequest
    {
        public Guid DependentJobProfileId { get; init; }
        public int Quantity { get; init; }
        public string? Notes { get; init; }
        public Guid ConcurrencyToken { get; init; }
    }

    public sealed class ConcurrencyTokenRequest
    {
        public Guid ConcurrencyToken { get; init; }
    }
}
