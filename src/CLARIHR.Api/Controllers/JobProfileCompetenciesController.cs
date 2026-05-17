using Asp.Versioning;
using System.ComponentModel.DataAnnotations;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.Pagination;
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
[Route("api/v{version:apiVersion}/job-profiles/{jobProfilePublicId:guid}/competencies")]
[Consumes("application/json")]
[Produces("application/json")]
[Tags("Job Profiles")]
[AuthorizationPolicySet(JobProfilePolicies.Read, JobProfilePolicies.Manage)]
public sealed class JobProfileCompetenciesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<JobProfileLegacyCompetencyResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List competencies of a job profile",
        Description = """
            Returns a paginated list of the competencies defined for the specified
            job profile. Use the `page` and `pageSize` query parameters to
            navigate large collections.
            """)]
    public async Task<ActionResult<PagedResponse<JobProfileLegacyCompetencyResponse>>> Get(
        Guid jobProfilePublicId,
        [FromQuery(Name = "page")] int pageNumber = 1,
        [Range(1, JobProfileValidationRules.MaxPageSize)]
        [FromQuery] int pageSize = JobProfileValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetJobProfileCompetenciesQuery(jobProfilePublicId, pageNumber, pageSize),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("{competencyPublicId:guid}")]
    [ProducesResponseType<JobProfileLegacyCompetencyResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a job profile competency by id",
        Description = """
            Returns a single competency of the specified job profile.

            The `concurrencyToken` in the response is required in the `If-Match`
            header of subsequent `PUT`/`PATCH`/`DELETE` requests to prevent
            lost updates.
            """)]
    public async Task<ActionResult<JobProfileLegacyCompetencyResponse>> GetById(
        Guid jobProfilePublicId,
        Guid competencyPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetJobProfileCompetencyByIdQuery(jobProfilePublicId, competencyPublicId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType<JobProfileLegacyCompetencyResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Add a competency to a job profile",
        Description = """
            Creates a new competency under the specified job profile and returns it
            with a `201 Created` response. The `Location` header points to the
            created resource and the `ETag` header carries its initial
            `concurrencyToken`.
            """)]
    public async Task<ActionResult<JobProfileLegacyCompetencyResponse>> Add(
        Guid jobProfilePublicId,
        [FromBody] MutateCompetencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddJobProfileCompetencyCommand(
                jobProfilePublicId,
                request.CatalogItemPublicId,
                request.Name,
                request.ExpectedLevel,
                request.Notes,
                request.SortOrder),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { jobProfilePublicId, competencyPublicId = value.CompetencyPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("{competencyPublicId:guid}")]
    [ProducesResponseType<JobProfileLegacyCompetencyResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Replace a job profile competency",
        Description = """
            Replaces all fields of an existing competency. Requires the `If-Match`
            header with the current `concurrencyToken` to prevent lost updates.
            The new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<JobProfileLegacyCompetencyResponse>> Update(
        Guid jobProfilePublicId,
        Guid competencyPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] MutateCompetencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateJobProfileCompetencyCommand(
                jobProfilePublicId,
                competencyPublicId,
                request.CatalogItemPublicId,
                request.Name,
                request.ExpectedLevel,
                request.Notes,
                request.SortOrder,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("{competencyPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<JobProfileLegacyCompetencyResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Patch a job profile competency",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing competency. Requires the
            `If-Match` header with the current `concurrencyToken`. The new token
            is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<JobProfileLegacyCompetencyResponse>> Patch(
        Guid jobProfilePublicId,
        Guid competencyPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<MutateCompetencyRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchJobProfileCompetencyCommand(
                jobProfilePublicId,
                competencyPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new JobProfileCompetencyPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("{competencyPublicId:guid}")]
    [ProducesResponseType<JobProfileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a competency from a job profile",
        Description = """
            Deletes the specified competency. Requires the `If-Match` header with the
            current `concurrencyToken`. Returns the parent job profile's updated
            concurrency token so the caller can continue mutating the profile
            without an extra round-trip.
            """)]
    public async Task<ActionResult<JobProfileParentConcurrencyResult>> Remove(
        Guid jobProfilePublicId,
        Guid competencyPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new RemoveJobProfileCompetencyCommand(jobProfilePublicId, competencyPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    public sealed class MutateCompetencyRequest
    {
        public Guid? CatalogItemPublicId { get; set; }
        public string? Name { get; set; }
        public string? ExpectedLevel { get; set; }
        public string? Notes { get; set; }
        public int SortOrder { get; set; }
    }
}
