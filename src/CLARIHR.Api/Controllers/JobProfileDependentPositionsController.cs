using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.JobProfiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize(Policy = JobProfilePolicies.Manage)]
[Route("api/v1/job-profiles/{publicId:guid}/dependent-positions")]
[Consumes("application/json")]
[Produces("application/json")]
[ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
public sealed class JobProfileDependentPositionsController(
    ICommandDispatcher commandDispatcher) : ControllerBase
{
    private const string ParentConcurrencyTokenHeaderName = "Parent-Concurrency-Token";

    [HttpPost]
    [ProducesResponseType<JobProfileSubResourceResult<JobProfileDependentPositionResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<JobProfileSubResourceResult<JobProfileDependentPositionResponse>>> Add(
        Guid publicId,
        [FromBody] AddDependentPositionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddJobProfileDependentPositionCommand(
                publicId,
                request.DependentJobProfileId,
                request.Quantity,
                request.Notes,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("{dependentPositionPublicId:guid}")]
    [ProducesResponseType<JobProfileSubResourceResult<JobProfileDependentPositionResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<JobProfileSubResourceResult<JobProfileDependentPositionResponse>>> Update(
        Guid publicId,
        Guid dependentPositionPublicId,
        [FromBody] UpdateDependentPositionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateJobProfileDependentPositionCommand(
                publicId,
                dependentPositionPublicId,
                request.DependentJobProfileId,
                request.Quantity,
                request.Notes,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpDelete("{dependentPositionPublicId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(
        Guid publicId,
        Guid dependentPositionPublicId,
        [FromHeader(Name = IfMatchHeader.HeaderName)] string? ifMatch,
        CancellationToken cancellationToken = default)
    {
        if (!IfMatchHeader.TryParseConcurrencyToken(ifMatch, out var concurrencyToken))
        {
            return BadRequest(ProblemDetailsFactory.CreateProblemDetails(HttpContext, statusCode: StatusCodes.Status400BadRequest, detail: IfMatchHeader.MissingDetail));
        }

        var result = await commandDispatcher.SendAsync(
            new RemoveJobProfileDependentPositionCommand(publicId, dependentPositionPublicId, concurrencyToken),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(result).Result!;
        }

        Response.Headers[ParentConcurrencyTokenHeaderName] = result.Value.ParentConcurrencyToken.ToString();
        return NoContent();
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

}
