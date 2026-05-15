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
[Route("api/v1/job-profiles/{jobProfilePublicId:guid}/compensations")]
[Consumes("application/json")]
[Produces("application/json")]
public sealed class JobProfileCompensationsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = JobProfilePolicies.Read)]
    [ProducesResponseType<IReadOnlyCollection<JobProfileCompensationItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    public async Task<ActionResult<IReadOnlyCollection<JobProfileCompensationItemResponse>>> Get(
        Guid jobProfilePublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetJobProfileCompensationsQuery(jobProfilePublicId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("{compensationPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Read)]
    [ProducesResponseType<JobProfileCompensationItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    public async Task<ActionResult<JobProfileCompensationItemResponse>> GetById(
        Guid jobProfilePublicId,
        Guid compensationPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetJobProfileCompensationByIdQuery(jobProfilePublicId, compensationPublicId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileCompensationItemResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileCompensationItemResponse>> Add(
        Guid jobProfilePublicId,
        [FromBody] AddCompensationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddJobProfileCompensationCommand(
                jobProfilePublicId,
                request.SalaryTabulatorLinePublicId,
                request.Notes),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<JobProfileCompensationItemResponse>.Failure(result.Error));
        }

        this.SetETag(result, value => value.ConcurrencyToken);
        return CreatedAtAction(nameof(GetById), new { jobProfilePublicId, compensationPublicId = result.Value.CompensationPublicId }, result.Value);
    }

    [HttpPut("{compensationPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileCompensationItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileCompensationItemResponse>> Update(
        Guid jobProfilePublicId,
        Guid compensationPublicId,
        [FromHeader(Name = IfMatchHeader.HeaderName)] string? ifMatch,
        [FromBody] UpdateCompensationRequest request,
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
            new UpdateJobProfileCompensationCommand(
                jobProfilePublicId,
                compensationPublicId,
                request.SalaryTabulatorLinePublicId,
                request.Notes,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("{compensationPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<JobProfileCompensationItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileCompensationItemResponse>> Patch(
        Guid jobProfilePublicId,
        Guid compensationPublicId,
        [FromHeader(Name = IfMatchHeader.HeaderName)] string? ifMatch,
        [FromBody] JsonPatchDocument<UpdateCompensationRequest> patchDoc,
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
            new PatchJobProfileCompensationCommand(
                jobProfilePublicId,
                compensationPublicId,
                concurrencyToken,
                MapPatchOperations(patchDoc)),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("{compensationPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileParentConcurrencyResult>> Remove(
        Guid jobProfilePublicId,
        Guid compensationPublicId,
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
            new RemoveJobProfileCompensationCommand(jobProfilePublicId, compensationPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    private static IReadOnlyCollection<JobProfileCompensationPatchOperation> MapPatchOperations(JsonPatchDocument<UpdateCompensationRequest> patchDoc) =>
        JsonPatchOperationMapper.Map(
            patchDoc,
            static (op, path, from, value) => new JobProfileCompensationPatchOperation(op, path, from, value));

    public sealed class AddCompensationRequest
    {
        public Guid SalaryTabulatorLinePublicId { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class UpdateCompensationRequest
    {
        public Guid SalaryTabulatorLinePublicId { get; set; }
        public string? Notes { get; set; }
    }
}
