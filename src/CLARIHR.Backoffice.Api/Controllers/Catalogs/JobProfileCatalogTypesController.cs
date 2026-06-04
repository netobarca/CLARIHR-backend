using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.JobProfileCatalogTypes;
using CLARIHR.Application.Features.JobProfileCatalogTypes.Common;
using CLARIHR.Backoffice.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Backoffice.Api.Controllers.Catalogs;

[ApiController]
[Route("api/platform/job-profile-catalog-types")]
[Authorize(Policy = "PlatformOperator")]
[Tags("Job Profile Catalog Types")]
public sealed class JobProfileCatalogTypesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher)
    : ControllerBase
{
    // GET api/platform/job-profile-catalog-types
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<JobProfileCatalogTypeResponse>), StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "Search Job Profile catalog types",
        Description = "Returns a paged list of system-wide Job Profile catalog types.")]
    public async Task<ActionResult<PagedResponse<JobProfileCatalogTypeResponse>>> Search(
        [FromQuery] bool? isActive,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = JobProfileCatalogTypeValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var query = new SearchJobProfileCatalogTypesQuery(isActive, search, pageNumber, pageSize);
        var result = await queryDispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }

    // GET api/platform/job-profile-catalog-types/{id}
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(JobProfileCatalogTypeResponse), StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a Job Profile catalog type",
        Description = "Returns a single Job Profile catalog type. The current `concurrencyToken` is included in the body for use in the `If-Match` header of a subsequent update.")]
    public async Task<ActionResult<JobProfileCatalogTypeResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var query = new GetJobProfileCatalogTypeByIdQuery(id);
        var result = await queryDispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }

    // POST api/platform/job-profile-catalog-types
    [HttpPost]
    [ProducesResponseType(typeof(JobProfileCatalogTypeResponse), StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Query | StandardErrorSet.Conflict)]
    [SwaggerOperation(
        Summary = "Create a Job Profile catalog type",
        Description = "Creates a Job Profile catalog type. Returns `201`; the current `concurrencyToken` is included in the body and the `ETag` header.")]
    public async Task<ActionResult<JobProfileCatalogTypeResponse>> Create(
        [FromBody] CreateJobProfileCatalogTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateJobProfileCatalogTypeCommand(request.Code, request.Name, request.SortOrder);
        var result = await commandDispatcher.SendAsync(command, cancellationToken);

        // The GetById route parameter `id` (Guid) is rewritten to `publicId` by
        // PublicContractRouteConvention, so the generated-URL route value must use that external name.
        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    // PUT api/platform/job-profile-catalog-types/{id}
    // Code is immutable (Q3): the request body intentionally omits it.
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(JobProfileCatalogTypeResponse), StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Update a Job Profile catalog type",
        Description = "Replaces the editable fields (name, sort order). The code is immutable and is not accepted. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<JobProfileCatalogTypeResponse>> Update(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateJobProfileCatalogTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateJobProfileCatalogTypeCommand(
            id, request.Name, request.SortOrder, concurrencyToken);
        var result = await commandDispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    // PATCH api/platform/job-profile-catalog-types/{id}
    [HttpPatch("{id:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType(typeof(JobProfileCatalogTypeResponse), StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Patch a Job Profile catalog type (RFC 6902 JSON Patch)",
        Description = "Applies a partial update using JSON Patch (RFC 6902), media type `application/json-patch+json`. Patchable paths: `/name`, `/sortOrder`. The code is immutable; activation state changes use the `/activate` and `/inactivate` actions. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<JobProfileCatalogTypeResponse>> Patch(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchJobProfileCatalogTypeRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var command = new PatchJobProfileCatalogTypeCommand(
            id,
            concurrencyToken,
            JsonPatchOperationMapper.Map(
                patchDoc,
                static (op, path, from, value) => new JobProfileCatalogTypePatchOperation(op, path, from, value)));
        var result = await commandDispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    // PATCH api/platform/job-profile-catalog-types/{id}/activate
    [HttpPatch("{id:guid}/activate")]
    [ProducesResponseType(typeof(JobProfileCatalogTypeResponse), StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Activate a Job Profile catalog type",
        Description = "Activates the catalog type. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<JobProfileCatalogTypeResponse>> Activate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var command = new ActivateJobProfileCatalogTypeCommand(id, concurrencyToken);
        var result = await commandDispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    // PATCH api/platform/job-profile-catalog-types/{id}/inactivate
    [HttpPatch("{id:guid}/inactivate")]
    [ProducesResponseType(typeof(JobProfileCatalogTypeResponse), StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Inactivate a Job Profile catalog type",
        Description = "Inactivates the catalog type. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<JobProfileCatalogTypeResponse>> Inactivate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var command = new InactivateJobProfileCatalogTypeCommand(id, concurrencyToken);
        var result = await commandDispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }
}

// ─── Request contracts ────────────────────────────────────────────────────────

public sealed record CreateJobProfileCatalogTypeRequest(string Code, string Name, int SortOrder);

// Code intentionally absent: the key is immutable once created (Q3).
public sealed record UpdateJobProfileCatalogTypeRequest(
    string Name,
    int SortOrder);

// Code intentionally absent: it is immutable and the applier rejects a /code patch.
public sealed class PatchJobProfileCatalogTypeRequest
{
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}
