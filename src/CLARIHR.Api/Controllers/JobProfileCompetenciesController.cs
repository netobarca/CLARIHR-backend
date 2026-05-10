using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.JobProfiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/job-profiles/{publicId:guid}/competencies")]
public sealed class JobProfileCompetenciesController(
    ICommandDispatcher commandDispatcher) : ControllerBase
{
    private const string ParentConcurrencyTokenHeaderName = "Parent-Concurrency-Token";

    [HttpPost]
    [ProducesResponseType<JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>>> Add(
        Guid publicId,
        [FromBody] AddCompetencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddJobProfileCompetencyCommand(
                publicId,
                request.CatalogItemId,
                request.Name,
                request.ExpectedLevel,
                request.Notes,
                request.SortOrder,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("{competencyPublicId:guid}")]
    [ProducesResponseType<JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobProfileSubResourceResult<JobProfileLegacyCompetencyResponse>>> Update(
        Guid publicId,
        Guid competencyPublicId,
        [FromBody] UpdateCompetencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateJobProfileCompetencyCommand(
                publicId,
                competencyPublicId,
                request.CatalogItemId,
                request.Name,
                request.ExpectedLevel,
                request.Notes,
                request.SortOrder,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpDelete("{competencyPublicId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remove(
        Guid publicId,
        Guid competencyPublicId,
        [FromBody] ConcurrencyTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new RemoveJobProfileCompetencyCommand(publicId, competencyPublicId, request.ConcurrencyToken),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(result).Result!;
        }

        Response.Headers[ParentConcurrencyTokenHeaderName] = result.Value.ParentConcurrencyToken.ToString();
        return NoContent();
    }

    public sealed class AddCompetencyRequest
    {
        public Guid? CatalogItemId { get; init; }
        public string? Name { get; init; }
        public string? ExpectedLevel { get; init; }
        public string? Notes { get; init; }
        public int SortOrder { get; init; }
        public Guid ConcurrencyToken { get; init; }
    }

    public sealed class UpdateCompetencyRequest
    {
        public Guid? CatalogItemId { get; init; }
        public string? Name { get; init; }
        public string? ExpectedLevel { get; init; }
        public string? Notes { get; init; }
        public int SortOrder { get; init; }
        public Guid ConcurrencyToken { get; init; }
    }

    public sealed class ConcurrencyTokenRequest
    {
        public Guid ConcurrencyToken { get; init; }
    }
}
