using System.Text.Json;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Domain.JobProfiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/companies/{companyId:guid}/job-catalogs/{category}")]
[Consumes("application/json")]
[Produces("application/json")]
public sealed class JobCatalogsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<JobCatalogItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    public async Task<ActionResult<PagedResponse<JobCatalogItemResponse>>> Get(
        Guid companyId,
        JobCatalogCategory category,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
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
    public async Task<ActionResult<JobCatalogItemResponse>> Add(
        Guid companyId,
        JobCatalogCategory category,
        [FromBody] CreateJobCatalogItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateJobCatalogItemCommand(companyId, category, request.Code, request.Name),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(result).Result!;
        }

        return Created($"{Request.Path}/{result.Value.Id:D}", result.Value);
    }

    [HttpPut("{jobCatalogPublicId:guid}")]
    [ProducesResponseType<JobCatalogItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobCatalogItemResponse>> Update(
        Guid companyId,
        JobCatalogCategory category,
        Guid jobCatalogPublicId,
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
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("{jobCatalogPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [ProducesResponseType<JobCatalogItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobCatalogItemResponse>> Patch(
        Guid companyId,
        JobCatalogCategory category,
        Guid jobCatalogPublicId,
        [FromBody] JsonPatchDocument<UpdateJobCatalogItemRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        if (patchDoc is null)
        {
            return BadRequest(ProblemDetailsFactory.CreateProblemDetails(
                HttpContext,
                statusCode: StatusCodes.Status400BadRequest,
                detail: "Invalid patch document."));
        }

        var result = await commandDispatcher.SendAsync(
            new PatchJobCatalogItemCommand(
                companyId,
                category,
                jobCatalogPublicId,
                MapPatchOperations(patchDoc)),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpDelete("{jobCatalogPublicId:guid}")]
    [ProducesResponseType<JobCatalogItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobCatalogItemResponse>> Remove(
        Guid companyId,
        JobCatalogCategory category,
        Guid jobCatalogPublicId,
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
            new RemoveJobCatalogItemCommand(companyId, category, jobCatalogPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    private static IReadOnlyCollection<JobCatalogItemPatchOperation> MapPatchOperations(JsonPatchDocument<UpdateJobCatalogItemRequest> patchDoc) =>
        patchDoc.Operations
            .Select(operation => new JobCatalogItemPatchOperation(
                operation.op,
                operation.path,
                operation.from,
                MapPatchValue(operation.value)))
            .ToArray();

    private static JsonElement? MapPatchValue(object? value)
    {
        if (value is null)
        {
            return JsonSerializer.SerializeToElement<object?>(null);
        }

        if (value is JToken token)
        {
            using var document = JsonDocument.Parse(token.ToString(Newtonsoft.Json.Formatting.None));
            return document.RootElement.Clone();
        }

        return JsonSerializer.SerializeToElement(value, value.GetType());
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
        public Guid ConcurrencyToken { get; set; }
    }
}
