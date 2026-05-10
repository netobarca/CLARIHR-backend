using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Domain.JobProfiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/job-profiles/{publicId:guid}/requirements")]
public sealed class JobProfileRequirementsController(
    ICommandDispatcher commandDispatcher) : ControllerBase
{
    private const string ParentConcurrencyTokenHeaderName = "Parent-Concurrency-Token";

    [HttpPost]
    [ProducesResponseType<JobProfileSubResourceResult<JobProfileRequirementResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobProfileSubResourceResult<JobProfileRequirementResponse>>> Add(
        Guid publicId,
        [FromBody] AddRequirementRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddJobProfileRequirementCommand(
                publicId,
                request.RequirementType,
                request.RequirementTypeCatalogItemId,
                request.CatalogItemId,
                request.CatalogCode,
                request.CatalogName,
                request.Description,
                request.SortOrder,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("{requirementPublicId:guid}")]
    [ProducesResponseType<JobProfileSubResourceResult<JobProfileRequirementResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobProfileSubResourceResult<JobProfileRequirementResponse>>> Update(
        Guid publicId,
        Guid requirementPublicId,
        [FromBody] UpdateRequirementRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateJobProfileRequirementCommand(
                publicId,
                requirementPublicId,
                request.RequirementType,
                request.RequirementTypeCatalogItemId,
                request.CatalogItemId,
                request.CatalogCode,
                request.CatalogName,
                request.Description,
                request.SortOrder,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpDelete("{requirementPublicId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remove(
        Guid publicId,
        Guid requirementPublicId,
        [FromBody] ConcurrencyTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new RemoveJobProfileRequirementCommand(publicId, requirementPublicId, request.ConcurrencyToken),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(result).Result!;
        }

        Response.Headers[ParentConcurrencyTokenHeaderName] = result.Value.ParentConcurrencyToken.ToString();
        return NoContent();
    }

    public sealed class AddRequirementRequest
    {
        public JobRequirementType RequirementType { get; init; }
        public Guid? RequirementTypeCatalogItemId { get; init; }
        public Guid? CatalogItemId { get; init; }
        public string? CatalogCode { get; init; }
        public string? CatalogName { get; init; }
        public string Description { get; init; } = string.Empty;
        public int SortOrder { get; init; }
        public Guid ConcurrencyToken { get; init; }
    }

    public sealed class UpdateRequirementRequest
    {
        public JobRequirementType RequirementType { get; init; }
        public Guid? RequirementTypeCatalogItemId { get; init; }
        public Guid? CatalogItemId { get; init; }
        public string? CatalogCode { get; init; }
        public string? CatalogName { get; init; }
        public string Description { get; init; } = string.Empty;
        public int SortOrder { get; init; }
        public Guid ConcurrencyToken { get; init; }
    }

    public sealed class ConcurrencyTokenRequest
    {
        public Guid ConcurrencyToken { get; init; }
    }
}
