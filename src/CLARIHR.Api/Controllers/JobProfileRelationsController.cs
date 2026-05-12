using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Domain.JobProfiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize(Policy = JobProfilePolicies.Manage)]
[Route("api/v1/job-profiles/{publicId:guid}/relations")]
[Consumes("application/json")]
[Produces("application/json")]
[ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
public sealed class JobProfileRelationsController(
    ICommandDispatcher commandDispatcher) : ControllerBase
{
    private const string ParentConcurrencyTokenHeaderName = "Parent-Concurrency-Token";

    [HttpPost]
    [ProducesResponseType<JobProfileSubResourceResult<JobProfileRelationResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<JobProfileSubResourceResult<JobProfileRelationResponse>>> Add(
        Guid publicId,
        [FromBody] AddRelationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddJobProfileRelationCommand(
                publicId,
                request.RelationType,
                request.CatalogItemId,
                request.Counterpart,
                request.Notes,
                request.SortOrder,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("{relationPublicId:guid}")]
    [ProducesResponseType<JobProfileSubResourceResult<JobProfileRelationResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<JobProfileSubResourceResult<JobProfileRelationResponse>>> Update(
        Guid publicId,
        Guid relationPublicId,
        [FromBody] UpdateRelationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateJobProfileRelationCommand(
                publicId,
                relationPublicId,
                request.RelationType,
                request.CatalogItemId,
                request.Counterpart,
                request.Notes,
                request.SortOrder,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpDelete("{relationPublicId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(
        Guid publicId,
        Guid relationPublicId,
        [FromHeader(Name = IfMatchHeader.HeaderName)] string? ifMatch,
        CancellationToken cancellationToken = default)
    {
        if (!IfMatchHeader.TryParseConcurrencyToken(ifMatch, out var concurrencyToken))
        {
            return BadRequest(ProblemDetailsFactory.CreateProblemDetails(HttpContext, statusCode: StatusCodes.Status400BadRequest, detail: IfMatchHeader.MissingDetail));
        }

        var result = await commandDispatcher.SendAsync(
            new RemoveJobProfileRelationCommand(publicId, relationPublicId, concurrencyToken),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(result).Result!;
        }

        Response.Headers[ParentConcurrencyTokenHeaderName] = result.Value.ParentConcurrencyToken.ToString();
        return NoContent();
    }

    public sealed class AddRelationRequest
    {
        public JobRelationType RelationType { get; init; }
        public Guid? CatalogItemId { get; init; }
        public string Counterpart { get; init; } = string.Empty;
        public string? Notes { get; init; }
        public int SortOrder { get; init; }
        public Guid ConcurrencyToken { get; init; }
    }

    public sealed class UpdateRelationRequest
    {
        public JobRelationType RelationType { get; init; }
        public Guid? CatalogItemId { get; init; }
        public string Counterpart { get; init; } = string.Empty;
        public string? Notes { get; init; }
        public int SortOrder { get; init; }
        public Guid ConcurrencyToken { get; init; }
    }

}
