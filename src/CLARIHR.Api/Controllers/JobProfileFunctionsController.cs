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
[Route("api/v{version:apiVersion}/job-profiles/{jobProfilePublicId:guid}/functions")]
[Consumes("application/json")]
[Produces("application/json")]
[Tags("Job Profiles")]
public sealed class JobProfileFunctionsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<JobProfileFunctionResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List functions of a job profile",
        Description = """
            Returns a paginated list of the functions defined for the specified
            job profile. Use the `page` and `pageSize` query parameters to
            navigate large collections.
            """)]
    public async Task<ActionResult<PagedResponse<JobProfileFunctionResponse>>> Get(
        Guid jobProfilePublicId,
        [FromQuery(Name = "page")] int pageNumber = 1,
        [Range(1, JobProfileValidationRules.MaxPageSize)]
        [FromQuery] int pageSize = JobProfileValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetJobProfileFunctionsQuery(jobProfilePublicId, pageNumber, pageSize),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("{functionPublicId:guid}")]
    [ProducesResponseType<JobProfileFunctionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a job profile function by id",
        Description = """
            Returns a single function of the specified job profile.

            The `concurrencyToken` in the response is required in the `If-Match`
            header of subsequent `PUT`/`PATCH`/`DELETE` requests to prevent
            lost updates.
            """)]
    public async Task<ActionResult<JobProfileFunctionResponse>> GetById(
        Guid jobProfilePublicId,
        Guid functionPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetJobProfileFunctionByIdQuery(jobProfilePublicId, functionPublicId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType<JobProfileFunctionResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Add a function to a job profile",
        Description = """
            Creates a new function under the specified job profile and returns it
            with a `201 Created` response. The `Location` header points to the
            created resource and the `ETag` header carries its initial
            `concurrencyToken`.
            """)]
    public async Task<ActionResult<JobProfileFunctionResponse>> Add(
        Guid jobProfilePublicId,
        [FromBody] MutateFunctionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddJobProfileFunctionCommand(
                jobProfilePublicId,
                request.FunctionType,
                request.FrequencyCatalogItemPublicId,
                request.Description,
                request.SortOrder),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { jobProfilePublicId, functionPublicId = value.FunctionPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("{functionPublicId:guid}")]
    [ProducesResponseType<JobProfileFunctionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Replace a job profile function",
        Description = """
            Replaces all fields of an existing function. Requires the `If-Match`
            header with the current `concurrencyToken` to prevent lost updates.
            The new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<JobProfileFunctionResponse>> Update(
        Guid jobProfilePublicId,
        Guid functionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] MutateFunctionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateJobProfileFunctionCommand(
                jobProfilePublicId,
                functionPublicId,
                request.FunctionType,
                request.FrequencyCatalogItemPublicId,
                request.Description,
                request.SortOrder,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("{functionPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<JobProfileFunctionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Patch a job profile function",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing function. Requires the
            `If-Match` header with the current `concurrencyToken`. The new token
            is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<JobProfileFunctionResponse>> Patch(
        Guid jobProfilePublicId,
        Guid functionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<MutateFunctionRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchJobProfileFunctionCommand(
                jobProfilePublicId,
                functionPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new JobProfileFunctionPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("{functionPublicId:guid}")]
    [ProducesResponseType<JobProfileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a function from a job profile",
        Description = """
            Deletes the specified function. Requires the `If-Match` header with the
            current `concurrencyToken`. Returns the parent job profile's updated
            concurrency token so the caller can continue mutating the profile
            without an extra round-trip.
            """)]
    public async Task<ActionResult<JobProfileParentConcurrencyResult>> Remove(
        Guid jobProfilePublicId,
        Guid functionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new RemoveJobProfileFunctionCommand(jobProfilePublicId, functionPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    public sealed class MutateFunctionRequest
    {
        public JobFunctionType FunctionType { get; set; }
        public Guid? FrequencyCatalogItemPublicId { get; set; }
        public string Description { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }
}
