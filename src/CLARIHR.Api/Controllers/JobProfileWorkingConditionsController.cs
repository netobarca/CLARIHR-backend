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
[Route("api/v1/job-profiles/{jobProfilePublicId:guid}/working-conditions")]
[Consumes("application/json")]
[Produces("application/json")]
public sealed class JobProfileWorkingConditionsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = JobProfilePolicies.Read)]
    [ProducesResponseType<IReadOnlyCollection<JobProfileWorkingConditionResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    public async Task<ActionResult<IReadOnlyCollection<JobProfileWorkingConditionResponse>>> Get(
        Guid jobProfilePublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetJobProfileWorkingConditionsQuery(jobProfilePublicId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("{workingConditionPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Read)]
    [ProducesResponseType<JobProfileWorkingConditionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    public async Task<ActionResult<JobProfileWorkingConditionResponse>> GetById(
        Guid jobProfilePublicId,
        Guid workingConditionPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetJobProfileWorkingConditionByIdQuery(jobProfilePublicId, workingConditionPublicId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileWorkingConditionResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileWorkingConditionResponse>> Add(
        Guid jobProfilePublicId,
        [FromBody] AddWorkingConditionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddJobProfileWorkingConditionCommand(
                jobProfilePublicId,
                request.WorkConditionTypeCatalogItemPublicId,
                request.CatalogItemPublicId,
                request.Name,
                request.Notes,
                request.SortOrder),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<JobProfileWorkingConditionResponse>.Failure(result.Error));
        }

        this.SetETag(result, value => value.ConcurrencyToken);
        return CreatedAtAction(nameof(GetById), new { jobProfilePublicId, workingConditionPublicId = result.Value.WorkingConditionPublicId }, result.Value);
    }

    [HttpPut("{workingConditionPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileWorkingConditionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileWorkingConditionResponse>> Update(
        Guid jobProfilePublicId,
        Guid workingConditionPublicId,
        [FromHeader(Name = IfMatchHeader.HeaderName)] string? ifMatch,
        [FromBody] UpdateWorkingConditionRequest request,
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
            new UpdateJobProfileWorkingConditionCommand(
                jobProfilePublicId,
                workingConditionPublicId,
                request.WorkConditionTypeCatalogItemPublicId,
                request.CatalogItemPublicId,
                request.Name,
                request.Notes,
                request.SortOrder,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("{workingConditionPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<JobProfileWorkingConditionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileWorkingConditionResponse>> Patch(
        Guid jobProfilePublicId,
        Guid workingConditionPublicId,
        [FromHeader(Name = IfMatchHeader.HeaderName)] string? ifMatch,
        [FromBody] JsonPatchDocument<UpdateWorkingConditionRequest> patchDoc,
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
            new PatchJobProfileWorkingConditionCommand(
                jobProfilePublicId,
                workingConditionPublicId,
                concurrencyToken,
                MapPatchOperations(patchDoc)),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("{workingConditionPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileParentConcurrencyResult>> Remove(
        Guid jobProfilePublicId,
        Guid workingConditionPublicId,
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
            new RemoveJobProfileWorkingConditionCommand(jobProfilePublicId, workingConditionPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    private static IReadOnlyCollection<JobProfileWorkingConditionPatchOperation> MapPatchOperations(JsonPatchDocument<UpdateWorkingConditionRequest> patchDoc) =>
        JsonPatchOperationMapper.Map(
            patchDoc,
            static (op, path, from, value) => new JobProfileWorkingConditionPatchOperation(op, path, from, value));

    public sealed class AddWorkingConditionRequest
    {
        public Guid? WorkConditionTypeCatalogItemPublicId { get; set; }
        public Guid? CatalogItemPublicId { get; set; }
        public string? Name { get; set; }
        public string? Notes { get; set; }
        public int SortOrder { get; set; }
    }

    public sealed class UpdateWorkingConditionRequest
    {
        public Guid? WorkConditionTypeCatalogItemPublicId { get; set; }
        public Guid? CatalogItemPublicId { get; set; }
        public string? Name { get; set; }
        public string? Notes { get; set; }
        public int SortOrder { get; set; }
    }
}
