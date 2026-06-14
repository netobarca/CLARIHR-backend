using Asp.Versioning;
using System.ComponentModel.DataAnnotations;
using CLARIHR.Api.Authorization;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
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
[Route("api/v{version:apiVersion}/job-profiles/{jobProfilePublicId:guid}/dependent-positions")]
[Consumes("application/json")]
[Produces("application/json")]
[Tags("Job Profiles")]
[AuthorizationPolicySet(JobProfilePolicies.Read, JobProfilePolicies.Manage)]
[ResourceActions(JobProfilePermissionCodes.ResourceKey)]
public sealed class JobProfileDependentPositionsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<JobProfileDependentPositionResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List dependent positions of a job profile",
        Description = """
            Returns a paginated list of the dependent positions defined for the specified
            job profile. Use the `page` and `pageSize` query parameters to
            navigate large collections.
            """)]
    public async Task<ActionResult<PagedResponse<JobProfileDependentPositionResponse>>> Get(
        Guid jobProfilePublicId,
        [FromQuery(Name = "page")] int pageNumber = 1,
        [Range(1, JobProfileValidationRules.MaxPageSize)]
        [FromQuery] int pageSize = JobProfileValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetJobProfileDependentPositionsQuery(jobProfilePublicId, pageNumber, pageSize),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("{dependentPositionPublicId:guid}")]
    [ProducesResponseType<JobProfileDependentPositionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a job profile dependent position by id",
        Description = """
            Returns a single dependent position of the specified job profile.

            The `concurrencyToken` in the response is required in the `If-Match`
            header of subsequent `PUT`/`PATCH`/`DELETE` requests to prevent
            lost updates.
            """)]
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
    [ProducesResponseType<JobProfileDependentPositionResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Add a dependent position to a job profile",
        Description = """
            Creates a new dependent position under the specified job profile and returns it
            with a `201 Created` response. The `Location` header points to the
            created resource and the `ETag` header carries its initial
            `concurrencyToken`.
            """)]
    public async Task<ActionResult<JobProfileDependentPositionResponse>> Add(
        Guid jobProfilePublicId,
        [FromBody] MutateDependentPositionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddJobProfileDependentPositionCommand(
                jobProfilePublicId,
                request.DependentJobProfilePublicId,
                request.Quantity,
                request.Notes),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { jobProfilePublicId, dependentPositionPublicId = value.DependentPositionPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("{dependentPositionPublicId:guid}")]
    [ProducesResponseType<JobProfileDependentPositionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Replace a job profile dependent position",
        Description = """
            Replaces all fields of an existing dependent position. Requires the `If-Match`
            header with the current `concurrencyToken` to prevent lost updates.
            The new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<JobProfileDependentPositionResponse>> Update(
        Guid jobProfilePublicId,
        Guid dependentPositionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] MutateDependentPositionRequest request,
        CancellationToken cancellationToken = default)
    {
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
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<JobProfileDependentPositionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Patch a job profile dependent position",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing dependent position. Requires the
            `If-Match` header with the current `concurrencyToken`. The new token
            is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<JobProfileDependentPositionResponse>> Patch(
        Guid jobProfilePublicId,
        Guid dependentPositionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<MutateDependentPositionRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchJobProfileDependentPositionCommand(
                jobProfilePublicId,
                dependentPositionPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new JobProfileDependentPositionPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("{dependentPositionPublicId:guid}")]
    [ProducesResponseType<JobProfileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a dependent position from a job profile",
        Description = """
            Deletes the specified dependent position. Requires the `If-Match` header with the
            current `concurrencyToken`. Returns the parent job profile's updated
            concurrency token so the caller can continue mutating the profile
            without an extra round-trip.
            """)]
    public async Task<ActionResult<JobProfileParentConcurrencyResult>> Remove(
        Guid jobProfilePublicId,
        Guid dependentPositionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new RemoveJobProfileDependentPositionCommand(jobProfilePublicId, dependentPositionPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    public sealed class MutateDependentPositionRequest
    {
        public Guid DependentJobProfilePublicId { get; set; }
        public int Quantity { get; set; }
        public string? Notes { get; set; }
    }
}
