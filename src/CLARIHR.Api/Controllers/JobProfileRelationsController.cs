using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Domain.JobProfiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/job-profiles/{jobProfilePublicId:guid}/relations")]
[Consumes("application/json")]
[Produces("application/json")]
public sealed class JobProfileRelationsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = JobProfilePolicies.Read)]
    [ProducesResponseType<IReadOnlyCollection<JobProfileRelationResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    public async Task<ActionResult<IReadOnlyCollection<JobProfileRelationResponse>>> Get(
        Guid jobProfilePublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetJobProfileRelationsQuery(jobProfilePublicId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("{relationPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Read)]
    [ProducesResponseType<JobProfileRelationResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    public async Task<ActionResult<JobProfileRelationResponse>> GetById(
        Guid jobProfilePublicId,
        Guid relationPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetJobProfileRelationByIdQuery(jobProfilePublicId, relationPublicId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileRelationResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileRelationResponse>> Add(
        Guid jobProfilePublicId,
        [FromBody] AddRelationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddJobProfileRelationCommand(
                jobProfilePublicId,
                request.RelationType,
                request.CatalogItemPublicId,
                request.Counterpart,
                request.Notes,
                request.SortOrder),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<JobProfileRelationResponse>.Failure(result.Error));
        }

        this.SetETag(result, value => value.ConcurrencyToken);
        return CreatedAtAction(nameof(GetById), new { jobProfilePublicId, relationPublicId = result.Value.RelationPublicId }, result.Value);
    }

    [HttpPut("{relationPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileRelationResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileRelationResponse>> Update(
        Guid jobProfilePublicId,
        Guid relationPublicId,
        [FromHeader(Name = IfMatchHeader.HeaderName)] string? ifMatch,
        [FromBody] UpdateRelationRequest request,
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
            new UpdateJobProfileRelationCommand(
                jobProfilePublicId,
                relationPublicId,
                request.RelationType,
                request.CatalogItemPublicId,
                request.Counterpart,
                request.Notes,
                request.SortOrder,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("{relationPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<JobProfileRelationResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileRelationResponse>> Patch(
        Guid jobProfilePublicId,
        Guid relationPublicId,
        [FromHeader(Name = IfMatchHeader.HeaderName)] string? ifMatch,
        [FromBody] JsonPatchDocument<UpdateRelationRequest> patchDoc,
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
            new PatchJobProfileRelationCommand(
                jobProfilePublicId,
                relationPublicId,
                concurrencyToken,
                MapPatchOperations(patchDoc)),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("{relationPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileParentConcurrencyResult>> Remove(
        Guid jobProfilePublicId,
        Guid relationPublicId,
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
            new RemoveJobProfileRelationCommand(jobProfilePublicId, relationPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    private static IReadOnlyCollection<JobProfileRelationPatchOperation> MapPatchOperations(JsonPatchDocument<UpdateRelationRequest> patchDoc) =>
        JsonPatchOperationMapper.Map(
            patchDoc,
            static (op, path, from, value) => new JobProfileRelationPatchOperation(op, path, from, value));

    public sealed class AddRelationRequest
    {
        public JobRelationType RelationType { get; set; }
        public Guid? CatalogItemPublicId { get; set; }
        public string Counterpart { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public int SortOrder { get; set; }
    }

    public sealed class UpdateRelationRequest
    {
        public JobRelationType RelationType { get; set; }
        public Guid? CatalogItemPublicId { get; set; }
        public string Counterpart { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public int SortOrder { get; set; }
    }
}
