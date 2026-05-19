using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.JobProfileCatalogTypes;
using CLARIHR.Application.Features.JobProfileCatalogTypes.Common;
using CLARIHR.Backoffice.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobProfileCatalogTypeResponse>> Create(
        [FromBody] CreateJobProfileCatalogTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateJobProfileCatalogTypeCommand(request.Code, request.Name, request.SortOrder);
        var result = await commandDispatcher.SendAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    // PUT api/platform/job-profile-catalog-types/{id}
    // Code is immutable (Q3): the request body intentionally omits it.
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(JobProfileCatalogTypeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobProfileCatalogTypeResponse>> Update(
        Guid id,
        [FromBody] UpdateJobProfileCatalogTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateJobProfileCatalogTypeCommand(
            id, request.Name, request.SortOrder, request.ConcurrencyToken);
        var result = await commandDispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }

    // PATCH api/platform/job-profile-catalog-types/{id}/activate
    [HttpPatch("{id:guid}/activate")]
    [ProducesResponseType(typeof(JobProfileCatalogTypeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobProfileCatalogTypeResponse>> Activate(
        Guid id,
        [FromBody] ConcurrencyTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new ActivateJobProfileCatalogTypeCommand(id, request.ConcurrencyToken);
        var result = await commandDispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }

    // PATCH api/platform/job-profile-catalog-types/{id}/inactivate
    [HttpPatch("{id:guid}/inactivate")]
    [ProducesResponseType(typeof(JobProfileCatalogTypeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobProfileCatalogTypeResponse>> Inactivate(
        Guid id,
        [FromBody] ConcurrencyTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new InactivateJobProfileCatalogTypeCommand(id, request.ConcurrencyToken);
        var result = await commandDispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }
}

// ─── Request contracts ────────────────────────────────────────────────────────

public sealed record CreateJobProfileCatalogTypeRequest(string Code, string Name, int SortOrder);

// Code intentionally absent: the key is immutable once created (Q3).
public sealed record UpdateJobProfileCatalogTypeRequest(
    string Name,
    int SortOrder,
    Guid ConcurrencyToken);
