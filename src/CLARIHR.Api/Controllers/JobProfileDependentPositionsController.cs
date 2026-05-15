using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.JobProfiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/job-profiles/{jobProfilePublicId:guid}/dependent-positions")]
[Consumes("application/json")]
[Produces("application/json")]
public sealed class JobProfileDependentPositionsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = JobProfilePolicies.Read)]
    [ProducesResponseType<IReadOnlyCollection<JobProfileDependentPositionResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    public async Task<ActionResult<IReadOnlyCollection<JobProfileDependentPositionResponse>>> Get(
        Guid jobProfilePublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetJobProfileDependentPositionsQuery(jobProfilePublicId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("{dependentPositionPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Read)]
    [ProducesResponseType<JobProfileDependentPositionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    public async Task<ActionResult<JobProfileDependentPositionResponse>> GetById(
        Guid jobProfilePublicId,
        Guid dependentPositionPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetJobProfileDependentPositionByIdQuery(jobProfilePublicId, dependentPositionPublicId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileDependentPositionResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileDependentPositionResponse>> Add(
        Guid jobProfilePublicId,
        [FromBody] AddDependentPositionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddJobProfileDependentPositionCommand(
                jobProfilePublicId,
                request.DependentJobProfilePublicId,
                request.Quantity,
                request.Notes),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<JobProfileDependentPositionResponse>.Failure(result.Error));
        }

        this.SetETag(result, value => value.ConcurrencyToken);
        return CreatedAtAction(nameof(GetById), new { jobProfilePublicId, dependentPositionPublicId = result.Value.DependentPositionPublicId }, result.Value);
    }

    [HttpPut("{dependentPositionPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileDependentPositionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileDependentPositionResponse>> Update(
        Guid jobProfilePublicId,
        Guid dependentPositionPublicId,
        [FromHeader(Name = IfMatchHeader.HeaderName)] string? ifMatch,
        [FromBody] UpdateDependentPositionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IfMatchHeader.TryParseConcurrencyToken(ifMatch, out var concurrencyToken))
        {
            return BadRequest(ProblemDetailsFactory.CreateProblemDetails(
                HttpContext,
                statusCode: StatusCodes.Status400BadRequest,
                detail: IfMatchHeader.MissingDetail));
        }

        var result = await commandDispatcher.SendAsync(
            new UpdateJobProfileDependentPositionCommand(
                jobProfilePublicId,
                dependentPositionPublicId,
                request.DependentJobProfilePublicId,
                request.Quantity,
                request.Notes,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("{dependentPositionPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<JobProfileDependentPositionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileDependentPositionResponse>> Patch(
        Guid jobProfilePublicId,
        Guid dependentPositionPublicId,
        [FromHeader(Name = IfMatchHeader.HeaderName)] string? ifMatch,
        [FromBody] JsonPatchDocument<UpdateDependentPositionRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        if (!IfMatchHeader.TryParseConcurrencyToken(ifMatch, out var concurrencyToken))
        {
            return BadRequest(ProblemDetailsFactory.CreateProblemDetails(
                HttpContext,
                statusCode: StatusCodes.Status400BadRequest,
                detail: IfMatchHeader.MissingDetail));
        }

        var result = await commandDispatcher.SendAsync(
            new PatchJobProfileDependentPositionCommand(
                jobProfilePublicId,
                dependentPositionPublicId,
                concurrencyToken,
                MapPatchOperations(patchDoc)),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("{dependentPositionPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileParentConcurrencyResult>> Remove(
        Guid jobProfilePublicId,
        Guid dependentPositionPublicId,
        [FromHeader(Name = IfMatchHeader.HeaderName)] string? ifMatch,
        CancellationToken cancellationToken = default)
    {
        if (!IfMatchHeader.TryParseConcurrencyToken(ifMatch, out var concurrencyToken))
        {
            return BadRequest(ProblemDetailsFactory.CreateProblemDetails(
                HttpContext,
                statusCode: StatusCodes.Status400BadRequest,
                detail: IfMatchHeader.MissingDetail));
        }

        var result = await commandDispatcher.SendAsync(
            new RemoveJobProfileDependentPositionCommand(jobProfilePublicId, dependentPositionPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    private static IReadOnlyCollection<JobProfileDependentPositionPatchOperation> MapPatchOperations(JsonPatchDocument<UpdateDependentPositionRequest> patchDoc) =>
        JsonPatchOperationMapper.Map(
            patchDoc,
            static (op, path, from, value) => new JobProfileDependentPositionPatchOperation(op, path, from, value));

    public sealed class AddDependentPositionRequest
    {
        public Guid DependentJobProfilePublicId { get; set; }
        public int Quantity { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class UpdateDependentPositionRequest
    {
        public Guid DependentJobProfilePublicId { get; set; }
        public int Quantity { get; set; }
        public string? Notes { get; set; }
    }
}
