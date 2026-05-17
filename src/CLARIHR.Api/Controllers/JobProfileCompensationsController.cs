using Asp.Versioning;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.JobProfiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/job-profiles/{jobProfilePublicId:guid}/compensations")]
[Consumes("application/json")]
[Produces("application/json")]
[Tags("Job Profiles")]
[AuthorizationPolicySet(JobProfilePolicies.Read, JobProfilePolicies.Manage)]
public sealed class JobProfileCompensationsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<JobProfileCompensationItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List compensation items of a job profile",
        Description = """
            Returns the full list of compensation items defined for the
            specified job profile.
            """)]
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
    [ProducesResponseType<JobProfileCompensationItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a job profile compensation item by id",
        Description = """
            Returns a single compensation item of the specified job profile.

            The `concurrencyToken` in the response is required in the `If-Match`
            header of subsequent `PUT`/`PATCH`/`DELETE` requests to prevent
            lost updates.
            """)]
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
    [ProducesResponseType<JobProfileCompensationItemResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Add a compensation item to a job profile",
        Description = """
            Creates a new compensation item under the specified job profile and returns it
            with a `201 Created` response. The `Location` header points to the
            created resource and the `ETag` header carries its initial
            `concurrencyToken`.
            """)]
    public async Task<ActionResult<JobProfileCompensationItemResponse>> Add(
        Guid jobProfilePublicId,
        [FromBody] MutateCompensationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddJobProfileCompensationCommand(
                jobProfilePublicId,
                request.SalaryTabulatorLinePublicId,
                request.Notes),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { jobProfilePublicId, compensationPublicId = value.CompensationPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("{compensationPublicId:guid}")]
    [ProducesResponseType<JobProfileCompensationItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Replace a job profile compensation item",
        Description = """
            Replaces all fields of an existing compensation item. Requires the `If-Match`
            header with the current `concurrencyToken` to prevent lost updates.
            The new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<JobProfileCompensationItemResponse>> Update(
        Guid jobProfilePublicId,
        Guid compensationPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] MutateCompensationRequest request,
        CancellationToken cancellationToken = default)
    {
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
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<JobProfileCompensationItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Patch a job profile compensation item",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing compensation item. Requires the
            `If-Match` header with the current `concurrencyToken`. The new token
            is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<JobProfileCompensationItemResponse>> Patch(
        Guid jobProfilePublicId,
        Guid compensationPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<MutateCompensationRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchJobProfileCompensationCommand(
                jobProfilePublicId,
                compensationPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new JobProfileCompensationPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("{compensationPublicId:guid}")]
    [ProducesResponseType<JobProfileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a compensation item from a job profile",
        Description = """
            Deletes the specified compensation item. Requires the `If-Match` header with the
            current `concurrencyToken`. Returns the parent job profile's updated
            concurrency token so the caller can continue mutating the profile
            without an extra round-trip.
            """)]
    public async Task<ActionResult<JobProfileParentConcurrencyResult>> Remove(
        Guid jobProfilePublicId,
        Guid compensationPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new RemoveJobProfileCompensationCommand(jobProfilePublicId, compensationPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    public sealed class MutateCompensationRequest
    {
        public Guid SalaryTabulatorLinePublicId { get; set; }
        public string? Notes { get; set; }
    }
}
