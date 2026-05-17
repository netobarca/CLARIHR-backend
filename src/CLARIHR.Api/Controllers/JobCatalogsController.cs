using Asp.Versioning;
using System.ComponentModel.DataAnnotations;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
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
[Route("api/v{version:apiVersion}/companies/{companyId:guid}/job-catalogs/{category}")]
[Consumes("application/json")]
[Produces("application/json")]
[Tags("Job Profiles")]
[AuthorizationPolicySet(JobProfilePolicies.Read, JobProfilePolicies.ManageCatalogs)]
public sealed class JobCatalogsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<JobCatalogItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List job catalog items in a category",
        Description = """
            Returns a paginated list of the job catalog items for the category
            given in the `{category}` path segment. Use the `page` and `pageSize`
            query parameters to navigate large collections.

            Filter with `isActive` and a free-text query (`q`) matched against
            code and name. Set `includeAllowedActions=true` to include, per item,
            the operations the current user is authorized to perform on it.

            These catalog items are the values that the job profile **Inline
            Catalog Create** flow (`allowInlineCatalogCreate` on the job profile
            create/update endpoints) can auto-create; this endpoint itself only
            lists existing items and never creates them.
            """)]
    public async Task<ActionResult<PagedResponse<JobCatalogItemResponse>>> Get(
        Guid companyId,
        JobCatalogCategory category,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [Range(1, JobProfileValidationRules.MaxPageSize)]
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchJobCatalogItemsQuery(companyId, category, isActive, search, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType<JobCatalogItemResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Add a job catalog item to a category",
        Description = """
            Creates a new job catalog item (`code` + `name`) under the category
            given in the `{category}` path segment and returns it with a
            `201 Created` response. The `Location` header points to the created
            resource and the `ETag` header carries its initial `concurrencyToken`.

            The `code` must be unique within the category. This is the explicit
            catalog-management entry point; the job profile **Inline Catalog
            Create** flow (`allowInlineCatalogCreate`) is a separate path that can
            auto-create the same kind of item while saving a job profile.
            """)]
    public async Task<ActionResult<JobCatalogItemResponse>> Add(
        Guid companyId,
        JobCatalogCategory category,
        [FromBody] CreateJobCatalogItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateJobCatalogItemCommand(companyId, category, request.Code, request.Name),
            cancellationToken);

        return this.ToCreatedResult(
            result,
            value => $"{Request.Path}/{value.Id:D}",
            value => value.ConcurrencyToken);
    }

    [HttpPut("{jobCatalogPublicId:guid}")]
    [ProducesResponseType<JobCatalogItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Replace a job catalog item",
        Description = """
            Replaces all fields (`code`, `name`, `isActive`) of an existing job
            catalog item. Requires the `If-Match` header with the current
            `concurrencyToken` to prevent lost updates. The new token is returned
            in the `ETag` header.

            System items cannot be modified and the `code` must remain unique
            within the category.
            """)]
    public async Task<ActionResult<JobCatalogItemResponse>> Update(
        Guid companyId,
        JobCatalogCategory category,
        Guid jobCatalogPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateJobCatalogItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateJobCatalogItemCommand(
                companyId,
                category,
                jobCatalogPublicId,
                request.Code,
                request.Name,
                request.IsActive,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("{jobCatalogPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<JobCatalogItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Patch a job catalog item",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to the `code`, `name` or `isActive`
            fields of an existing job catalog item. Requires the `If-Match`
            header with the current `concurrencyToken`. The new token is
            returned in the `ETag` header.

            System items cannot be modified and the `code` must remain unique
            within the category.
            """)]
    public async Task<ActionResult<JobCatalogItemResponse>> Patch(
        Guid companyId,
        JobCatalogCategory category,
        Guid jobCatalogPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<UpdateJobCatalogItemRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchJobCatalogItemCommand(
                companyId,
                category,
                jobCatalogPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new JobCatalogItemPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("{jobCatalogPublicId:guid}")]
    [ProducesResponseType<JobCatalogItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a job catalog item from a category",
        Description = """
            Deletes the specified job catalog item. Requires the `If-Match`
            header with the current `concurrencyToken`. System items are
            rejected, and an item still referenced by existing job profiles
            cannot be deleted (a usage check is enforced).
            """)]
    public async Task<ActionResult<JobCatalogItemResponse>> Remove(
        Guid companyId,
        JobCatalogCategory category,
        Guid jobCatalogPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new RemoveJobCatalogItemCommand(companyId, category, jobCatalogPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed class CreateJobCatalogItemRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public sealed class UpdateJobCatalogItemRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
