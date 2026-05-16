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
using CLARIHR.Domain.JobProfiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/job-profiles/{jobProfilePublicId:guid}/requirements")]
[Consumes("application/json")]
[Produces("application/json")]
[Tags("Job Profiles")]
public sealed class JobProfileRequirementsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<JobProfileRequirementResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List requirements of a job profile",
        Description = """
            Returns a paginated list of the requirements defined for the specified
            job profile. Use the `page` and `pageSize` query parameters to
            navigate large collections.
            """)]
    public async Task<ActionResult<PagedResponse<JobProfileRequirementResponse>>> Get(
        Guid jobProfilePublicId,
        [FromQuery(Name = "page")] int pageNumber = 1,
        [Range(1, JobProfileValidationRules.MaxPageSize)]
        [FromQuery] int pageSize = JobProfileValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetJobProfileRequirementsQuery(jobProfilePublicId, pageNumber, pageSize),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("{requirementPublicId:guid}")]
    [ProducesResponseType<JobProfileRequirementResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a job profile requirement by id",
        Description = """
            Returns a single requirement of the specified job profile.

            The `concurrencyToken` in the response is required in the `If-Match`
            header of subsequent `PUT`/`PATCH`/`DELETE` requests to prevent
            lost updates.
            """)]
    public async Task<ActionResult<JobProfileRequirementResponse>> GetById(
        Guid jobProfilePublicId,
        Guid requirementPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetJobProfileRequirementByIdQuery(jobProfilePublicId, requirementPublicId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType<JobProfileRequirementResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Add a requirement to a job profile",
        Description = """
            Creates a new requirement under the specified job profile and returns it
            with a `201 Created` response. The `Location` header points to the
            created resource and the `ETag` header carries its initial
            `concurrencyToken`.
            """)]
    public async Task<ActionResult<JobProfileRequirementResponse>> Add(
        Guid jobProfilePublicId,
        [FromBody] MutateRequirementRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddJobProfileRequirementCommand(
                jobProfilePublicId,
                request.RequirementType,
                request.RequirementTypeCatalogItemPublicId,
                request.CatalogItemPublicId,
                request.CatalogCode,
                request.CatalogName,
                request.Description,
                request.SortOrder),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { jobProfilePublicId, requirementPublicId = value.RequirementPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("{requirementPublicId:guid}")]
    [ProducesResponseType<JobProfileRequirementResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Replace a job profile requirement",
        Description = """
            Replaces all fields of an existing requirement. Requires the `If-Match`
            header with the current `concurrencyToken` to prevent lost updates.
            The new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<JobProfileRequirementResponse>> Update(
        Guid jobProfilePublicId,
        Guid requirementPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] MutateRequirementRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateJobProfileRequirementCommand(
                jobProfilePublicId,
                requirementPublicId,
                request.RequirementType,
                request.RequirementTypeCatalogItemPublicId,
                request.CatalogItemPublicId,
                request.CatalogCode,
                request.CatalogName,
                request.Description,
                request.SortOrder,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("{requirementPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<JobProfileRequirementResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Patch a job profile requirement",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing requirement. Requires the
            `If-Match` header with the current `concurrencyToken`. The new token
            is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<JobProfileRequirementResponse>> Patch(
        Guid jobProfilePublicId,
        Guid requirementPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<MutateRequirementRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchJobProfileRequirementCommand(
                jobProfilePublicId,
                requirementPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new JobProfileRequirementPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("{requirementPublicId:guid}")]
    [ProducesResponseType<JobProfileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a requirement from a job profile",
        Description = """
            Deletes the specified requirement. Requires the `If-Match` header with the
            current `concurrencyToken`. Returns the parent job profile's updated
            concurrency token so the caller can continue mutating the profile
            without an extra round-trip.
            """)]
    public async Task<ActionResult<JobProfileParentConcurrencyResult>> Remove(
        Guid jobProfilePublicId,
        Guid requirementPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new RemoveJobProfileRequirementCommand(jobProfilePublicId, requirementPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    public sealed class MutateRequirementRequest
    {
        public JobRequirementType RequirementType { get; set; }
        public Guid? RequirementTypeCatalogItemPublicId { get; set; }
        public Guid? CatalogItemPublicId { get; set; }
        public string? CatalogCode { get; set; }
        public string? CatalogName { get; set; }
        public string Description { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }
}
